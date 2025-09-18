import { getSettings } from '@/utils/settings';

interface SettingInitialState {
  showChatBar: boolean;
  showChatInput: boolean;
}

export const settingInitialState: SettingInitialState = {
  showChatBar: getSettings().showChatBar,
  showChatInput: true,
};

export enum SettingActionTypes {
  SHOW_CHAT_BAR = 'SHOW_CHAT_BAR',
  SHOW_CHAT_INPUT = 'SHOW_CHAT_INPUT',
}

export type SettingsAction =
  | { type: SettingActionTypes.SHOW_CHAT_BAR; payload: boolean }
  | { type: SettingActionTypes.SHOW_CHAT_INPUT; payload: boolean };

export default function settingReducer(
  state: SettingInitialState,
  action: SettingsAction,
): SettingInitialState {
  switch (action.type) {
    case SettingActionTypes.SHOW_CHAT_BAR:
      return { ...state, showChatBar: action.payload };
    case SettingActionTypes.SHOW_CHAT_INPUT:
      return { ...state, showChatInput: action.payload };
    default:
      return state;
  }
}
