interface SettingInitialState {
  showChatBar: boolean;
}

export const settingInitialState: SettingInitialState = {
  showChatBar: true,
};

export enum SettingActionTypes {
  SHOW_CHAT_BAR = 'SHOW_CHAT_BAR',
}

export type SettingsAction =
  | { type: SettingActionTypes.SHOW_CHAT_BAR; payload: boolean };

export default function settingReducer(
  state: SettingInitialState,
  action: SettingsAction,
): SettingInitialState {
  switch (action.type) {
    case SettingActionTypes.SHOW_CHAT_BAR:
      return { ...state, showChatBar: action.payload };
    default:
      return state;
  }
}
