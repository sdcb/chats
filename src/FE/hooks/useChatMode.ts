import { useCallback, useEffect, useState } from 'react';

import { ChatRunMode } from '@/types/chatMessage';

const STORAGE_PREFIX = 'chats.mode.';
const CHANGE_EVENT = 'chats-mode-change';

export function getChatMode(chatId: string | undefined): ChatRunMode {
  if (!chatId || typeof window === 'undefined') return 'normal';
  try {
    return window.localStorage.getItem(STORAGE_PREFIX + chatId) === 'plan' ? 'plan' : 'normal';
  } catch {
    return 'normal';
  }
}

export function setChatMode(chatId: string, mode: ChatRunMode) {
  try {
    if (mode === 'normal') window.localStorage.removeItem(STORAGE_PREFIX + chatId);
    else window.localStorage.setItem(STORAGE_PREFIX + chatId, mode);
  } catch {
    // storage unavailable: the mode just won't be remembered
  }
  window.dispatchEvent(new CustomEvent(CHANGE_EVENT, { detail: chatId }));
}

/** Plan/normal mode remembered per chat. */
export function useChatMode(chatId: string | undefined): [ChatRunMode, (mode: ChatRunMode) => void] {
  const [mode, setMode] = useState<ChatRunMode>(() => getChatMode(chatId));

  useEffect(() => {
    setMode(getChatMode(chatId));
    const handler = (e: Event) => {
      if ((e as CustomEvent<string>).detail === chatId) setMode(getChatMode(chatId));
    };
    window.addEventListener(CHANGE_EVENT, handler);
    return () => window.removeEventListener(CHANGE_EVENT, handler);
  }, [chatId]);

  const update = useCallback(
    (next: ChatRunMode) => {
      if (chatId) setChatMode(chatId, next);
    },
    [chatId],
  );

  return [mode, update];
}
