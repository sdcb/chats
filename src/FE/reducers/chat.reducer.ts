import { CHATS_SELECT_TYPE, IChat, IChatPaging } from '@/types/chat';
import { IChatGroup } from '@/types/group';

interface ChatInitialState {
  chats: IChat[];
  selectedChatId?: string;
  chatPaging: IChatPaging[];
  isChatsLoading: boolean;
  isMessagesLoading: boolean;
  stopIds: string[];
  chatGroups: IChatGroup[];
  chatsSelectType: CHATS_SELECT_TYPE;
}

export const chatInitialState: ChatInitialState = {
  chats: [],
  selectedChatId: undefined,
  chatPaging: [],
  isChatsLoading: false,
  isMessagesLoading: false,
  stopIds: [],
  chatGroups: [],
  chatsSelectType: CHATS_SELECT_TYPE.NONE,
};

export enum ChatActionTypes {
  SET_CHATS = 'SET_CHATS',
  SET_CHAT_GROUPS = 'SET_CHAT_GROUPS',
  SET_CHATS_INCR = 'SET_CHATS_INCR',
  SET_SELECTED_CHAT_ID = 'SET_SELECTED_CHAT_ID',
  SET_CHAT_PAGING = 'SET_CHAT_PAGING',
  SET_IS_CHATS_LOADING = 'SET_IS_CHATS_LOADING',
  SET_IS_MESSAGES_LOADING = 'SET_IS_MESSAGES_LOADING',
  SET_STOP_IDS = 'SET_STOP_IDS',
  SET_CHAT_GROUP = 'SET_CHAT_GROUP',
  SET_IS_CHATS_DELETE_MODE = 'SET_IS_CHATS_DELETE_MODE',
}

export type ChatAction =
  | { type: ChatActionTypes.SET_CHATS; payload: IChat[] }
  | { type: ChatActionTypes.SET_CHATS_INCR; payload: IChat[] }
  | { type: ChatActionTypes.SET_SELECTED_CHAT_ID; payload: string | undefined }
  | { type: ChatActionTypes.SET_CHAT_PAGING; payload: IChatPaging[] }
  | {
      type: ChatActionTypes.SET_IS_CHATS_LOADING;
      payload: boolean;
    }
  | {
      type: ChatActionTypes.SET_IS_MESSAGES_LOADING;
      payload: boolean;
    }
  | { type: ChatActionTypes.SET_STOP_IDS; payload: string[] }
  | { type: ChatActionTypes.SET_CHAT_GROUP; payload: IChatGroup[] }
  | {
      type: ChatActionTypes.SET_IS_CHATS_DELETE_MODE;
      payload: CHATS_SELECT_TYPE;
    };

export default function chatReducer(
  state: ChatInitialState,
  action: ChatAction,
): ChatInitialState {
  switch (action.type) {
    case ChatActionTypes.SET_CHATS:
      return { ...state, chats: action.payload };
    case ChatActionTypes.SET_SELECTED_CHAT_ID:
      return { ...state, selectedChatId: action.payload };
    case ChatActionTypes.SET_CHAT_PAGING:
      return { ...state, chatPaging: action.payload };
    case ChatActionTypes.SET_IS_CHATS_LOADING:
      return { ...state, isChatsLoading: action.payload };
    case ChatActionTypes.SET_IS_MESSAGES_LOADING:
      return { ...state, isMessagesLoading: action.payload };
    case ChatActionTypes.SET_STOP_IDS:
      return { ...state, stopIds: action.payload };
    case ChatActionTypes.SET_CHAT_GROUP:
      return { ...state, chatGroups: action.payload };
    case ChatActionTypes.SET_IS_CHATS_DELETE_MODE:
      return { ...state, chatsSelectType: action.payload };
    default:
      return state;
  }
}
