import { CHATS_SELECT_TYPE, IChat, IChatPaging } from '@/types/chat';
import { SteeringDto } from '@/types/chatMessage';
import { IChatGroup } from '@/types/group';

export interface ActiveRunState {
  chatId: string;
  runId: string;
  /** steering is only supported for single-span runs */
  steerable: boolean;
}

interface ChatInitialState {
  chats: IChat[];
  selectedChatId?: string;
  chatPaging: IChatPaging[];
  isChatsLoading: boolean;
  isMessagesLoading: boolean;
  stopIds: string[];
  activeRun: ActiveRunState | null;
  steerings: SteeringDto[];
  /** discarded steering text to put back into the input box */
  steeringRefill: string | null;
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
  activeRun: null,
  steerings: [],
  steeringRefill: null,
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
  SET_ACTIVE_RUN = 'SET_ACTIVE_RUN',
  SET_STEERINGS = 'SET_STEERINGS',
  SET_STEERING_REFILL = 'SET_STEERING_REFILL',
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
  | { type: ChatActionTypes.SET_ACTIVE_RUN; payload: ActiveRunState | null }
  | {
      type: ChatActionTypes.SET_STEERINGS;
      payload: SteeringDto[] | ((prev: SteeringDto[]) => SteeringDto[]);
    }
  | { type: ChatActionTypes.SET_STEERING_REFILL; payload: string | null }
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
    case ChatActionTypes.SET_ACTIVE_RUN:
      return { ...state, activeRun: action.payload };
    case ChatActionTypes.SET_STEERINGS:
      return {
        ...state,
        steerings:
          typeof action.payload === 'function'
            ? action.payload(state.steerings)
            : action.payload,
      };
    case ChatActionTypes.SET_STEERING_REFILL:
      return { ...state, steeringRefill: action.payload };
    case ChatActionTypes.SET_CHAT_GROUP:
      return { ...state, chatGroups: action.payload };
    case ChatActionTypes.SET_IS_CHATS_DELETE_MODE:
      return { ...state, chatsSelectType: action.payload };
    default:
      return state;
  }
}
