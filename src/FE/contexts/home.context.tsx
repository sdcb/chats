import { Dispatch, createContext } from 'react';

import { ActionType } from '@/hooks/useCreateReducer';

import { CHATS_SELECT_TYPE, IChat } from '@/types/chat';
import { GetChatsParams } from '@/types/clientApis';
import { getSettings } from '@/utils/settings';

import {
  ChatAction,
  SetChatGroupType,
  SetChatsPagingType,
  SetChatsType,
  SetSelectedChatIdType,
} from '@/reducers/chat.reducer';
import {
  MessageAction,
} from '@/reducers/message.reducer';
import {
  ModelAction,
  SetModelMapType,
  SetModelsType,
} from '@/reducers/model.reducer';
import {
  PromptAction,
  SetDefaultPromptType,
  SetPromptsType,
} from '@/reducers/prompt.reducer';
import { SettingsAction } from '@/reducers/setting.reducer';
import { IChatMessage } from '@/types/chatMessage';

export interface HandleUpdateChatParams {
  isShared?: boolean;
  title?: string;
  chatModelId?: string;
}

export interface HomeInitialState {
  messages: IChatMessage[];
  selectedMessages: IChatMessage[][];

  chats: SetChatsType;
  chatGroups: SetChatGroupType;
  selectedChatId: SetSelectedChatIdType;
  chatPaging: SetChatsPagingType;
  isChatsLoading: boolean;
  chatsSelectType: CHATS_SELECT_TYPE;

  models: SetModelsType;
  modelMap: SetModelMapType;

  defaultPrompt: SetDefaultPromptType | null;
  prompts: SetPromptsType;

  showChatBar: boolean;
  showChatInput: boolean;
}

export const initialState: HomeInitialState = {
  messages: [],
  selectedMessages: [],

  chats: [],
  chatGroups: [],
  selectedChatId: undefined,
  chatPaging: [],
  isChatsLoading: false,
  chatsSelectType: CHATS_SELECT_TYPE.NONE,

  models: [],
  modelMap: {},

  defaultPrompt: null,
  prompts: [],

  showChatBar: getSettings().showChatBar,
  showChatInput: true,
};

export interface HomeContextProps {
  state: HomeInitialState;
  dispatch: Dispatch<ActionType<HomeInitialState>>;

  // 计算属性
  selectedChat: IChat | undefined;

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
