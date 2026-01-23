import { isMobile } from './common';

const STORAGE_KEY = 'settings';

export const DEFAULT_FONT_SIZE = 14;
export const MIN_FONT_SIZE = 12;
export const MAX_FONT_SIZE = 18;

export interface Settings {
  showChatBar: boolean;
  fontSize: number;
  hideChatBackground: boolean;
  hideInputAfterSend: boolean;
}

export const DEFAULT_SETTINGS: Settings = {
  showChatBar: typeof window !== 'undefined'  && isMobile() ? false : true,
  fontSize: DEFAULT_FONT_SIZE,
  hideChatBackground: false,
  hideInputAfterSend: false,
};

export const getSettings = (): Settings => {
  let settings = DEFAULT_SETTINGS;
  if (typeof localStorage === 'undefined') return settings;
  const settingsJson = localStorage.getItem(STORAGE_KEY);
  if (settingsJson) {
    let savedSettings = JSON.parse(settingsJson) as Settings;
    settings = Object.assign(settings, savedSettings);
  }
  return settings;
};

export const saveSettings = (value: Settings) => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
};
