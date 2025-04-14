import { useEffect, useReducer, useState } from 'react';

import { useRouter } from 'next/router';

import { useCreateReducer } from '@/hooks/useCreateReducer';
import useTranslation from '@/hooks/useTranslation';

import { currentISODateString } from '@/utils/date';
import { findSelectedMessageByLeafId } from '@/utils/message';
import { getSettings } from '@/utils/settings';
import { getUserSession, redirectToLoginPage } from '@/utils/user';

import {
  ChatStatus,
  DefaultChatPaging,
  IChat,
  IChatPaging,
} from '@/types/chat';
import { IChatMessage } from '@/types/chatMessage';
import { ChatResult, GetChatsParams } from '@/types/clientApis';
import { IChatGroup } from '@/types/group';

import Spinner from '@/components/Spinner/Spinner';

import {
  setChatGroup,
  setChatPaging,
  setChats,
  setIsChatsLoading,
  setSelectedChat,
  setStopIds,
} from '../../_actions/chat.actions';
import {
  setMessages,
  setSelectedMessages,
} from '../../_actions/message.actions';
import { setModelMap, setModels } from '../../_actions/model.actions';
import { setDefaultPrompt, setPrompts } from '../../_actions/prompt.actions';
import {
  setShowChatBar,
} from '../../_actions/setting.actions';
import HomeContext, {
  HandleUpdateChatParams,
  HomeInitialState,
  initialState,
} from '../../_contexts/home.context';
import chatReducer, { chatInitialState } from '../../_reducers/chat.reducer';
import messageReducer, {
  messageInitialState,
} from '../../_reducers/message.reducer';
import modelReducer, { modelInitialState } from '../../_reducers/model.reducer';
import promptReducer, {
  promptInitialState,
} from '../../_reducers/prompt.reducer';
import settingReducer, {
  settingInitialState,
} from '../../_reducers/setting.reducer';
import Chat from '../Chat/Chat';
import Chatbar from '../Chatbar/Chatbar';
import SettingsSheet from '../Settings/SettingsSheet';

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

  const { chats, chatPaging, stopIds } = chatState;
  const { models } = modelState;
  const [isPageLoading, setIsPageLoading] = useState(true);

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

  const selectChat = (chatList: IChat[], chatId?: string) => {
    const chat = findChat(chatList, chatId);
    if (chat) {
      chatDispatch(setSelectedChat(supplyChatProperty(chat)));

      getUserMessages(chat.id).then((data) => {
        if (data.length > 0) {
          selectChatMessage(data, chat.leafMessageId);
        } else {
          messageDispatch(setMessages([]));
          messageDispatch(setSelectedMessages([]));
        }
      });
    }
    return chat;
  };

  const handleNewChat = (groupId: string | null = null) => {
    postChats({
      title: t('New Conversation'),
      groupId,
    }).then((data) => {
      const chat = supplyChatProperty(data);
      chat.groupId = groupId;
      const chatList = [chat, ...chats];
      chatDispatch(setChats(chatList));
      chatDispatch(setSelectedChat(chat));
      messageDispatch(setMessages([]));
      messageDispatch(setSelectedMessages([]));

      const chatId = data.id;
      router.push('#/' + chatId);
    });
  };

  const hasModel = () => {
    return models?.length > 0;
  };

  const handleSelectChat = (chat: IChat) => {
    chatDispatch(setSelectedChat(chat));
    getUserMessages(chat.id).then((data) => {
      if (data.length > 0) {
        selectChatMessage(data, chat.leafMessageId);
      } else {
        messageDispatch(setMessages([]));
        messageDispatch(setSelectedMessages([]));
      }
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
    const chatList = chats.filter((x) => {
      return !ids.includes(x.id);
    });
    chatDispatch(setChats(chatList));

    if (chatList.length > 0) {
      selectChat(chatList, chatList[0].id);
    } else {
      chatDispatch(setSelectedChat(undefined));
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

  const getChats = async (query: string = '') => {
    const data = await getUserChatGroupWithMessages({
      ...DefaultChatPaging,
      query,
    });
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
    const chat = selectChat(chatList);
    if (chat)
      chatGroupList = chatGroupList.map((x) =>
        x.id === chat.groupId ? { ...x, isExpanded: true } : x,
      );
    chatDispatch(setChats(chatList));
    chatDispatch(setChatGroup(chatGroupList));
    chatDispatch(setChatPaging(chatPagingList));
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
    setIsPageLoading(true);
    const { showChatBar } = getSettings();
    settingDispatch(setShowChatBar(showChatBar));
  }, []);

  useEffect(() => {
    const session = getUserSession();
    if (!session) {
      redirectToLoginPage();
      return;
    }
    chatDispatch(setIsChatsLoading(true));
    getUserModels().then(async (modelList) => {
      modelDispatch(setModels(modelList));
      modelDispatch(setModelMap(modelList));

      if (modelList && modelList.length > 0) {
        getDefaultPrompt().then((data) => {
          promptDispatch(setDefaultPrompt(data));
        });
      }
      await getChats();
      chatDispatch(setIsChatsLoading(false));
    });

    getUserPromptBrief().then((data) => {
      promptDispatch(setPrompts(data));
    });
    setTimeout(() => setIsPageLoading(false), 800);
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

  const PageLoadingRender = () => (
    <div
      className={`fixed top-0 left-0 bottom-0 right-0 bg-background z-50 text-center text-[12.5px]`}
    >
      <div className="fixed w-screen h-screen top-1/2">
        <div className="flex justify-center">
          <Spinner className="text-gray-500 dark:text-gray-50" />
        </div>
      </div>
    </div>
  );

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
      {isPageLoading ? (
        <PageLoadingRender />
      ) : (
        <div className="flex h-screen w-screen flex-col text-sm">
          <div className="flex h-full w-full bg-background">
            <Chatbar />
            <Chat />
          </div>
        </div>
      )}
    </HomeContext.Provider>
  );
};

export default HomeContent;
