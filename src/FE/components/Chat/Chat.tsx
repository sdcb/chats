import {
  memo,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';
import { currentISODateString, getTz } from '@/utils/date';
import {
  findLastLeafId,
  findSelectedMessageByLeafId,
  generateResponseMessage,
  generateResponseMessages,
  generateUserMessage,
} from '@/utils/message';
import { throttle } from '@/utils/throttle';
import { getUserSession } from '@/utils/user';

import {
  ChatRole,
  ChatSpanStatus,
  ChatStatus,
  FileContent,
  FileDef,
  Message,
  MessageContentType,
  ReasoningContent,
  RequestContent,
  ResponseContent,
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
import ChatMessageMemoized from './MemoizedChatMessage';
import NoChat from './NoChat';
import NoModel from './NoModel';

import {
  deleteMessage,
  putChats,
  putMessageReactionClear,
  putMessageReactionUp,
  putResponseMessageEditAndSaveNew,
  putResponseMessageEditInPlace,
  responseContentToRequest,
} from '@/apis/clientApis';
import { cn } from '@/lib/utils';

const Chat = memo(() => {
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
    },
    selectedChat,
    hasModel,
    chatDispatch,
    messageDispatch,
  } = useContext(HomeContext);
  const [autoScrollEnabled, setAutoScrollEnabled] = useState<boolean>(true);
  const [showScrollDownButton, setShowScrollDownButton] =
    useState<boolean>(false);
  const [showScrollToTopButton, setShowScrollToTopButton] =
    useState<boolean>(false);
  const [showScrollToPrevUserMessageButton, setShowScrollToPrevUserMessageButton] =
    useState<boolean>(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const chatContainerRef = useRef<HTMLDivElement>(null);

  // 如果没有选中的聊天，显示NoChat组件
  if (!selectedChat) {
    return hasModel() ? <NoChat /> : <NoModel />;
  }

  const getSelectedMessagesLastActiveMessage = () => {
    const selectedMessageLength = selectedMessages.length - 1;
    if (selectedMessageLength === -1) return null;
    const lastMessage = selectedMessages[selectedMessageLength].find(
      (x) => x.isActive,
    );
    return lastMessage;
  };

  const changeChatTitle = (title: string, append: boolean = false) => {
    if (!selectedChat) return;
    
    const newChats = chats.map((chat) => {
      if (chat.id === selectedChat.id) {
        const updatedChat = { ...chat };
        append ? (updatedChat.title += title) : (updatedChat.title = title);
        return updatedChat;
      }
      return chat;
    });
    chatDispatch(setChats(newChats));
  };

  const changeSelectedChatStatus = (status: ChatStatus) => {
    if (!selectedChat) return;
    
    const updatedChats = chats.map((chat) =>
      chat.id === selectedChat.id ? { ...chat, status } : chat
    );
    chatDispatch(setChats(updatedChats));
  };

  const startChat = () => {
    changeSelectedChatStatus(ChatStatus.Chatting);
  };

  const handleChatError = () => {
    changeSelectedChatStatus(ChatStatus.Failed);
  };

  const changeSelectedResponseMessage = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: string,
    status: ChatSpanStatus,
    finalMessageId?: string,
  ): IChatMessage[][] => {
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const contentCount = x.content.length - 1;
        let newContent = [...x.content];
        
        if (
          contentCount >= 0 &&
          newContent[contentCount].$type === MessageContentType.text
        ) {
          const oldText = (newContent[contentCount] as TextContent).c;
          const newText = oldText + text;
          newContent[contentCount] = {
            ...newContent[contentCount],
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
    newSelectedMsgs[messageCount] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseFile = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: FileDef,
  ): IChatMessage[][] => {
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const contentCount = x.content.length - 1;
        let newContent = [...x.content];
        
        if (
          contentCount >= 0 &&
          newContent[contentCount].$type === MessageContentType.fileId
        ) {
          // Update existing file content
          newContent[contentCount] = {
            ...newContent[contentCount],
            c: text
          } as FileContent;
        } else {
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
    newSelectedMsgs[messageCount] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseReason = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    text: string,
  ): IChatMessage[][] => {
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        const contentCount = x.content.length - 1;
        let newContent = [...x.content];
        
        if (
          contentCount >= 0 &&
          newContent[contentCount].$type === MessageContentType.reasoning
        ) {
          newContent[contentCount] = {
            ...newContent[contentCount],
            c: (newContent[contentCount] as ReasoningContent).c + text
          } as ReasoningContent;
        } else {
          newContent.push({
            i: '',
            $type: MessageContentType.reasoning,
            c: text,
          });
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
    newSelectedMsgs[messageCount] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseReasoningDuration = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    time: number,
  ): IChatMessage[][] => {
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
    const updatedMessageList = messageList.map((x) => {
      if (x.id === messageId) {
        return {
          ...x,
          reasoningDuration: time,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[messageCount] = updatedMessageList;
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
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
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
    newSelectedMsgs[messageCount] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseToolResult = (
    selectedMsgs: IChatMessage[][],
    messageId: string,
    toolCallId: string,
    result: string,
  ): IChatMessage[][] => {
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
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
    newSelectedMsgs[messageCount] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const changeSelectedResponseMessageInfo = (
    selectedMsgs: IChatMessage[][],
    spanId: number,
    message: IChatMessage,
  ): IChatMessage[][] => {
    const messageCount = selectedMsgs.length - 1;
    const messageList = selectedMsgs[messageCount];
    const updatedMessageList = messageList.map((x) => {
      if (x.spanId === spanId) {
        return {
          ...x,
          id: message.id,
          duration: message.duration,
          firstTokenLatency: message.firstTokenLatency,
          inputPrice: message.inputPrice,
          outputPrice: message.outputPrice,
          inputTokens: message.inputTokens,
          outputTokens: message.outputTokens,
        };
      }
      return x;
    });
    
    const newSelectedMsgs = [...selectedMsgs];
    newSelectedMsgs[messageCount] = updatedMessageList;
    messageDispatch(setSelectedMessages(newSelectedMsgs));
    return newSelectedMsgs;
  };

  const checkSelectChatModelIsExist = (spans: ChatSpanDto[]) => {
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
  };

  const handleSend = useCallback(
    async (message: Message, messageId?: string) => {
      if (!checkSelectChatModelIsExist(selectedChat.spans)) return;
      
      // 检查是否存在任何助手回复
      const hasAssistantResponse = selectedMessages.some(messageGroup => 
        messageGroup.some(msg => msg.role === ChatRole.Assistant)
      );
      
      // 如果有用户消息但没有助手回复，提示错误
      if (selectedMessages.length > 0 && !hasAssistantResponse) {
        toast.error(t('Cannot send message: No valid conversation context. Please start a new chat.'));
        return;
      }
      
      startChat();
      let { id: chatId } = selectedChat;
      let selectedMessageList = [...selectedMessages];
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
        timezoneOffset: getTz(),
        parentAssistantMessageId: messageId || null,
        userMessage: requestContent,
      };

      const response = await fetch(`${getApiUrl()}/api/chats/general`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${getUserSession()}`,
        },
        body: JSON.stringify(chatBody),
      });

      await handleChatMessage(response, selectedMessageList);
    },
    [chats, selectedChat, selectedMessages],
  );

  const handleRegenerate = async (
    spanId: number,
    messageId: string,
    modelId: number,
  ) => {
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

    const response = await fetch(
      `${getApiUrl()}/api/chats/regenerate-assistant-message`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${getUserSession()}`,
        },
        body: JSON.stringify(chatBody),
      },
    );

    await handleChatMessage(response, selectedMessageList);
  };

  const handleRegenerateAllAssistant = async (
    messageId: string,
    modelId: number,
  ) => {
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

    const response = await fetch(
      `${getApiUrl()}/api/chats/regenerate-all-assistant-message`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${getUserSession()}`,
        },
        body: JSON.stringify(chatBody),
      },
    );

    await handleChatMessage(response, selectedMessageList);
  };

  const handleEditAndSendMessage = async (
    message: Message,
    messageId?: string,
  ) => {
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

    const response = await fetch(`${getApiUrl()}/api/chats/general`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${getUserSession()}`,
      },
      body: JSON.stringify(chatBody),
    });

    await handleChatMessage(response, selectedMessageList);
  };

  const handleChatMessage = async (
    response: Response,
    selectedMessageList: IChatMessage[][],
  ) => {
  let messageList = [...messages];
  // 用于跟踪每个 span 最近一次非空的工具调用 ID，便于将 u 为 null 的参数片段归并
  const currentToolCallIdBySpan = new Map<number, string>();
    const data = response.body;
    if (!response.ok) {
      handleChatError();
      const errMsg = await response.text();
      toast.error(t(errMsg) || response.statusText);
      return;
    }
    if (!data) {
      handleChatError();
      return;
    }

    const reader = data.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    async function* processBuffer() {
      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }
        buffer += decoder.decode(value, { stream: true });
        let newlineIndex;
        while ((newlineIndex = buffer.indexOf('\r\n\r\n')) >= 0) {
          const line = buffer.slice(0, newlineIndex).trim();
          buffer = buffer.slice(newlineIndex + 4); // Skip '\r\n\r\n'
          if (line.startsWith('data: ')) {
            yield line.slice(6);
          }
          if (line === '') {
            continue;
          }
        }
      }
    }
    for await (const message of processBuffer()) {
      const value: SseResponseLine = JSON.parse(message);
      if (value.k === SseResponseKind.StopId) {
        chatDispatch(setStopIds([value.r]));
      } else if (value.k === SseResponseKind.ReasoningSegment) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        selectedMessageList = changeSelectedResponseReason(selectedMessageList, msgId, msg);
      } else if (value.k === SseResponseKind.Segment) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        selectedMessageList = changeSelectedResponseMessage(
          selectedMessageList,
          msgId,
          msg,
          ChatSpanStatus.Chatting,
        );
      } else if (value.k === SseResponseKind.Error) {
        const { r: msg, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
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
        selectedMessageList = changeSelectedResponseMessage(
          selectedMessageList,
          msgId,
          '',
          ChatSpanStatus.None,
        );
        selectedMessageList = changeSelectedResponseMessageInfo(selectedMessageList, spanId, msg);
        messageList.push(msg);
      } else if (value.k === SseResponseKind.StartResponse) {
        const { r: time, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        selectedMessageList = changeSelectedResponseReasoningDuration(
          selectedMessageList,
          msgId,
          time,
        );
      } else if (value.k === SseResponseKind.ImageGenerated) {
        const { r, i: spanId } = value;
        const msgId = `${ResponseMessageTempId}-${spanId}`;
        selectedMessageList = changeSelectedResponseFile(selectedMessageList, msgId, r);
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

    const chatList = chats.map((x) =>
      x.id === selectedChat.id
        ? { ...x, updatedAt: currentISODateString() }
        : x,
    );

    chatDispatch(setChats(chatList));
    messageDispatch(setSelectedMessages(selectedMsgs));
    messageDispatch(setMessages(messageList));
    changeSelectedChatStatus(ChatStatus.None);
  };

  useCallback(() => {
    if (autoScrollEnabled) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [autoScrollEnabled]);

  const handleScroll = () => {
    if (chatContainerRef.current) {
      const { scrollTop, scrollHeight, clientHeight } =
        chatContainerRef.current;
      const bottomTolerance = 30;

      if (scrollTop + clientHeight < scrollHeight - bottomTolerance) {
        setAutoScrollEnabled(false);
        setShowScrollDownButton(true && selectedMessages.length > 0);
      } else {
        setAutoScrollEnabled(true);
        setShowScrollDownButton(false);
      }

      // 判断是否显示滚动到顶部按钮（滚动超过100px时显示）
      setShowScrollToTopButton(scrollTop > 100);
      
      // 判断是否显示滚动到上一个用户消息按钮
      // 简单的逻辑：滚动超过200px且有多个消息时显示
      setShowScrollToPrevUserMessageButton(scrollTop > 200 && selectedMessages.length > 1);
    }
  };

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

  const scrollDown = () => {
    if (autoScrollEnabled) {
      messagesEndRef.current?.scrollIntoView(true);
    }
  };
  const throttledScrollDown = throttle(scrollDown, 250);

  useEffect(() => {
    throttledScrollDown();
    handleScroll();
  }, [selectedMessages, throttledScrollDown]);

  const handleChangePrompt = (prompt: Prompt) => {
    // to do
  };

  const handleChangeChatLeafMessageId = (messageId: string) => {
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
    
    // 更新selectedChat的leafMessageId
    const chatList = chats.map((x) =>
      x.id === selectedChat.id
        ? { ...x, leafMessageId: messageId, updatedAt: currentISODateString() }
        : x,
    );
    chatDispatch(setChats(chatList));
    
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
      
      // 更新chats中的leafMessageId
      const updatedChats = chats.map((chat) =>
        chat.id === selectedChat.id
          ? { ...chat, leafMessageId: copyMsg!.id }
          : chat
      );
      chatDispatch(setChats(updatedChats));
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

  return (
    <div className="relative flex-1">
      <div className="flex flex-col">
        <div className="relative h-16">{selectedChat && <ChatHeader />}</div>
        <div
          className="relative h-[calc(100vh-64px)] overflow-x-hidden scroll-container w-full"
          ref={chatContainerRef}
          onScroll={handleScroll}
        >
          <div
            className="sm:w-full chat-container"
            style={{
              width: `calc(100vw - ${showChatBar ? 280 : 0}px)`,
            }}
          >
            {selectedChat && selectedMessages.length === 0 && (
              <ChatPresetList />
            )}
          </div>

          <ChatMessageMemoized
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

          {!hasModel() && !selectedChat?.id && <NoModel />}
          {hasModel() && !selectedChat?.id && <NoChat />}
          <div className={cn(showChatInput ? 'h-40' : 'h-2')}></div>
        </div>

        {hasModel() && selectedChat && (
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
Chat.displayName = 'Chat';
export default Chat;
