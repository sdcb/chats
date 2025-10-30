import { CHATS_SELECT_TYPE, IChat, IChatPaging } from '@/types/chat';
import { IChatGroup } from '@/types/group';

export type SetChatsType = IChat[];
export type SetSelectedChatIdType = string | undefined;
export type SetChatStatusType = boolean;
export type SetChatsPagingType = IChatPaging[];
export type SetIsChatsLoadingType = boolean;
export type SetIsMessagesLoadingType = boolean;
export type SetStopIdsType = string[];
export type SetChatGroupType = IChatGroup[];
export type SetChatsSelectType = CHATS_SELECT_TYPE;

interface ChatInitialState {
  chats: SetChatsType;
  selectedChatId?: SetSelectedChatIdType;
  chatPaging: SetChatsPagingType;
  isChatsLoading: SetIsChatsLoadingType;
  isMessagesLoading: SetIsMessagesLoadingType;
  stopIds: SetStopIdsType;
  chatGroups: SetChatGroupType;
  chatsSelectType: SetChatsSelectType;
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
  | { type: ChatActionTypes.SET_CHATS; payload: SetChatsType }
  | { type: ChatActionTypes.SET_CHATS_INCR; payload: SetChatsType }
  | { type: ChatActionTypes.SET_SELECTED_CHAT_ID; payload: SetSelectedChatIdType }
  | { type: ChatActionTypes.SET_CHAT_PAGING; payload: SetChatsPagingType }
  | {
      type: ChatActionTypes.SET_IS_CHATS_LOADING;
      payload: SetIsChatsLoadingType;
    }
  | {
      type: ChatActionTypes.SET_IS_MESSAGES_LOADING;
      payload: SetIsMessagesLoadingType;
    }
  | { type: ChatActionTypes.SET_STOP_IDS; payload: SetStopIdsType }
  | { type: ChatActionTypes.SET_CHAT_GROUP; payload: SetChatGroupType }
  | {
      type: ChatActionTypes.SET_IS_CHATS_DELETE_MODE;
      payload: SetChatsSelectType;
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
