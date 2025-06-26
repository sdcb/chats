import { IChatMessage } from '@/types/chatMessage';
import {
  MessageAction,
  MessageActionTypes,
} from '../_reducers/message.reducer';

export const setMessages = (messages: IChatMessage[]): MessageAction => ({
  type: MessageActionTypes.SET_MESSAGES,
  payload: messages,
});

export const setSelectedMessages = (
  selectedMessages: IChatMessage[][],
): MessageAction => ({
  type: MessageActionTypes.SET_SELECTED_MESSAGES,
  payload: selectedMessages,
});

export default function () {}
