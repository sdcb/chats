import { AdminModelDto } from '@/types/adminApis';
import { CHATS_SELECT_TYPE, IChat } from '@/types/chat';
import { ChatSpanDto } from '@/types/clientApis';

import {
  ChatAction,
  ChatActionTypes,
  SetChatGroupType,
  SetChatsPagingType,
  SetChatsType,
  SetIsChatsLoadingType,
  SetSelectedChatType,
  SetStopIdsType,
} from '@/reducers/chat.reducer';

export const setChats = (chats: SetChatsType): ChatAction => ({
  type: ChatActionTypes.SET_CHATS,
  payload: chats,
});

export const setSelectedChat = (chat?: SetSelectedChatType): ChatAction => {
  return {
    type: ChatActionTypes.SET_SELECTED_CHAT,
    payload: chat,
  };
};

export const setChangeSelectedChatSpan = (
  chat: IChat,
  span: ChatSpanDto,
  model: AdminModelDto,
): ChatAction => {
  chat.spans = chat.spans.map((s) => {
    if (s.spanId === span.spanId) {
      s = {
        ...s,
        ...span,
        modelId: model.modelId,
        modelName: model.name,
        modelProviderId: model.modelProviderId,
      };
    }
    return s;
  });
  return {
    type: ChatActionTypes.SET_SELECTED_CHAT,
    payload: chat,
  };
};

export const setChatPaging = (paging: SetChatsPagingType): ChatAction => ({
  type: ChatActionTypes.SET_CHAT_PAGING,
  payload: paging,
});

export const setIsChatsLoading = (
  isChatsLoading: SetIsChatsLoadingType,
): ChatAction => ({
  type: ChatActionTypes.SET_IS_CHATS_LOADING,
  payload: isChatsLoading,
});

export const setStopIds = (stopIds: SetStopIdsType): ChatAction => ({
  type: ChatActionTypes.SET_STOP_IDS,
  payload: stopIds,
});

export const setChatGroup = (group: SetChatGroupType): ChatAction => ({
  type: ChatActionTypes.SET_CHAT_GROUP,
  payload: group,
});

export const setChatsSelectType = (type: CHATS_SELECT_TYPE): ChatAction => ({
  type: ChatActionTypes.SET_IS_CHATS_DELETE_MODE,
  payload: type,
});

export default function () {}
