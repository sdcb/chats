interface SettingInitialState {
  showChatBar: boolean;
  showPromptBar: boolean;
  showSetting: boolean;
}

export const settingInitialState: SettingInitialState = {
  showChatBar: true,
  showPromptBar: false,
  showSetting: false,
};

export enum SettingActionTypes {
  SHOW_CHAT_BAR = 'SHOW_CHAT_BAR',
  SHOW_PROMPT_BAR = 'SHOW_PROMPT_BAR',
  SHOW_SETTING = 'SHOW_SETTING',
}

export type SettingsAction =
  | { type: SettingActionTypes.SHOW_CHAT_BAR; payload: boolean }
  | { type: SettingActionTypes.SHOW_PROMPT_BAR; payload: boolean }
  | { type: SettingActionTypes.SHOW_SETTING; payload: boolean };

export default function settingReducer(
  state: SettingInitialState,
  action: SettingsAction,
): SettingInitialState {
  switch (action.type) {
    case SettingActionTypes.SHOW_CHAT_BAR:
      return { ...state, showChatBar: action.payload };
    case SettingActionTypes.SHOW_PROMPT_BAR:
      return { ...state, showPromptBar: action.payload };
    case SettingActionTypes.SHOW_SETTING:
      return { ...state, showSetting: action.payload };
    default:
      return state;
  }
}
