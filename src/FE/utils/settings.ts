import { isMobile } from './common';

const STORAGE_KEY = 'settings';

export const DEFAULT_FONT_SIZE = 15;
export const MIN_FONT_SIZE = 12;
export const MAX_FONT_SIZE = 18;
export const DEFAULT_CHATBAR_WIDTH = 280;
export const MIN_CHATBAR_WIDTH = 280;
export const MAX_CHATBAR_WIDTH = 520;

export const getDesktopChatbarMaxWidth = (viewportWidth: number): number => {
  if (!Number.isFinite(viewportWidth) || viewportWidth <= 0) {
    return MIN_CHATBAR_WIDTH;
  }

  return Math.max(
    MIN_CHATBAR_WIDTH,
    Math.min(MAX_CHATBAR_WIDTH, Math.floor(viewportWidth * 0.45)),
  );
};

export const clampChatbarWidth = (
  width: number,
  viewportWidth?: number,
): number => {
  const maxWidth =
    viewportWidth == null
      ? MAX_CHATBAR_WIDTH
      : getDesktopChatbarMaxWidth(viewportWidth);

  return Math.max(
    MIN_CHATBAR_WIDTH,
    Math.min(Number.isFinite(width) ? width : DEFAULT_CHATBAR_WIDTH, maxWidth),
  );
};

export const getEffectiveChatbarWidth = ({
  preferredWidth,
  viewportWidth,
  isMobileView,
  isOpen,
}: {
  preferredWidth: number;
  viewportWidth: number;
  isMobileView: boolean;
  isOpen: boolean;
}): number => {
  if (!isOpen || isMobileView) return 0;

  return clampChatbarWidth(preferredWidth, viewportWidth);
};

export interface Settings {
  showChatBar: boolean;
  fontSize: number;
  chatBarWidth: number;
}

export const DEFAULT_SETTINGS: Settings = {
  showChatBar: typeof window !== 'undefined'  && isMobile() ? false : true,
  fontSize: DEFAULT_FONT_SIZE,
  chatBarWidth: DEFAULT_CHATBAR_WIDTH,
};

export const getSettings = (): Settings => {
  let settings = { ...DEFAULT_SETTINGS };
  if (typeof localStorage === 'undefined') return settings;
  const settingsJson = localStorage.getItem(STORAGE_KEY);
  if (settingsJson) {
    let savedSettings = JSON.parse(settingsJson) as Settings;
    settings = { ...settings, ...savedSettings };
  }
  settings.chatBarWidth = clampChatbarWidth(settings.chatBarWidth);
  return settings;
};

export const saveSettings = (value: Settings) => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
};
