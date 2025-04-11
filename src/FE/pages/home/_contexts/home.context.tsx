import { Dispatch, createContext } from 'react';

import { ActionType } from '@/hooks/useCreateReducer';

import { CHATS_SELECT_TYPE, IChat } from '@/types/chat';
import { GetChatsParams } from '@/types/clientApis';

import {
  ChatAction,
  SetChatGroupType,
  SetChatsPagingType,
  SetChatsType,
} from '../_reducers/chat.reducer';
import {
  MessageAction,
  SetMessagesType,
  SetSelectedMessagesType,
} from '../_reducers/message.reducer';
import {
  ModelAction,
  SetModelMapType,
  SetModelsType,
} from '../_reducers/model.reducer';
import {
  PromptAction,
  SetDefaultPromptType,
  SetPromptsType,
} from '../_reducers/prompt.reducer';
import { SettingsAction } from '../_reducers/setting.reducer';

export interface HandleUpdateChatParams {
  isShared?: boolean;
  title?: string;
  chatModelId?: string;
}

export interface HomeInitialState {
  messages: SetMessagesType;
  selectedMessages: SetSelectedMessagesType;

  chats: SetChatsType;
  chatGroups: SetChatGroupType;
  selectedChat: IChat;
  chatPaging: SetChatsPagingType;
  isChatsLoading: boolean;
  chatsSelectType: CHATS_SELECT_TYPE;

  models: SetModelsType;
  modelMap: SetModelMapType;

  defaultPrompt: SetDefaultPromptType | null;
  prompts: SetPromptsType;

  showChatBar: boolean;
  showSetting: boolean;
}

export const initialState: HomeInitialState = {
  messages: [],
  selectedMessages: [],

  chats: [],
  chatGroups: [],
  selectedChat: {} as IChat,
  chatPaging: [],
  isChatsLoading: false,
  chatsSelectType: CHATS_SELECT_TYPE.NONE,

  models: [],
  modelMap: {},

  defaultPrompt: null,
  prompts: [],

  showChatBar: true,
  showSetting: false,
};

export interface HomeContextProps {
  state: HomeInitialState;
  dispatch: Dispatch<ActionType<HomeInitialState>>;

  chatDispatch: Dispatch<ChatAction>;
  messageDispatch: Dispatch<MessageAction>;
  modelDispatch: Dispatch<ModelAction>;
  settingDispatch: Dispatch<SettingsAction>;
  promptDispatch: Dispatch<PromptAction>;

  hasModel: () => boolean;
  handleNewChat: (groupId?: string | null) => void;
  handleDeleteChat: (ids: string[]) => void;
  handleSelectChat: (chat: IChat) => void;
  handleUpdateChat: (
    chats: IChat[],
    id: string,
    params: HandleUpdateChatParams,
  ) => void;
  getChats: (query: string) => void;
  getChatsByGroup: (params: GetChatsParams) => void;
  handleStopChats: () => void;
}

const HomeContext = createContext<HomeContextProps>(undefined!);

export default HomeContext;
