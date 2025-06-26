import { IChatMessage } from '@/types/chatMessage';

interface MessageInitialState {
  messages: IChatMessage[];
  selectedMessages: IChatMessage[][];
}

export const messageInitialState: MessageInitialState = {
  messages: [],
  selectedMessages: [],
};

export enum MessageActionTypes {
  SET_MESSAGES = 'SET_MESSAGES',
  SET_SELECTED_MESSAGES = 'SET_SELECTED_MESSAGES',
}

export type MessageAction =
  | {
      type: MessageActionTypes.SET_MESSAGES;
      payload: IChatMessage[];
    }
  | {
      type: MessageActionTypes.SET_SELECTED_MESSAGES;
      payload: IChatMessage[][];
    };

export default function messageReducer(
  state: MessageInitialState,
  action: MessageAction,
): MessageInitialState {
  switch (action.type) {
    case MessageActionTypes.SET_MESSAGES:
      return { ...state, messages: action.payload };
    case MessageActionTypes.SET_SELECTED_MESSAGES:
      return { ...state, selectedMessages: action.payload };
    default:
      return state;
  }
}
