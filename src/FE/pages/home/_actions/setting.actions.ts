import {
  SettingActionTypes,
  SettingsAction,
} from '../_reducers/setting.reducer';

export const setShowChatBar = (showChatBar: boolean): SettingsAction => ({
  type: SettingActionTypes.SHOW_CHAT_BAR,
  payload: showChatBar,
});

export default function () {}
