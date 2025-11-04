import { Dispatch, createContext } from 'react';

import { ActionType } from '@/hooks/useCreateReducer';

import { CHATS_SELECT_TYPE, IChat, IChatPaging } from '@/types/chat';
import { GetChatsParams } from '@/types/clientApis';
import { IChatGroup } from '@/types/group';
import { getSettings } from '@/utils/settings';

import {
  ChatAction,
} from '@/reducers/chat.reducer';
import {
  MessageAction,
} from '@/reducers/message.reducer';
import {
  ModelAction,
} from '@/reducers/model.reducer';
import {
  PromptAction,
} from '@/reducers/prompt.reducer';
import { SettingsAction } from '@/reducers/setting.reducer';
import { AdminModelDto } from '@/types/adminApis';
import { IChatMessage } from '@/types/chatMessage';
import { Prompt, PromptSlim } from '@/types/prompt';

export interface HandleUpdateChatParams {
  isShared?: boolean;
  title?: string;
  chatModelId?: string;
}

export interface HomeInitialState {
  messages: IChatMessage[];
  selectedMessages: IChatMessage[][];

  chats: IChat[];
  chatGroups: IChatGroup[];
  selectedChatId: string | undefined;
  chatPaging: IChatPaging[];
  isChatsLoading: boolean;
  isMessagesLoading: boolean;
  chatsSelectType: CHATS_SELECT_TYPE;

  models: AdminModelDto[];
  modelMap: Record<string, AdminModelDto>;

  defaultPrompt: Prompt | null;
  prompts: PromptSlim[];

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
  isMessagesLoading: false,
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
