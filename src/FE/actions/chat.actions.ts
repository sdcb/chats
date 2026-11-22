import { AdminModelDto } from '@/types/adminApis';
import { CHATS_SELECT_TYPE, IChat, IChatPaging } from '@/types/chat';
import { ChatSpanDto } from '@/types/clientApis';
import { IChatGroup } from '@/types/group';

import { SteeringDto } from '@/types/chatMessage';

import {
  ActiveRunState,
  ChatAction,
  ChatActionTypes,
} from '@/reducers/chat.reducer';

export const setChats = (chats: IChat[]): ChatAction => ({
  type: ChatActionTypes.SET_CHATS,
  payload: chats,
});

export const setSelectedChatId = (chatId?: string): ChatAction => {
  return {
    type: ChatActionTypes.SET_SELECTED_CHAT_ID,
    payload: chatId,
  };
};

export const setChangeSelectedChatSpan = (
  chats: IChat[],
  chatId: string,
  span: ChatSpanDto,
  model: AdminModelDto,
): ChatAction => {
  const updatedChats = chats.map((chat) => {
    if (chat.id === chatId) {
      const updatedSpans = chat.spans.map((s) => {
        if (s.spanId === span.spanId) {
          return {
            ...s,
            ...span,
            modelId: model.modelId,
            modelName: model.name,
            modelProviderId: model.modelProviderId,
          };
        }
        return s;
      });
      return { ...chat, spans: updatedSpans };
    }
    return chat;
  });
  return {
    type: ChatActionTypes.SET_CHATS,
    payload: updatedChats,
  };
};

export const setChatPaging = (paging: IChatPaging[]): ChatAction => ({
  type: ChatActionTypes.SET_CHAT_PAGING,
  payload: paging,
});

export const setIsChatsLoading = (
  isChatsLoading: boolean,
): ChatAction => ({
  type: ChatActionTypes.SET_IS_CHATS_LOADING,
  payload: isChatsLoading,
});

export const setIsMessagesLoading = (
  isMessagesLoading: boolean,
): ChatAction => ({
  type: ChatActionTypes.SET_IS_MESSAGES_LOADING,
  payload: isMessagesLoading,
});

export const setStopIds = (stopIds: string[]): ChatAction => ({
  type: ChatActionTypes.SET_STOP_IDS,
  payload: stopIds,
});

export const setChatGroup = (group: IChatGroup[]): ChatAction => ({
  type: ChatActionTypes.SET_CHAT_GROUP,
  payload: group,
});

export const setChatsSelectType = (type: CHATS_SELECT_TYPE): ChatAction => ({
  type: ChatActionTypes.SET_IS_CHATS_DELETE_MODE,
  payload: type,
});


export const setActiveRun = (activeRun: ActiveRunState | null): ChatAction => ({
  type: ChatActionTypes.SET_ACTIVE_RUN,
  payload: activeRun,
});

export const setSteerings = (
  steerings: SteeringDto[] | ((prev: SteeringDto[]) => SteeringDto[]),
): ChatAction => ({
  type: ChatActionTypes.SET_STEERINGS,
  payload: steerings,
});

export const setSteeringRefill = (text: string | null): ChatAction => ({
  type: ChatActionTypes.SET_STEERING_REFILL,
  payload: text,
});
