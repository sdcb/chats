import { getSettings } from '@/utils/settings';

interface SettingInitialState {
  showChatBar: boolean;
  showChatInput: boolean;
  chatBarWidth: number;
}

export const settingInitialState: SettingInitialState = {
  showChatBar: getSettings().showChatBar,
  showChatInput: true,
  chatBarWidth: getSettings().chatBarWidth,
};

export enum SettingActionTypes {
  SHOW_CHAT_BAR = 'SHOW_CHAT_BAR',
  SHOW_CHAT_INPUT = 'SHOW_CHAT_INPUT',
  CHAT_BAR_WIDTH = 'CHAT_BAR_WIDTH',
}

export type SettingsAction =
  | { type: SettingActionTypes.SHOW_CHAT_BAR; payload: boolean }
  | { type: SettingActionTypes.SHOW_CHAT_INPUT; payload: boolean }
  | { type: SettingActionTypes.CHAT_BAR_WIDTH; payload: number };

export default function settingReducer(
  state: SettingInitialState,
  action: SettingsAction,
): SettingInitialState {
  switch (action.type) {
    case SettingActionTypes.SHOW_CHAT_BAR:
      return { ...state, showChatBar: action.payload };
    case SettingActionTypes.SHOW_CHAT_INPUT:
      return { ...state, showChatInput: action.payload };
    case SettingActionTypes.CHAT_BAR_WIDTH:
      return { ...state, chatBarWidth: action.payload };
    default:
      return state;
  }
}
