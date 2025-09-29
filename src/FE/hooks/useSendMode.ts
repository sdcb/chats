import { useState, useEffect } from 'react';

export type SendMode = 'enter' | 'ctrl-enter';

const SEND_MODE_KEY = 'chat-send-mode';

export const useSendMode = () => {
  const [sendMode, setSendMode] = useState<SendMode>('enter');

  useEffect(() => {
    const stored = localStorage.getItem(SEND_MODE_KEY);
    if (stored === 'enter' || stored === 'ctrl-enter') {
      setSendMode(stored);
    }
  }, []);

  const updateSendMode = (mode: SendMode) => {
    setSendMode(mode);
    localStorage.setItem(SEND_MODE_KEY, mode);
  };

  return {
    sendMode,
    updateSendMode,
  };
};