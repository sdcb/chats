import {
  SettingActionTypes,
  SettingsAction,
} from '../_reducers/setting.reducer';

export const setShowChatBar = (showChatBar: boolean): SettingsAction => ({
  type: SettingActionTypes.SHOW_CHAT_BAR,
  payload: showChatBar,
});

export const setShowChatInput = (showChatInput: boolean): SettingsAction => ({
  type: SettingActionTypes.SHOW_CHAT_INPUT,
  payload: showChatInput,
});

export default function () {}