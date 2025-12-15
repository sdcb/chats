import {
  memo,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { currentISODateString, getTz } from '@/utils/date';
import {
  findLastLeafId,
  findSelectedMessageByLeafId,
  generateResponseMessage,
  generateResponseMessages,
  generateUserMessage,
} from '@/utils/message';
import { throttle } from '@/utils/throttle';
import { syncChatsToCache } from '@/utils/chatCache';

import {
  ChatRole,
  ChatSpanStatus,
  ChatStatus,
  FileContent,
  FileDef,
  IChat,
  Message,
  MessageContentType,
  ReasoningContent,
  RequestContent,
  ResponseContent,
  TempFileContent,
  TextContent,
  ToolCallContent,
  ToolResponseContent,
} from '@/types/chat';
import {
  IChatMessage,
  MessageDisplayType,
  ReactionMessageType,
  ResponseMessageTempId,
  SseResponseKind,
  SseResponseLine,
} from '@/types/chatMessage';
import { ChatSpanDto } from '@/types/clientApis';
import { Prompt } from '@/types/prompt';

import {
  setChats,
  setStopIds,
} from '@/actions/chat.actions';
import {
  setMessages,
  setSelectedMessages,
} from '@/actions/message.actions';
import HomeContext from '@/contexts/home.context';
import ChatHeader from './ChatHeader';
import ChatInput from './ChatInput';
import ChatPresetList from './ChatPresetList';
import { ChatMessage } from '@/components/ChatMessage';
import ChatMessagesSkeleton from './ChatMessagesSkeleton';
import NoChat from './NoChat';
import NoModel from './NoModel';

import {
  deleteMessage,
  getTurnGenerateInfo,
  putChats,
  putMessageReactionClear,
  putMessageReactionUp,
  putResponseMessageEditAndSaveNew,
  putResponseMessageEditInPlace,
  responseContentToRequest,
} from '@/apis/clientApis';
import { streamGeneralChat, streamRegenerateAssistant, streamRegenerateAllAssistant, ChatApiError } from '@/apis/chatApi';
import { cn } from '@/lib/utils';

const ChatView = memo(() => {
  const { t } = useTranslation();
  const {
    state: {
      chats,
      messages,
      selectedMessages,
      models,
      modelMap,
      showChatBar,
      showChatInput,
      isMessagesLoading,
    },
    selectedChat,
    hasModel,
    chatDispatch,
    messageDispatch,
  } = useContext(HomeContext);
  const chatsRef = useRef<IChat[]>(chats);
  useEffect(() => {
    chatsRef.current = chats;
  }, [chats]);

  const updateChatsState = useCallback(
    (updater: (prevChats: IChat[]) => IChat[]) => {
      const updatedChats = updater(chatsRef.current);
      chatsRef.current = updatedChats;
      chatDispatch(setChats(updatedChats));
      // 同步更新缓存
      syncChatsToCache(updatedChats);
    },
    [chatDispatch],
  );
  const [autoScrollEnabled, setAutoScrollEnabled] = useState<boolean>(true);
  const [autoScrollTemporarilyDisabled, setAutoScrollTemporarilyDisabled] =
    useState<boolean>(false);
  const [showScrollDownButton, setShowScrollDownButton] =
    useState<boolean>(false);
  const [showScrollToTopButton, setShowScrollToTopButton] =
    useState<boolean>(false);
  const [showScrollToPrevUserMessageButton, setShowScrollToPrevUserMessageButton] =
    useState<boolean>(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const chatContainerRef = useRef<HTMLDivElement>(null);
  const autoScrollDisabledRef = useRef<boolean>(false);
  const prevIsMessagesLoadingRef = useRef<boolean>(isMessagesLoading);

  useEffect(() => {
    autoScrollDisabledRef.current = autoScrollTemporarilyDisabled;
  }, [autoScrollTemporarilyDisabled]);

  // 当消息加载完成后（从 loading 变为 loaded），滚动到 leafMessage 的位置
  useEffect(() => {
    const wasLoading = prevIsMessagesLoadingRef.current;
    prevIsMessagesLoadingRef.current = isMessagesLoading;

    // 如果之前是 loading，现在不是 loading，说明消息加载完成
    if (wasLoading && !isMessagesLoading && selectedMessages.length > 0) {
      // 使用 requestAnimationFrame 确保 DOM 渲染完成后再滚动
      requestAnimationFrame(() => {
        // 获取最后一条活跃消息（leafMessage）
        const lastMessageGroup = selectedMessages[selectedMessages.length - 1];
        const leafMessage = lastMessageGroup?.find((x) => x.isActive);
        
        if (leafMessage) {
          // 通过 data-message-id 属性找到 leafMessage 的 DOM 元素
          const leafMessageElement = chatContainerRef.current?.querySelector(
            `[data-message-id="${leafMessage.id}"]`
          );
          if (leafMessageElement) {
            // 滚动到 leafMessage 的开始位置
            leafMessageElement.scrollIntoView({ behavior: 'instant', block: 'start' });
            return;
          }
        }
        // 如果找不到 leafMessage 元素，回退到滚动到底部
        messagesEndRef.current?.scrollIntoView({ behavior: 'instant' });
      });
    }
  }, [isMessagesLoading, selectedMessages]);

  const checkSelectChatModelIsExist = useCallback((spans: ChatSpanDto[]) => {
    const modelList = spans
      .filter((x) => !models.find((m) => m.modelId === x.modelId))
      .map((x) => x.modelName);
    const count = modelList.length;
    count > 0 &&
      toast.error(
        t('The model {{modelName}} does not exist', {
          modelName: modelList.join(' '),
        }),
      );
    return count === 0;
  }, [models, t]);

  const autoScrollCallback = useCallback(() => {
    if (autoScrollEnabled) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [autoScrollEnabled]);

  const scrollDown = useCallback(() => {
    if (autoScrollEnabled) {
      messagesEndRef.current?.scrollIntoView(true);
    }
  }, [autoScrollEnabled]);
  
  const throttledScrollDown = useMemo(
    () => throttle(scrollDown, 250),
    [scrollDown],
  );

  const handleScroll = useCallback(() => {
    if (chatContainerRef.current) {
      const { scrollTop, scrollHeight, clientHeight } =
        chatContainerRef.current;
      const bottomTolerance = 30;

      if (scrollTop + clientHeight < scrollHeight - bottomTolerance) {
        if (!autoScrollDisabledRef.current) {
          setAutoScrollEnabled(false);
        }
        setShowScrollDownButton(true && selectedMessages.length > 0);
      } else {
        if (!autoScrollDisabledRef.current) {
          setAutoScrollEnabled(true);
        }
        setShowScrollDownButton(false);
      }

      // 判断是否显示滚动到顶部按钮（滚动超过100px时显示）
      setShowScrollToTopButton(scrollTop > 100);
      
      // 判断是否显示滚动到上一个用户消息按钮
      // 简单的逻辑：滚动超过200px且有多个消息时显示
      setShowScrollToPrevUserMessageButton(scrollTop > 200 && selectedMessages.length > 1);
    }
  }, [selectedMessages]);

  useEffect(() => {
    if (!selectedChat) return;
    if (autoScrollEnabled) {
      throttledScrollDown();
    }
    handleScroll();
  }, [
    selectedMessages,
    selectedChat,
    throttledScrollDown,
    handleScroll,
    autoScrollEnabled,
  ]);

  useEffect(() => {
    const container = chatContainerRef.current;
    if (!container) return;

    let touchStartY = 0;

    const disableAutoScrollForRequest = () => {
      if (autoScrollDisabledRef.current) {
        return;
      }
      setAutoScrollEnabled(false);
      setAutoScrollTemporarilyDisabled(true);
      autoScrollDisabledRef.current = true;
    };

    const handleWheel = (event: WheelEvent) => {
      if (event.deltaY < 0) {
        disableAutoScrollForRequest();
      }
    };

    const handleTouchStart = (event: TouchEvent) => {
      if (event.touches.length === 1) {
        touchStartY = event.touches[0].clientY;
      }
    };

    const handleTouchMove = (event: TouchEvent) => {
      if (event.touches.length === 1) {
        const currentY = event.touches[0].clientY;
        const deltaY = currentY - touchStartY;
        if (deltaY > 0) {
          disableAutoScrollForRequest();
        }
      }
    };

    container.addEventListener('wheel', handleWheel, { passive: true });
    container.addEventListener('touchstart', handleTouchStart, {
      passive: true,
    });
    container.addEventListener('touchmove', handleTouchMove, {
      passive: true,
    });

    return () => {
      container.removeEventListener('wheel', handleWheel);
      container.removeEventListener('touchstart', handleTouchStart);
      container.removeEventListener('touchmove', handleTouchMove);
    };
  }, []);

  const getSelectedMessagesLastActiveMessage = () => {
    const selectedMessageLength = selectedMessages.length - 1;
    if (selectedMessageLength === -1) return null;
    const lastMessage = selectedMessages[selectedMessageLength].find(
      (x) => x.isActive,
    );
    return lastMessage;
  };

  const changeChatTitle = useCallback(
    (title: string, append: boolean = false) => {
      if (!selectedChat) return;

      updateChatsState((prevChats) =>
        prevChats.map((chat) => {
          if (chat.id !== selectedChat.id) {
            return chat;
          }

          const nextTitle = append
            ? `${chat.title ?? ''}${title}`
            : title;

          return { ...chat, title: nextTitle };
        }),
      );
    },
    [selectedChat, updateChatsState],
  );

  const changeSelectedChatStatus = useCallback(
    (status: ChatStatus) => {
      if (!selectedChat) return;

      updateChatsState((prevChats) =>
        prevChats.map((chat) =>
          chat.id === selectedChat.id ? { ...chat, status } : chat,
        ),
      );
    },
    [selectedChat, updateChatsState],
  );

  const startChat = useCallback(() => {
    changeSelectedChatStatus(ChatStatus.Chatting);
    autoScrollDisabledRef.current = false;
    setAutoScrollTemporarilyDisabled(false);
    setAutoScrollEnabled(true);
  }, [changeSelectedChatStatus]);

  const handleChatError = useCallback(() => {
    changeSelectedChatStatus(ChatStatus.Failed);
  }, [changeSelectedChatStatus]);

  const changeSelectedResponseMessage = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: string,
    status: ChatSpanStatus,
    finalMessageId?: string,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const lastContentIndex = x.content.length - 1;
        let newContent = [...x.content];
        
        if (
          lastContentIndex >= 0 &&
          newContent[lastContentIndex].$type === MessageContentType.text
        ) {
          const oldText = (newContent[lastContentIndex] as TextContent).c;
          const newText = oldText + text;
          newContent[lastContentIndex] = {
            ...newContent[lastContentIndex],
            c: newText
          } as TextContent;
        } else {
          newContent.push({ i: '', $type: MessageContentType.text, c: text });
        }

        if (status === ChatSpanStatus.Failed) {
          newContent.push({ i: '', $type: MessageContentType.error, c: text });
        }

        const updatedMessage = {
          ...x,
          content: newContent,
          status: status,
        };

        if (status === ChatSpanStatus.None) {
          updatedMessage.siblingIds = [...x.siblingIds, messageId];
          updatedMessage.id = finalMessageId!;
        }

        return updatedMessage;
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseFilePreview = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: FileDef,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const lastContentIndex = x.content.length - 1;
        let newContent = [...x.content];
        
        // 检查最后一个内容是否是 tempFileId 类型（预览图片）
        if (
          lastContentIndex >= 0 &&
          newContent[lastContentIndex].$type === MessageContentType.tempFileId
        ) {
          // 更新现有的预览图片
          newContent[lastContentIndex] = {
            ...newContent[lastContentIndex],
            c: text
          } as TempFileContent;
        } else {
          // 插入新的预览位置
          newContent.push({ i: '', $type: MessageContentType.tempFileId, c: text });
        }

        return {
          ...x,
          content: newContent,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseFileFinal = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: FileDef,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const lastContentIndex = x.content.length - 1;
        let newContent = [...x.content];
        
        // 检查最后一个内容是否是 tempFileId 类型（预览图片）
        if (
          lastContentIndex >= 0 &&
          newContent[lastContentIndex].$type === MessageContentType.tempFileId
        ) {
          // 将预览图片替换为最终图片（改变类型为 fileId）
          newContent[lastContentIndex] = {
            ...newContent[lastContentIndex],
            $type: MessageContentType.fileId,
            c: text
          } as FileContent;
        } else {
          // 没有预览图片，直接追加最终图片
          newContent.push({ i: '', $type: MessageContentType.fileId, c: text });
        }

        return {
          ...x,
          content: newContent,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseReason = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: string,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const lastContentIndex = x.content.length - 1;
        let newContent = [...x.content];
        
        if (
          lastContentIndex >= 0 &&
          newContent[lastContentIndex].$type === MessageContentType.reasoning
        ) {
          const last = newContent[lastContentIndex] as ReasoningContent;
          newContent[lastContentIndex] = {
            ...last,
            c: (last.c || '') + text,
            finished: false,
          } as ReasoningContent;
        } else {
          newContent.push({
            i: '',
            $type: MessageContentType.reasoning,
            c: text,
            finished: false,
          } as ReasoningContent);
        }

        return {
          ...x,
          status: ChatSpanStatus.Reasoning,
          content: newContent,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  // 标记当前消息中最近的一段 reasoning 为已完成（离开 ReasoningSegment）
  const changeSelectedResponseReasoningFinish = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const newContent = [...x.content];
        for (let i = newContent.length - 1; i >= 0; i--) {
          const c = newContent[i];
          if (c.$type === MessageContentType.reasoning) {
            const r = c as ReasoningContent;
            // 只在尚未标记完成时进行更新
            if (r.finished !== true) {
              newContent[i] = {
                ...r,
                finished: true,
              } as ReasoningContent;
            }
            break;
          }
        }
        return {
          ...x,
          content: newContent,
        };
      }
      return x;
    });

    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseToolCall = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    toolCallId: string,
    toolName: string,
    parameters: string,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        let newContent = [...x.content];

        // 查找是否已存在该工具调用/响应
        const callIndex = newContent.findIndex(
          (c) => c.$type === MessageContentType.toolCall && (c as ToolCallContent).u === toolCallId,
        );
        const respIndex = newContent.findIndex(
          (c) => c.$type === MessageContentType.toolResponse && (c as ToolResponseContent).u === toolCallId,
        );

        if (callIndex >= 0) {
          // 更新已有的工具调用参数（流式追加）
          const existingToolCall = newContent[callIndex] as ToolCallContent;
          newContent[callIndex] = {
            ...existingToolCall,
            n: toolName || existingToolCall.n,
            p: (existingToolCall.p || '') + parameters,
          };
        } else {
          // 插入新的工具调用；如果结果已先到，则把调用插到结果前面，确保参数在上方
          const toolCallContent: ToolCallContent = {
            i: `tool-call-${toolCallId}`,
            $type: MessageContentType.toolCall,
            u: toolCallId,
            n: toolName,
            p: parameters,
          };
          if (respIndex >= 0) {
            newContent.splice(respIndex, 0, toolCallContent);
          } else {
            newContent.push(toolCallContent);
          }
        }

        return {
          ...x,
          content: newContent,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseToolResult = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    toolCallId: string,
    result: string,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        let newContent = [...x.content];

        // 查找是否已存在该工具调用/响应
        const callIndex = newContent.findIndex(
          (c) => c.$type === MessageContentType.toolCall && (c as ToolCallContent).u === toolCallId,
        );
        const respIndex = newContent.findIndex(
          (c) => c.$type === MessageContentType.toolResponse && (c as ToolResponseContent).u === toolCallId,
        );

        if (respIndex >= 0) {
          // 更新现有工具响应
          const existingToolResponse = newContent[respIndex] as ToolResponseContent;
          newContent[respIndex] = {
            ...existingToolResponse,
            r: result,
          };
        } else {
          // 新的工具响应；如果工具调用已存在，则把响应插入到它的后面，保持“参数在上、结果在下”
          const toolResponseContent: ToolResponseContent = {
            i: `tool-response-${toolCallId}`,
            $type: MessageContentType.toolResponse,
            u: toolCallId,
            r: result,
          };
          if (callIndex >= 0) {
            newContent.splice(callIndex + 1, 0, toolResponseContent);
          } else {
            newContent.push(toolResponseContent);
          }
        }

        return {
          ...x,
          content: newContent,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseMessageInfo = (
    selectedMsgs: IChatMessage[][],
    spanId: number,
    message: IChatMessage,
  ): IChatMessage[][] => {
    const lastMessageGroupIndex = selectedMsgs.length - 1;
    const messageList = selectedMsgs[lastMessageGroupIndex];
    const updatedMessageList = messageList.map((x) => {
      if (x.spanId === spanId) {
        return {
          ...x,
          id: message.id,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[lastMessageGroupIndex] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const handleRegenerate = async (
    spanId: number,
    messageId: string,
    modelId: number,
  ) => {
    if (!selectedChat) return;
    if (!checkSelectChatModelIsExist(selectedChat.spans)) return;
    startChat();
    let { id: chatId } = selectedChat;
    let selectedMessageList = [...selectedMessages];
    const responseMessages = generateResponseMessage(
      spanId,
      messageId,
      modelId,
      modelMap[modelId]?.name,
    );
    const index = selectedMessages.findIndex(
      (x) => x.findIndex((m) => m.parentId === messageId) !== -1,
    );
    selectedMessageList[index] = selectedMessageList[index].map((m) => {
      if (m.spanId === spanId) {
        responseMessages.siblingIds = [responseMessages.id, ...m.siblingIds];
        return responseMessages;
      }
      return m;
    });

    selectedMessageList = selectedMessageList.slice(0, index + 1);

    messageDispatch(setSelectedMessages(selectedMessageList));

    let chatBody = {
      chatId,
      spanId,
      modelId,
      parentUserMessageId: messageId || null,
      timezoneOffset: getTz(),
    };

    try {
      const stream = streamRegenerateAssistant(chatBody);
      await handleChatMessage(stream, selectedMessageList);
    } catch (e: any) {
      handleChatError();
      const err = e as ChatApiError;
      const msg = err?.message || (typeof e === 'string' ? e : '');
      toast.error(t(msg) || msg);
    }
  };

  const handleRegenerateAllAssistant = async (
    messageId: string,
    modelId: number,
  ) => {
    if (!selectedChat) return;
    if (!checkSelectChatModelIsExist(selectedChat.spans)) return;
    startChat();
    let { id: chatId } = selectedChat;
    
    // 找到用户消息在 selectedMessages 中的位置
    const userMessageIndex = selectedMessages.findIndex(
      (x) => x.findIndex((m) => m.id === messageId) !== -1,
    );
    
    if (userMessageIndex === -1) return;
    
    // 保留用户消息及之前的消息，重新生成所有助手消息
    let selectedMessageList = selectedMessages.slice(0, userMessageIndex + 1);
    
    // 为所有启用的 span 生成新的响应消息
    let responseMessages = generateResponseMessages(selectedChat, messageId);
    selectedMessageList.push(responseMessages);
    
    messageDispatch(setSelectedMessages(selectedMessageList));

    let chatBody = {
      chatId,
      modelId,
      parentUserMessageId: messageId,
      timezoneOffset: getTz(),
    };

    try {
      const stream = streamRegenerateAllAssistant(chatBody);
      await handleChatMessage(stream, selectedMessageList);
    } catch (e: any) {
      handleChatError();
      const err = e as ChatApiError;
      const msg = err?.message || (typeof e === 'string' ? e : '');
      toast.error(t(msg) || msg);
    }
  };

  const handleEditAndSendMessage = async (
    message: Message,
    messageId?: string,
  ) => {
    if (!selectedChat) return;
    if (!checkSelectChatModelIsExist(selectedChat.spans)) return;
    startChat();
    let { id: chatId, spans: chatSpans } = selectedChat;
    let index = selectedMessages.findIndex(
      (x) => x.findIndex((m) => m.id === messageId) !== -1,
    );
    index += 1;
    let selectedMessageList = selectedMessages.slice(0, index);
    let userMessage = generateUserMessage(message.content, messageId);
    selectedMessageList.push([userMessage]);
    let responseMessages = generateResponseMessages(selectedChat, messageId);
    selectedMessageList.push(responseMessages);
    messageDispatch(setSelectedMessages(selectedMessageList));

    const requestContent: RequestContent[] = responseContentToRequest(
      message.content,
    );
    let chatBody = {
      chatId,
      parentAssistantMessageId: messageId || null,
      userMessage: requestContent,
      timezoneOffset: getTz(),
    };

    try {
      const stream = streamGeneralChat(chatBody);
      await handleChatMessage(stream, selectedMessageList);
    } catch (e: any) {
      handleChatError();
      const err = e as ChatApiError;
      const msg = err?.message || (typeof e === 'string' ? e : '');
      toast.error(t(msg) || msg);
    }
  };

  const handleChatMessage = useCallback(
    async (
      stream: AsyncIterable<SseResponseLine>,
      selectedMessageList: IChatMessage[][],
    ) => {
      if (!selectedChat) return;
      let messageList = [...messages];
      // 用于跟踪每个 span 最近一次非空的工具调用 ID，便于将 u 为 null 的参数片段归并
      const currentToolCallIdBySpan = new Map<number, string>();
    for await (const value of stream) {
      if (value.k === SseResponseKind.StopId) {
        chatDispatch(setStopIds([value.r]));
      } else if (value.k === SseResponseKind.ReasoningSegment) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        selectedMessageList = changeSelectedResponseReason(selectedMessageList, msgId, msg);
      } else if (value.k === SseResponseKind.Segment) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseMessage(
          selectedMessageList,
          msgId,
          msg,
          ChatSpanStatus.Chatting,
        );
      } else if (value.k === SseResponseKind.Error) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseMessage(
          selectedMessageList,
          msgId,
          msg,
          ChatSpanStatus.Failed,
        );
      } else if (value.k === SseResponseKind.UserMessage) {
        messageList.push(value.r);
      } else if (value.k === SseResponseKind.ResponseMessage) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseMessage(
          selectedMessageList,
          msgId,
          '',
          ChatSpanStatus.None,
        );
        selectedMessageList = changeSelectedResponseMessageInfo(selectedMessageList, spanId, msg);
        messageList.push(msg);
      } else if (value.k === SseResponseKind.StartResponse) {
        const { i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
      } else if (value.k === SseResponseKind.ImageGenerating) {
        const { r, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseFilePreview(selectedMessageList, msgId, r);
      } else if (value.k === SseResponseKind.ImageGenerated) {
        const { r, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseFileFinal(selectedMessageList, msgId, r);
      } else if (value.k === SseResponseKind.CallingTool) {
        // 13 事件：u 仅在首个片段非空，后续片段 u/r 可能为 null，只携带 p（参数增量）
        const { u, r: toolName, p: parameters, i: spanId } = value;
        if (u) {
          currentToolCallIdBySpan.set(spanId, u);
        }
        const toolCallId = (u ?? currentToolCallIdBySpan.get(spanId)) as string | undefined;
        if (!toolCallId) {
          // 尚未获取到工具调用 ID，无法归并，跳过本片段
          continue;
        }
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseToolCall(
          selectedMessageList,
          msgId,
          toolCallId,
          toolName ?? '',
          parameters ?? '',
        );
      } else if (value.k === SseResponseKind.ToolCompleted) {
        const { u: toolCallId, r: result, i: spanId } = value as any;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        // 离开 ReasoningSegment，完成上一段 reasoning
        selectedMessageList = changeSelectedResponseReasoningFinish(selectedMessageList, msgId);
        selectedMessageList = changeSelectedResponseToolResult(
          selectedMessageList,
          msgId,
          toolCallId,
          result,
        );
        // 若该 span 的活动调用已完成，清除追踪
        if (currentToolCallIdBySpan.get(spanId) === toolCallId) {
          currentToolCallIdBySpan.delete(spanId);
        }
      } else if (value.k === SseResponseKind.UpdateTitle) {
        changeChatTitle(value.r);
      } else if (value.k === SseResponseKind.TitleSegment) {
        changeChatTitle(value.r, true);
      } else {
        console.log('Unknown message', value);
      }
    }

    const leafMessageId = messageList[messageList.length - 1].id;
    const selectedMsgs = findSelectedMessageByLeafId(
      messageList,
      leafMessageId,
    );

    const updatedAt = currentISODateString();
    updateChatsState((prevChats) =>
      prevChats.map((x) =>
        x.id === selectedChat.id
          ? { ...x, leafMessageId, updatedAt }
          : x,
      ),
    );
    messageDispatch(setSelectedMessages(selectedMsgs));
    messageDispatch(setMessages(messageList));
    changeSelectedChatStatus(ChatStatus.None);
    autoScrollDisabledRef.current = false;
    setAutoScrollTemporarilyDisabled(false);
    },
    [
      changeChatTitle,
      changeSelectedChatStatus,
      chatDispatch,
      messageDispatch,
      messages,
      selectedChat,
      updateChatsState,
    ],
  );

  const handleScrollDown = () => {
    chatContainerRef.current?.scrollTo({
      top: chatContainerRef.current.scrollHeight,
      behavior: 'smooth',
    });
  };

  const handleScrollToTop = () => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const handleScrollToPrevUserMessage = () => {
    if (!chatContainerRef.current) return;
    
    // 获取当前滚动位置
    const currentScrollTop = chatContainerRef.current.scrollTop;
    
    // 从 selectedMessages 中找到用户消息，按时间顺序排列
    const allUserMessages: string[] = [];
    selectedMessages.forEach((messageGroup) => {
      messageGroup.forEach((message) => {
        if (message.role === ChatRole.User) {
          allUserMessages.push(message.id);
        }
      });
    });
    
    if (allUserMessages.length === 0) {
      handleScrollToTop();
      return;
    }
    
    // 使用新的data属性查找用户消息元素
    let targetElement: Element | null = null;
    
    // 从后往前查找当前视口上方的用户消息
    for (let i = allUserMessages.length - 1; i >= 0; i--) {
      const messageId = allUserMessages[i];
      
      // 使用data属性查找用户消息元素
      const element = chatContainerRef.current.querySelector(`[data-user-message-id="${messageId}"]`) ||
                     chatContainerRef.current.querySelector(`[data-message-id="${messageId}"][data-message-role="user"]`);
      
      if (element) {
        const elementTop = (element as HTMLElement).offsetTop;
        // 如果这个消息在当前视口上方（留100px缓冲区）
        if (elementTop < currentScrollTop - 100) {
          targetElement = element;
          break;
        }
      }
    }
    
    if (targetElement) {
      targetElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else if (allUserMessages.length > 0) {
      // 如果没有找到在视口上方的用户消息，滚动到第一个用户消息
      const firstMessageId = allUserMessages[0];
      const firstElement = chatContainerRef.current.querySelector(`[data-user-message-id="${firstMessageId}"]`) ||
                          chatContainerRef.current.querySelector(`[data-message-id="${firstMessageId}"][data-message-role="user"]`);
      
      if (firstElement) {
        firstElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      } else {
        handleScrollToTop();
      }
    } else {
      handleScrollToTop();
    }
  };

  const handleChangePrompt = (prompt: Prompt) => {
    // to do
  };

  const handleChangeChatLeafMessageId = (messageId: string) => {
    if (!selectedChat) return;
    if (selectedChat.status === ChatStatus.Chatting) return;
    for (const levelMessages of selectedMessages) {
      for (const message of levelMessages) {
        if (message.id === messageId && message.isActive) {
          return; // Already selected
        }
      }
    }
    const leafId = findLastLeafId(messages, messageId);
    const selectedMsgs = findSelectedMessageByLeafId(messages, leafId);
    messageDispatch(setSelectedMessages(selectedMsgs));
    
    const updatedAt = currentISODateString();
    // 更新selectedChat的leafMessageId
    updateChatsState((prevChats) =>
      prevChats.map((x) =>
        x.id === selectedChat.id
          ? {
              ...x,
              leafMessageId: leafId,
              updatedAt,
            }
          : x,
      ),
    );
    
    putChats(selectedChat.id, {
      setsLeafMessageId: true,
      leafMessageId: leafId,
    });
  };

  const handleReactionMessage = (
    type: ReactionMessageType,
    messageId: string,
  ) => {
    const message = messages.find((m) => m.id === messageId);
    let p = null;
    let reaction: boolean | null = null;

    if (type === ReactionMessageType.Good) {
      if (message?.reaction) {
        p = putMessageReactionClear(messageId);
      } else {
        reaction = true;
        p = putMessageReactionUp(messageId);
      }
    } else {
      if (message?.reaction === false) {
        p = putMessageReactionClear(messageId);
      } else {
        reaction = false;
        p = putMessageReactionUp(messageId);
      }
    }

    p.then(() => {
      const msgs = messages.map((m) =>
        m.id === messageId ? { ...m, reaction } : m,
      );
      const selectedMsgs = selectedMessages.map((msg) => {
        return msg.map((m) => (m.id === message?.id ? { ...m, reaction } : m));
      });
      messageDispatch(setSelectedMessages(selectedMsgs));
      messageDispatch(setMessages(msgs));
    });
  };

  const handleUpdateResponseMessage = async (
    messageId: string,
    content: ResponseContent,
    isCopy: boolean = false,
  ) => {
    if (!selectedChat) return;
    let data: IChatMessage;
    const params = {
      messageId,
      contentId: content.i,
      c: ('c' in content ? content.c : '') as string,
    };
    if (isCopy) {
      data = await putResponseMessageEditAndSaveNew(params);
    } else {
      await putResponseMessageEditInPlace(params);
    }

    let msgs = structuredClone(messages);
    if (!isCopy) {
      msgs = messages.map((x) => {
        if (x.id === messageId) {
          const newContent = x.content.map((c) => {
            if (c.i === content.i) return content;
            return c;
          });
          return { ...x, content: newContent, edited: true };
        }
        return x;
      });
    }

    let copyMsg: IChatMessage;
    let selectedMsgs = selectedMessages.map((msg) => {
      return msg.map((m) => {
        if (m.id === messageId) {
          if (isCopy) {
            const msgSiblingIds = [...m.siblingIds, data.id];
            copyMsg = {
              ...data,
              siblingIds: msgSiblingIds,
            };
            return copyMsg;
          } else {
            const newContent = m.content.map((c) => {
              if (c.i === content.i) return content;
              return c;
            });
            m.content = newContent;
          }
        }
        return m;
      });
    });
    if (isCopy) {
      msgs.map((m) => {
        if (copyMsg.siblingIds.includes(m.id)) {
          m.siblingIds = copyMsg.siblingIds;
        }
        return m;
      });

      msgs.push(copyMsg!);
      
      const updatedAt = currentISODateString();
      // 更新chats中的leafMessageId
      updateChatsState((prevChats) =>
        prevChats.map((chat) =>
          chat.id === selectedChat.id
            ? { ...chat, leafMessageId: copyMsg!.id, updatedAt }
            : chat,
        ),
      );
    }
    messageDispatch(setMessages(msgs));
    messageDispatch(setSelectedMessages(selectedMsgs));
  };

  const handleUpdateUserMessage = async (
    messageId: string,
    content: ResponseContent,
  ) => {
    const params = {
      messageId,
      contentId: content.i,
      c: ('c' in content ? content.c : '') as string,
    };
    await putResponseMessageEditInPlace(params);

    const msgs = messages.map((x) => {
      if (x.id === messageId && x.role === ChatRole.User) {
        const newContent = x.content.map((c) => {
          if (c.i === content.i) return content;
          return c;
        });
        return { ...x, content: newContent };
      }
      return x;
    });

    const selectedMsgs = selectedMessages.map((msg) => {
      return msg.map((m) => {
        if (m.id === messageId && m.role === ChatRole.User) {
          const newContent = m.content.map((c) => {
            if (c.i === content.i) return content;
            return c;
          });
          return {
            ...m,
            content: newContent,
          };
        }
        return m;
      });
    });

    messageDispatch(setMessages(msgs));
    messageDispatch(setSelectedMessages(selectedMsgs));
  };

  const handleDeleteMessage = async (messageId: string) => {
    let nextMsgId = '';
    let msgs = messages.filter((x) => x.id !== messageId);
    let deletedMessage: IChatMessage | undefined;
    
    // 找到被删除的消息
    selectedMessages.forEach((msg) => {
      msg.forEach((m) => {
        if (m.id === messageId) {
          deletedMessage = m;
        }
      });
    });

    if (!deletedMessage) return;

    // 删除逻辑按照新的要求
    if (deletedMessage.siblingIds.length > 1) {
      // 如果有同级消息，选择其他同级消息
      const siblingIds = deletedMessage.siblingIds.filter((id) => id !== deletedMessage!.id);
      nextMsgId = siblingIds[siblingIds.length - 1];
    } else {
      // 如果是最后一个同级消息，需要找替代方案
      if (deletedMessage.role === ChatRole.Assistant) {
        // 删除的是助手消息
        // 1. 寻找其他助手响应消息（同一父级下的其他助手消息）
        const otherAssistantMessages = msgs.filter(m => 
          m.role === ChatRole.Assistant && 
          m.parentId === deletedMessage!.parentId &&
          m.id !== deletedMessage!.id
        );
        
        if (otherAssistantMessages.length > 0) {
          // 使用另一侧响应的当前显示的消息
          const activeAssistant = otherAssistantMessages.find(m => m.isActive);
          nextMsgId = activeAssistant ? activeAssistant.id : otherAssistantMessages[0].id;
        } else {
          // 2. 没有任何响应消息了，则选择父级用户消息
          if (deletedMessage.parentId) {
            nextMsgId = deletedMessage.parentId;
          } else {
            // 3. 父级也没有，使用空字符串表示null
            nextMsgId = '';
          }
        }
      } else if (deletedMessage.role === ChatRole.User) {
        // 删除的是用户消息，同时删除所有子消息
        const childMessages = messages.filter(m => m.parentId === deletedMessage!.id);
        childMessages.forEach(child => {
          msgs = msgs.filter(x => x.id !== child.id);
        });
        
        // 选择父级消息
        if (deletedMessage.parentId) {
          nextMsgId = deletedMessage.parentId;
        } else {
          nextMsgId = '';
        }
      }
    }

    const leafId = nextMsgId ? findLastLeafId(msgs, nextMsgId) : null;
    await deleteMessage(messageId, leafId);
    const selectedMsgs = leafId ? findSelectedMessageByLeafId(msgs, leafId) : [];
    
    // 更新chats中的leafMessageId
    if (selectedChat && leafId) {
      const updatedAt = currentISODateString();
      updateChatsState((prevChats) =>
        prevChats.map((x) =>
          x.id === selectedChat.id
            ? { ...x, leafMessageId: leafId, updatedAt }
            : x,
        ),
      );
    }
    
    messageDispatch(setSelectedMessages(selectedMsgs));
    messageDispatch(setMessages(msgs));
  };


  const handleChangeDisplayType = (
    messageId: string,
    type: MessageDisplayType,
  ) => {
    const msgs = messages.map((x) => {
      if (x.id === messageId) {
        x.displayType = type;
      }
      return x;
    });

    const selectedMsgs = selectedMessages.map((msg) => {
      return msg.map((m) => {
        if (m.id === messageId) {
          m.displayType = type;
        }
        return m;
      });
    });

    messageDispatch(setMessages(msgs));
    messageDispatch(setSelectedMessages(selectedMsgs));
  };

  const handleSend = useCallback(
    async (message: Message, messageId?: string) => {
      if (!selectedChat) return;
      if (!checkSelectChatModelIsExist(selectedChat.spans)) return;

      const hasAssistantResponse = selectedMessages.some((messageGroup) =>
        messageGroup.some((msg) => msg.role === ChatRole.Assistant),
      );

      if (selectedMessages.length > 0 && !hasAssistantResponse) {
        toast.error(
          t(
            'Cannot send message: No valid conversation context. Please start a new chat.',
          ),
        );
        return;
      }

      startChat();
      const { id: chatId } = selectedChat;
      let selectedMessageList = [...selectedMessages];
      const userMessage = generateUserMessage(message.content, messageId);
      selectedMessageList.push([userMessage]);
      const responseMessages = generateResponseMessages(selectedChat, messageId);
      selectedMessageList.push(responseMessages);
      messageDispatch(setSelectedMessages(selectedMessageList));
      const requestContent: RequestContent[] = responseContentToRequest(
        message.content,
      );
      const chatBody = {
        chatId,
        timezoneOffset: getTz(),
        parentAssistantMessageId: messageId || null,
        userMessage: requestContent,
      };

      try {
        const stream = streamGeneralChat(chatBody);
        await handleChatMessage(stream, selectedMessageList);
      } catch (e: any) {
        handleChatError();
        const err = e as ChatApiError;
        const msg = err?.message || (typeof e === 'string' ? e : '');
        toast.error(t(msg) || msg);
      }
    },
    [
      checkSelectChatModelIsExist,
      handleChatError,
      handleChatMessage,
      messageDispatch,
      selectedChat,
      selectedMessages,
      startChat,
      t,
    ],
  );

  // 如果没有选中的聊天，显示NoChat或NoModel组件
  if (!selectedChat) {
    return (
      <div className="relative flex-1">
        <div className="flex flex-col">
          <div className="relative h-16"></div>
          <div
            className="relative h-[calc(100vh-64px)] overflow-x-hidden scroll-container w-full"
            ref={chatContainerRef}
          >
            {hasModel() ? <NoChat /> : <NoModel />}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="relative flex-1">
      <div className="flex flex-col">
        <div className="relative h-16"><ChatHeader /></div>
        <div
          className="relative h-[calc(100vh-64px)] overflow-x-hidden scroll-container w-full"
          ref={chatContainerRef}
          onScroll={handleScroll}
        >
          {isMessagesLoading ? (
            <ChatMessagesSkeleton selectedChat={selectedChat} />
          ) : (
            <>
              <div
                className="sm:w-full chat-container"
                style={{
                  width: `calc(100vw - ${showChatBar ? 280 : 0}px)`,
                }}
              >
                {selectedMessages.length === 0 && <ChatPresetList />}
              </div>

              <ChatMessage
                selectedChat={selectedChat}
                selectedMessages={selectedMessages}
                models={models}
                messagesEndRef={messagesEndRef}
                onChangeChatLeafMessageId={handleChangeChatLeafMessageId}
                onEditAndSendMessage={handleEditAndSendMessage}
                onRegenerate={handleRegenerate}
                onReactionMessage={handleReactionMessage}
                onEditResponseMessage={handleUpdateResponseMessage}
                onEditUserMessage={handleUpdateUserMessage}
                onDeleteMessage={handleDeleteMessage}
                onChangeDisplayType={handleChangeDisplayType}
                onRegenerateAllAssistant={handleRegenerateAllAssistant}
              />

              <div className={cn(showChatInput ? 'h-32' : 'h-2')}></div>
            </>
          )}
        </div>

        {hasModel() && (
          <ChatInput
            onSend={(message) => {
              const lastMessage = getSelectedMessagesLastActiveMessage();
              handleSend(message, lastMessage?.id);
            }}
            onScrollDownClick={handleScrollDown}
            onScrollToTopClick={handleScrollToTop}
            onScrollToPrevUserMessageClick={handleScrollToPrevUserMessage}
            showScrollDownButton={showScrollDownButton}
            showScrollToTopButton={showScrollToTopButton}
            showScrollToPrevUserMessageButton={showScrollToPrevUserMessageButton}
            onChangePrompt={handleChangePrompt}
          />
        )}
      </div>
    </div>
  );
});
ChatView.displayName = 'ChatView';
export default ChatView;
