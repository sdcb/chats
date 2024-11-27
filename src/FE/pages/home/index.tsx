import { useEffect, useRef } from 'react';
import { Dispatch, createContext } from 'react';

import Head from 'next/head';
import { useRouter } from 'next/router';

import { useCreateReducer } from '@/hooks/useCreateReducer';
import { ActionType } from '@/hooks/useCreateReducer';
import useTranslation from '@/hooks/useTranslation';

import {
  getPathChatId,
  getSelectChatId,
  saveSelectChatId,
} from '@/utils/chats';
import { getSelectMessages } from '@/utils/message';
import { getStorageModelId, setStorageModelId } from '@/utils/model';
import { formatPrompt } from '@/utils/promptVariable';
import {
  DEFAULT_SETTINGS,
  getSettings,
  getSettingsLanguage,
  saveSettings,
} from '@/utils/settings';
import { Settings } from '@/utils/settings';
import { getLoginUrl, getUserInfo, getUserSession } from '@/utils/user';
import { UserSession } from '@/utils/user';

import { IChat, Role } from '@/types/chat';
import { ChatMessage } from '@/types/chatMessage';
import { ChatResult, GetChatsParams } from '@/types/clientApis';
import { UserModelConfig } from '@/types/model';
import { Prompt } from '@/types/prompt';

import { Chat } from '@/components/Chat/Chat';
import ChatSettingsBar from '@/components/ChatSettings/ChatSettingsBar';
import { Chatbar } from '@/components/Chatbar/Chatbar';
import PromptBar from '@/components/Promptbar';
import Spinner from '@/components/Spinner';

import {
  getChatsByPaging,
  getDefaultPrompt,
  getUserMessages,
  getUserModels,
  getUserPromptBrief,
  postChats,
} from '@/apis/clientApis';
import Decimal from 'decimal.js';
import { v4 as uuidv4 } from 'uuid';
import { AdminModelDto } from '@/types/adminApis';

interface HandleUpdateChatParams {
  isShared?: boolean;
  title?: string;
  chatModelId?: string;
}

interface HomeInitialState {
  user: UserSession | null;
  loading: boolean;
  messageIsStreaming: boolean;
  models: AdminModelDto[];
  chats: ChatResult[];
  chatsPaging: { count: number; page: number; pageSize: number };
  selectChat: IChat;
  selectModel: AdminModelDto | undefined;
  selectModels: AdminModelDto[];
  currentMessages: ChatMessage[];
  selectMessages: ChatMessage[];
  selectMessageLastId: string;
  currentChatMessageId: string;
  userModelConfig: UserModelConfig | undefined;
  chatError: boolean;
  prompts: Prompt[];
  settings: Settings;
  searchTerm: string;
}

const initialState: HomeInitialState = {
  user: null,
  loading: false,
  messageIsStreaming: false,
  currentMessages: [],
  userModelConfig: undefined,
  selectMessages: [],
  selectMessageLastId: '',
  currentChatMessageId: '',
  models: [],
  chats: [],
  chatsPaging: { count: 0, page: 1, pageSize: 50 },
  selectModel: undefined,
  selectModels: [],
  selectChat: {} as IChat,
  chatError: false,
  prompts: [],
  settings: DEFAULT_SETTINGS,
  searchTerm: '',
};

interface HomeContextProps {
  state: HomeInitialState;
  dispatch: Dispatch<ActionType<HomeInitialState>>;
  handleNewChat: () => void;
  handleSelectChat: (chat: IChat) => void;
  handleUpdateChat: (
    chats: ChatResult[],
    id: string,
    params: HandleUpdateChatParams,
  ) => void;
  handleUpdateSelectMessage: (lastLeafId: string) => void;
  handleUpdateCurrentMessage: (chatId: string) => void;
  handleDeleteChat: (id: string) => void;
  handleSelectModel: (model: AdminModelDto) => void;
  handleUpdateUserModelConfig: (value: any) => void;
  handleUpdateSettings: <K extends keyof Settings>(
    key: K,
    value: Settings[K],
  ) => void;
  hasModel: () => boolean;
  getChats: (params: GetChatsParams, models?: AdminModelDto[]) => void;
}

const HomeContext = createContext<HomeContextProps>(undefined!);

export { initialState, HomeContext };

const Home = () => {
  const router = useRouter();
  const { t } = useTranslation();
  const contextValue = useCreateReducer<HomeInitialState>({
    initialState,
  });

  const {
    state: { chats, currentMessages, models, user, userModelConfig, settings },
    dispatch,
  } = contextValue;
  const stopConversationRef = useRef<boolean>(false);

  const calcSelectModel = (chats: ChatResult[], models: AdminModelDto[]) => {
    const model = models.find((x) => x.modelId === chats[0]?.modelId);
    if (model) return model;
    else return models.length > 0 ? models[0] : undefined;
  };

  const getChatModel = (
    chats: ChatResult[],
    chatId: string,
    models: AdminModelDto[],
  ) => {
    const chatModelId = chats.find((x) => x.id === chatId)?.modelId;
    const model = models.find((x) => x.modelId === chatModelId);
    return model;
  };

  const chatErrorMessage = (messageId: string) : ChatMessage => {
    return {
      id: uuidv4(),
      parentId: messageId,
      childrenIds: [],
      assistantChildrenIds: [],
      role: 'assistant' as Role,
      content: { text: '', image: [] },
      inputTokens: 0,
      outputTokens: 0,
      inputPrice: new Decimal(0),
      outputPrice: new Decimal(0),
    };
  };

  const clamp = (value: number, min: number, max: number) => {
    return Math.min(Math.max(value, min), max);
  }

  const handleSelectModel = (model: AdminModelDto) => {
    if (!model) return;
    dispatch({ field: 'selectModel', value: model });
    const initialConfig = {
      temperature: clamp(0.85, model.minTemperature, model.maxTemperature),
      enableSearch: model.allowSearch ? false : null,
    };
    handleUpdateUserModelConfig(initialConfig);

    getDefaultPrompt().then((data) => {
      handleUpdateUserModelConfig({
        ...initialConfig,
        prompt: formatPrompt(data.content, { model }),
      });
    });
  };

  const handleNewChat = () => {
    postChats({ title: t('New Conversation') }).then((data) => {
      const model = calcSelectModel(chats, models);
      dispatch({ field: 'selectChat', value: data });
      dispatch({ field: 'selectMessageLastId', value: '' });
      dispatch({ field: 'currentMessages', value: [] });
      dispatch({ field: 'selectMessages', value: [] });
      dispatch({ field: 'chatError', value: false });
      dispatch({ field: 'chats', value: [data, ...chats] });
      handleSelectModel(model!);
      router.push('#/' + data.id);
    });
  };

  const handleUpdateCurrentMessage = (chatId: string) => {
    getUserMessages(chatId).then((data) => {
      if (data.length > 0) {
        dispatch({ field: 'currentMessages', value: data });
        const lastMessage = data[data.length - 1];
        const selectMessageList = getSelectMessages(data, lastMessage.id);
        dispatch({
          field: 'selectMessages',
          value: selectMessageList,
        });
        dispatch({ field: 'selectMessageLastId', value: lastMessage.id });
      } else {
        dispatch({ field: 'currentMessages', value: [] });
        dispatch({
          field: 'selectMessages',
          value: [],
        });
      }
    });
  };

  const handleSelectChat = (chat: IChat) => {
    dispatch({
      field: 'chatError',
      value: false,
    });
    dispatch({ field: 'selectChat', value: chat });
    const selectModel =
      getChatModel(chats, chat.id, models) || calcSelectModel(chats, models);
    selectModel && setStorageModelId(selectModel.modelId);
    getUserMessages(chat.id).then((data) => {
      if (data.length > 0) {
        dispatch({ field: 'currentMessages', value: data });
        const lastMessage = data[data.length - 1];
        const selectMessageList = getSelectMessages(data, lastMessage.id);
        if (lastMessage.role !== 'assistant') {
          dispatch({
            field: 'chatError',
            value: true,
          });
          selectMessageList.push(chatErrorMessage(lastMessage.id));
        }

        dispatch({
          field: 'selectMessages',
          value: selectMessageList,
        });
        dispatch({ field: 'selectMessageLastId', value: lastMessage.id });
        dispatch({ field: 'userModelConfig', value: chat.userModelConfig });
        dispatch({
          field: 'selectModel',
          value: selectModel,
        });
      } else {
        handleSelectModel(selectModel!);
        dispatch({ field: 'currentMessages', value: [] });
        dispatch({
          field: 'selectMessages',
          value: [],
        });
      }
    });
    router.push('#/' + chat.id);
    saveSelectChatId(chat.id);
  };

  const handleUpdateSelectMessage = (messageId: string) => {
    const selectMessageList = getSelectMessages(currentMessages, messageId);
    dispatch({
      field: 'selectMessages',
      value: selectMessageList,
    });
  };

  const handleUpdateUserModelConfig = (value: any) => {
    dispatch({
      field: 'userModelConfig',
      value: { ...userModelConfig, ...value },
    });
  };

  const handleUpdateChat = (
    chats: ChatResult[],
    id: string,
    params: HandleUpdateChatParams,
  ) => {
    const chatList = chats.map((x) => {
      if (x.id === id) return { ...x, ...params };
      return x;
    });

    dispatch({ field: 'chats', value: chatList });
  };

  const handleDeleteChat = (id: string) => {
    const chatList = chats.filter((x) => {
      return x.id !== id;
    });
    dispatch({ field: 'chats', value: chatList });
    dispatch({ field: 'selectChat', value: undefined });
    dispatch({ field: 'selectMessageLastId', value: '' });
    dispatch({ field: 'currentMessages', value: [] });
    dispatch({ field: 'selectMessages', value: [] });
    dispatch({ field: 'chatError', value: false });
    dispatch({
      field: 'selectModel',
      value: calcSelectModel(chats, models),
    });
    dispatch({ field: 'userModelConfig', value: {} });
  };

  const handleUpdateSettings = <K extends keyof Settings>(
    key: K,
    value: Settings[K],
  ) => {
    settings[key] = value;
    dispatch({ field: 'settings', value: settings });
    saveSettings(settings);
  };

  const hasModel = () => {
    return models?.length > 0;
  };

  const selectChat = (
    chatList: ChatResult[],
    chatId: string | null,
    models: AdminModelDto[],
  ) => {
    const chat = chatList.find((x) => x.id === chatId);
    if (chat) {
      dispatch({ field: 'selectChat', value: chat });

      getUserMessages(chat.id).then((data) => {
        if (data.length > 0) {
          dispatch({ field: 'currentMessages', value: data });
          const lastMessage = data[data.length - 1];
          const selectMessageList = getSelectMessages(data, lastMessage.id);
          if (lastMessage.role !== 'assistant') {
            dispatch({
              field: 'chatError',
              value: true,
            });
            selectMessageList.push(chatErrorMessage(lastMessage.id));
          }
          dispatch({
            field: 'selectMessages',
            value: selectMessageList,
          });
          dispatch({ field: 'selectMessageLastId', value: lastMessage.id });
        } else {
          dispatch({ field: 'currentMessages', value: [] });
          dispatch({
            field: 'selectMessages',
            value: [],
          });
        }
        const model =
          getChatModel(chatList, chat?.id, models) ||
          calcSelectModel(chatList, models);
        handleSelectModel(model!);
      });
    }
  };

  const getChats = (params: GetChatsParams, modelList?: AdminModelDto[]) => {
    const { page, pageSize } = params;
    getChatsByPaging(params).then((data) => {
      const { rows, count } = data;
      dispatch({
        field: 'chatsPaging',
        value: { count, page, pageSize },
      });
      let chatList = rows;
      if (!modelList) {
        chatList = rows.concat(chats);
      }
      dispatch({ field: 'chats', value: chatList });
      if (modelList) {
        const selectChatId = getPathChatId(router.asPath) || getSelectChatId();
        selectChat(rows, selectChatId, modelList || models);
      }
    });
  };

  useEffect(() => {
    const settings = getSettings();

    dispatch({
      field: 'settings',
      value: settings,
    });
  }, []);

  useEffect(() => {
    const session = getUserInfo();
    const sessionId = getUserSession();
    if (session && sessionId) {
      setTimeout(() => {
        dispatch({ field: 'user', value: session });
      }, 1000);
    } else {
      router.push(getLoginUrl(getSettingsLanguage()));
    }
    if (sessionId) {
      getUserModels().then((modelData) => {
        dispatch({ field: 'models', value: modelData });
        if (modelData && modelData.length > 0) {
          const selectModelId = getStorageModelId();
          const model = modelData.find((x) => x.modelId.toString() === selectModelId) ?? modelData[0];
          if (model) {
            setStorageModelId(model.modelId);
            handleSelectModel(model);
          }
        }

        getChats({ page: 1, pageSize: 50 }, modelData);
      });

      getUserPromptBrief().then((data) => {
        dispatch({ field: 'prompts', value: data });
      });
    }
  }, []);

  useEffect(() => {
    const handlePopState = (event: PopStateEvent) => {
      const chatId = getPathChatId(event.state.as);
      selectChat(chats, chatId, models);
    };

    window.addEventListener('popstate', handlePopState);

    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, [chats]);

  return (
    <HomeContext.Provider
      value={{
        ...contextValue,
        handleNewChat,
        handleSelectChat,
        handleUpdateChat,
        handleDeleteChat,
        handleSelectModel,
        handleUpdateSelectMessage,
        handleUpdateCurrentMessage,
        handleUpdateUserModelConfig,
        handleUpdateSettings,
        hasModel,
        getChats,
      }}
    >
      <Head>
        <title>Chats</title>
        <meta name="description" content="" />
        <meta
          name="viewport"
          content="height=device-height ,width=device-width, initial-scale=1, user-scalable=no"
        />
        <link rel="icon" href="/favicon.ico" />
      </Head>
      <main>
        {!user && (
          <div
            className={`fixed top-0 left-0 bottom-0 right-0 bg-white dark:bg-[#202123] text-black/80 dark:text-white/80 z-50 text-center text-[12.5px]`}
          >
            <div className="fixed w-screen h-screen top-1/2">
              <div className="flex justify-center">
                <Spinner className="text-gray-500 dark:text-gray-50" />
              </div>
            </div>
          </div>
        )}
        <div className={`flex h-screen w-screen flex-col text-sm`}>
          <div className="flex h-full w-full dark:bg-[#262630]">
            <Chatbar />
            <div className="flex w-full">
              <Chat stopConversationRef={stopConversationRef} />
            </div>
            <PromptBar />
            <ChatSettingsBar />
          </div>
        </div>
      </main>
    </HomeContext.Provider>
  );
};

export default Home;