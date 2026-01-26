import { useEffect, useReducer, useState, useMemo } from 'react';

import { useRouter } from 'next/router';

import { useCreateReducer } from '@/hooks/useCreateReducer';
import useTranslation from '@/hooks/useTranslation';

import { currentISODateString } from '@/utils/date';
import { findSelectedMessageByLeafId } from '@/utils/message';
import { getSettings } from '@/utils/settings';
import { getUserSession, redirectToLoginPage } from '@/utils/user';
import { getChatCache, setChatCache } from '@/utils/chatCache';

import {
  ChatStatus,
  DefaultChatPaging,
  IChat,
  IChatPaging,
  UngroupedChatName,
} from '@/types/chat';
import { IChatMessage } from '@/types/chatMessage';
import { ChatResult, GetChatsParams, GetUserChatGroupWithMessagesResult } from '@/types/clientApis';
import { IChatGroup } from '@/types/group';

import {
  setChatGroup,
  setChatPaging,
  setChats,
  setIsChatsLoading,
  setIsMessagesLoading,
  setSelectedChatId,
  setStopIds,
} from '@/actions/chat.actions';
import {
  setMessages,
  setSelectedMessages,
} from '@/actions/message.actions';
import { setModelMap, setModels } from '@/actions/model.actions';
import { setDefaultPrompt, setPrompts } from '@/actions/prompt.actions';
import {
  setShowChatBar,
} from '@/actions/setting.actions';
import HomeContext, {
  HandleUpdateChatParams,
  HomeInitialState,
  initialState,
} from '@/contexts/home.context';
import chatReducer, { chatInitialState } from '@/reducers/chat.reducer';
import messageReducer, {
  messageInitialState,
} from '@/reducers/message.reducer';
import modelReducer, { modelInitialState } from '@/reducers/model.reducer';
import promptReducer, {
  promptInitialState,
} from '@/reducers/prompt.reducer';
import settingReducer, {
  settingInitialState,
} from '@/reducers/setting.reducer';
import Chat from '../Chat/ChatView';
import Chatbar from '../Chatbar/Chatbar';

import {
  getChatsByPaging,
  getDefaultPrompt,
  getUserChatGroupWithMessages,
  getUserMessages,
  getUserModels,
  getUserPromptBrief,
  postChats,
  stopChat,
} from '@/apis/clientApis';

const HomeContent = () => {
  const router = useRouter();
  const { t } = useTranslation();
  const [chatState, chatDispatch] = useReducer(chatReducer, chatInitialState);
  const [messageState, messageDispatch] = useReducer(
    messageReducer,
    messageInitialState,
  );
  const [modelState, modelDispatch] = useReducer(
    modelReducer,
    modelInitialState,
  );
  const [settingState, settingDispatch] = useReducer(
    settingReducer,
    settingInitialState,
  );
  const [promptState, promptDispatch] = useReducer(
    promptReducer,
    promptInitialState,
  );

  const { chats, chatPaging, stopIds, selectedChatId, chatGroups } = chatState;
  const { models } = modelState;

  // 解析 hash 中的 chatId，例如 "#/abc" -> "abc"
  const getHashChatId = (): string | undefined => {
    if (typeof window === 'undefined') return undefined;
    const hash = window.location.hash || '';
    if (hash.startsWith('#/')) return hash.slice(2) || undefined;
    return undefined;
  };

  // 根据 selectedChatId 纯计算 selectedChat（无副作用）
  const selectedChat = useMemo(() => {
    if (!selectedChatId) return undefined;
    return chats.find((chat) => chat.id === selectedChatId);
  }, [chats, selectedChatId]);

  // 当 chats 就绪且还未选中任何聊天时，依据 URL 或默认规则初始化 selectedChatId
  useEffect(() => {
    if (!chats.length) return;
    if (selectedChatId) return; // 已有选中，无需初始化

    // 优先 URL 中的 chatId，其次未分组的第一个，最后列表第一个
    const urlChatId = getHashChatId();
    const targetFromUrl = urlChatId
      ? chats.find((c) => c.id === urlChatId)
      : undefined;
    const target =
      targetFromUrl || chats.find((c) => c.groupId === null) || chats[0];
    if (target) {
      // 使用既有的选择逻辑，确保同步加载消息与选中路径
      selectChat(chats, target.id);
    }
  }, [chats, selectedChatId, router.asPath, chatDispatch]);

  // 当 selectedChatId 无效（对应的 chat 不在列表中）时，自动回退到有效的聊天
  useEffect(() => {
    if (!chats.length) return;
    if (!selectedChatId) return;
    const exists = chats.some((c) => c.id === selectedChatId);
    if (exists) return;

    const urlChatId = getHashChatId();
    const targetFromUrl = urlChatId
      ? chats.find((c) => c.id === urlChatId)
      : undefined;
    const fallback =
      targetFromUrl || chats.find((c) => c.groupId === null) || chats[0];
    if (fallback) {
      selectChat(chats, fallback.id);
    } else {
      chatDispatch(setSelectedChatId(undefined));
    }
  }, [chats, selectedChatId]);

  const contextValue = useCreateReducer<HomeInitialState>({
    initialState,
  });

  const selectChatMessage = (
    messages: IChatMessage[],
    leafMessageId?: string,
  ) => {
    messageDispatch(setMessages(messages));
    let leafMsgId = leafMessageId;
    if (!leafMsgId) {
      const messageCount = messages.length - 1;
      leafMsgId = messages[messageCount].id;
    }
    const selectedMessageList = findSelectedMessageByLeafId(
      messages,
      leafMsgId,
    );
    messageDispatch(setSelectedMessages(selectedMessageList));
  };

  const findChat = (chatList: IChat[], selectChatId?: string) => {
    let chatId = selectChatId || router.asPath.substring(3);
    if (chatList.length > 0) {
      const foundChat =
        chatList.find((x) => x.id === chatId) ||
        chatList.find((x) => x.groupId === null) ||
        chatList[0];
      return foundChat;
    }
    return undefined;
  };

  const supplyChatProperty = (chat: ChatResult): IChat => {
    return { ...chat, status: ChatStatus.None } as any;
  };

  function selectChat(chatList: IChat[], chatId?: string) {
    const chat = findChat(chatList, chatId);
    if (chat) {
      chatDispatch(setSelectedChatId(chat.id));
      chatDispatch(setIsMessagesLoading(true));
      messageDispatch(setMessages([]));
      messageDispatch(setSelectedMessages([]));

      getUserMessages(chat.id).then((data) => {
        if (data.length > 0) {
          selectChatMessage(data, chat.leafMessageId);
        } else {
          messageDispatch(setMessages([]));
          messageDispatch(setSelectedMessages([]));
        }
        chatDispatch(setIsMessagesLoading(false));
      }).catch(() => {
        chatDispatch(setIsMessagesLoading(false));
      });
    }
    return chat;
  }

  const getGroupSnapshotForCache = (groupId: string | null) => {
    if (groupId === null) {
      return {
        id: null,
        name: UngroupedChatName,
        rank: 0,
        isExpanded: true,
      } as const;
    }

    const matchedGroup = chatGroups.find((group) => group.id === groupId);
    if (matchedGroup) {
      return matchedGroup;
    }

    return {
      id: groupId,
      name: '',
      rank: 0,
      isExpanded: true,
    } as const;
  };

  const persistNewChatToCache = (chat: IChat) => {
    const cachedData = getChatCache();
    if (!cachedData?.length) return;

    const targetGroupId = chat.groupId ?? null;
    let updated = false;

    const updatedGroups = cachedData.map((group) => {
      const groupId = group.id ?? null;
      if (groupId !== targetGroupId) {
        return group;
      }

      updated = true;
      const filteredRows = group.chats.rows.filter((row) => row.id !== chat.id);
      const existed = filteredRows.length !== group.chats.rows.length;

      return {
        ...group,
        chats: {
          ...group.chats,
          rows: [{ ...chat }, ...filteredRows],
          count: existed ? group.chats.count : group.chats.count + 1,
        },
      };
    });

    if (!updated) {
      const snapshot = getGroupSnapshotForCache(targetGroupId);
      updatedGroups.push({
        id: snapshot.id,
        name: snapshot.name,
        rank: snapshot.rank,
        isExpanded: snapshot.isExpanded,
        chats: {
          rows: [{ ...chat }],
          count: 1,
        },
      } as GetUserChatGroupWithMessagesResult);
    }

    setChatCache(updatedGroups, '');
  };

  const removeChatsFromCache = (chatIds: string[]) => {
    if (!chatIds.length) return;
    const cachedData = getChatCache();
    if (!cachedData?.length) return;

    const idSet = new Set(chatIds);
    let changed = false;

    const updatedGroups = cachedData.map((group) => {
      const rows = group.chats.rows.filter((row) => !idSet.has(row.id));
      if (rows.length === group.chats.rows.length) {
        return group;
      }

      changed = true;
      const removedCount = group.chats.rows.length - rows.length;
      return {
        ...group,
        chats: {
          ...group.chats,
          rows,
          count: Math.max(0, group.chats.count - removedCount),
        },
      };
    });

    if (changed) {
      setChatCache(updatedGroups, '');
    }
  };

  const handleNewChat = (groupId: string | null = null) => {
    return postChats({
      title: t('New Conversation'),
      groupId,
    }).then((data) => {
      const chat = supplyChatProperty(data);
      chat.groupId = groupId;
      const chatList = [chat, ...chats];
      chatDispatch(setChats(chatList));
      chatDispatch(setSelectedChatId(chat.id));
      messageDispatch(setMessages([]));
      messageDispatch(setSelectedMessages([]));

      const chatId = data.id;
      router.push('#/' + chatId);
      persistNewChatToCache(chat);
    });
  };

  const hasModel = () => {
    return models?.length > 0;
  };

  const handleSelectChat = (chat: IChat) => {
    chatDispatch(setSelectedChatId(chat.id));
    chatDispatch(setIsMessagesLoading(true));
    messageDispatch(setMessages([]));
    messageDispatch(setSelectedMessages([]));
    getUserMessages(chat.id).then((data) => {
      if (data.length > 0) {
        selectChatMessage(data, chat.leafMessageId);
      } else {
        messageDispatch(setMessages([]));
        messageDispatch(setSelectedMessages([]));
      }
      chatDispatch(setIsMessagesLoading(false));
    }).catch(() => {
      chatDispatch(setIsMessagesLoading(false));
    });
    router.push('#/' + chat.id);
  };

  const handleUpdateChat = (
    chats: IChat[],
    id: string,
    params: HandleUpdateChatParams,
  ) => {
    const chatList = chats.map((x) => {
      if (x.id === id)
        return { ...x, ...params, updatedAt: currentISODateString() };
      return x;
    });
    chatDispatch(setChats(chatList));
  };

  const handleDeleteChat = (ids: string[]) => {
    // 获取被删除聊天的信息
    const deletedChats = chats.filter((x) => ids.includes(x.id));
    const chatList = chats.filter((x) => {
      return !ids.includes(x.id);
    });
    chatDispatch(setChats(chatList));
    removeChatsFromCache(ids);

    if (chatList.length > 0) {
      let chatIdToSelect: string | undefined;

      // 如果有被删除的聊天，尝试在同一分组中找到下一个或上一个聊天
      if (deletedChats.length > 0) {
        const deletedChat = deletedChats[0]; // 使用第一个被删除的聊天作为参考
        const deletedChatGroupId = deletedChat.groupId;
        
        // 获取被删除聊天在原始列表中的索引
        const originalIndex = chats.findIndex((chat) => chat.id === deletedChat.id);
        
        // 在同一分组中寻找下一个聊天（原始索引之后的聊天）
        for (let i = originalIndex + 1; i < chats.length; i++) {
          const chat = chats[i];
          if (chat.groupId === deletedChatGroupId && !ids.includes(chat.id)) {
            chatIdToSelect = chat.id;
            break;
          }
        }
        
        // 如果没有找到下一个，寻找上一个聊天（原始索引之前的聊天）
        if (!chatIdToSelect) {
          for (let i = originalIndex - 1; i >= 0; i--) {
            const chat = chats[i];
            if (chat.groupId === deletedChatGroupId && !ids.includes(chat.id)) {
              chatIdToSelect = chat.id;
              break;
            }
          }
        }
      }

      // 如果在同一分组中没有找到聊天，则按优先级选择
      if (!chatIdToSelect) {
        // 1. 优先选择未分组的第一个聊天
        const ungroupedChat = chatList.find((chat) => chat.groupId === null);
        if (ungroupedChat) {
          chatIdToSelect = ungroupedChat.id;
        } else {
          // 2. 选择第一个有聊天的分组的第一个聊天
          chatIdToSelect = chatList[0].id;
        }
      }

      selectChat(chatList, chatIdToSelect);
    } else {
      chatDispatch(setSelectedChatId(undefined));
      messageDispatch(setSelectedMessages([]));
      messageDispatch(setMessages([]));
    }
  };

  const handleStopChats = () => {
    let p = [] as any[];
    stopIds.forEach((id) => {
      p.push(stopChat(id));
    });
    Promise.all(p).then(() => {
      chatDispatch(setStopIds([]));
    });
  };

  const getChats = async (query: string = ''): Promise<{ usedCache: boolean }> => {
    // 处理缓存数据的通用函数
    const processChatData = (data: any[]) => {
      const chatList: IChat[] = [];
      let chatGroupList: IChatGroup[] = [];
      const chatPagingList: IChatPaging[] = [];
      data.forEach((d) => {
        if (query && d.chats.count === 0) return;
        chatPagingList.push({
          ...DefaultChatPaging,
          groupId: d.id,
          count: d.chats.count,
        });
        chatGroupList.push({ ...d, isExpanded: query ? true : d.isExpanded });
        chatList.push(...d.chats.rows);
      });
      
      chatDispatch(setChats(chatList));
      chatDispatch(setChatGroup(chatGroupList));
      chatDispatch(setChatPaging(chatPagingList));
    };

    // 1. 先尝试从缓存读取（仅在没有搜索条件时）
    let hasCacheData = false;
    if (!query) {
      const cachedData = getChatCache(query);
      if (cachedData && cachedData.length > 0) {
        // 立即显示缓存数据
        processChatData(cachedData);
        hasCacheData = true;
        // 有缓存数据时，立即关闭骨骼屏
        chatDispatch(setIsChatsLoading(false));
      }
    }

    // 2. 异步请求最新数据
    try {
      const data = await getUserChatGroupWithMessages({
        ...DefaultChatPaging,
        query,
      });
      
      // 更新缓存（仅在没有搜索条件时）
      if (!query) {
        setChatCache(data, query);
      }
      
      // 更新UI
      processChatData(data);
    } catch (error) {
      console.error('Failed to fetch chat groups:', error);
      // 如果有缓存数据，错误时就继续使用缓存数据，不做额外处理
      // 如果没有缓存数据，可以考虑显示错误提示
    }
    
    return { usedCache: hasCacheData };
  };

  const getChatsByGroup = (params: GetChatsParams) => {
    const { page, groupId } = params;
    getChatsByPaging(params).then((data) => {
      const { rows } = data || { rows: [], count: 0 };
      const mapRows = rows.map(
        (x) => ({ ...x, status: ChatStatus.None } as IChat),
      );
      let chatList = chats.concat(mapRows);
      chatDispatch(setChats(chatList));
      const chatPagingList = chatPaging.map((x) =>
        x.groupId === groupId ? { ...x, page } : x,
      );
      chatDispatch(setChatPaging(chatPagingList));
    });
  };

  useEffect(() => {
    // 加载设置
    const { showChatBar } = getSettings();
    settingDispatch(setShowChatBar(showChatBar));
  }, []);

  useEffect(() => {
    const session = getUserSession();
    if (!session) {
      redirectToLoginPage();
      return;
    }
    
    // 所有请求并行发起，不互相依赖
    chatDispatch(setIsChatsLoading(true));
    
    // 1. 加载模型列表
    getUserModels().then((modelList) => {
      modelDispatch(setModels(modelList));
      modelDispatch(setModelMap(modelList));
    });

    // 2. 加载默认提示词
    getDefaultPrompt().then((data) => {
      promptDispatch(setDefaultPrompt(data));
    });

    // 3. 加载聊天列表
    getChats().then(({ usedCache }) => {
      // 如果没有使用缓存，在 API 返回后关闭骨骼屏
      if (!usedCache) {
        chatDispatch(setIsChatsLoading(false));
      }
      // 如果使用了缓存，骨骼屏已经在 getChats 内部关闭了
    });

    // 4. 加载提示词简要列表
    getUserPromptBrief().then((data) => {
      promptDispatch(setPrompts(data));
    });
  }, []);

  // useEffect(() => {
  //   const handlePopState = (event: PopStateEvent) => {
  //     const chatId = getPathChatId(event.state?.as || '');
  //     selectChat(chats, chatId);
  //   };

  //   window.addEventListener('popstate', handlePopState);

  //   return () => {
  //     window.removeEventListener('popstate', handlePopState);
  //   };
  // }, [chats]);

  return (
    <HomeContext.Provider
      value={{
        ...contextValue,
        state: {
          ...contextValue.state,
          ...chatState,
          ...messageState,
          ...modelState,
          ...settingState,
          ...promptState,
        },
        selectedChat,
        chatDispatch: chatDispatch,
        messageDispatch: messageDispatch,
        modelDispatch: modelDispatch,
        settingDispatch: settingDispatch,
        promptDispatch: promptDispatch,

        handleNewChat,
        handleStopChats,
        handleSelectChat,
        handleUpdateChat,
        handleDeleteChat,
        hasModel,
        getChats,
        getChatsByGroup,
      }}
    >
      <div className="flex h-screen w-screen flex-col text-sm">
        <div className="flex h-full w-full bg-background chat-background">
          <Chatbar />
          <Chat />
        </div>
      </div>
    </HomeContext.Provider>
  );
};

export default HomeContent;
