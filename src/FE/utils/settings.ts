import { isMobile } from './common';

const STORAGE_KEY = 'settings';

export interface Settings {
  showChatBar: boolean;
}

export const DEFAULT_SETTINGS = {
  showChatBar: typeof window !== 'undefined'  && isMobile() ? false : true,
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
