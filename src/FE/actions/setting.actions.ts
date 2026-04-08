import {
  clampChatbarWidth,
  getSettings,
  saveSettings,
} from '@/utils/settings';
import {
  SettingActionTypes,
  SettingsAction,
} from '@/reducers/setting.reducer';

export const setShowChatBar = (showChatBar: boolean): SettingsAction => {
  // 保存设置到 localStorage
  const currentSettings = getSettings();
  saveSettings({ ...currentSettings, showChatBar });
  
  return {
    type: SettingActionTypes.SHOW_CHAT_BAR,
    payload: showChatBar,
  };
};

export const setChatBarWidth = (
  chatBarWidth: number,
  persist: boolean = true,
): SettingsAction => {
  const nextWidth = clampChatbarWidth(chatBarWidth);

  if (persist) {
    const currentSettings = getSettings();
    saveSettings({ ...currentSettings, chatBarWidth: nextWidth });
  }

  return {
    type: SettingActionTypes.CHAT_BAR_WIDTH,
    payload: nextWidth,
  };
};

export const setShowChatInput = (showChatInput: boolean): SettingsAction => ({
  type: SettingActionTypes.SHOW_CHAT_INPUT,
  payload: showChatInput,
});

