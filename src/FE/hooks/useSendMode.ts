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

    // 监听 storage 事件,当其他组件更新 sendMode 时同步更新
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === SEND_MODE_KEY && e.newValue) {
        if (e.newValue === 'enter' || e.newValue === 'ctrl-enter') {
          setSendMode(e.newValue);
        }
      }
    };

    // 监听自定义事件,用于同一页面内的组件间通信
    const handleCustomEvent = (e: Event) => {
      const customEvent = e as CustomEvent<SendMode>;
      setSendMode(customEvent.detail);
    };

    window.addEventListener('storage', handleStorageChange);
    window.addEventListener('sendModeChanged', handleCustomEvent);

    return () => {
      window.removeEventListener('storage', handleStorageChange);
      window.removeEventListener('sendModeChanged', handleCustomEvent);
    };
  }, []);

  const updateSendMode = (mode: SendMode) => {
    setSendMode(mode);
    localStorage.setItem(SEND_MODE_KEY, mode);
    
    // 触发自定义事件,通知同一页面内的其他组件
    const event = new CustomEvent('sendModeChanged', { detail: mode });
    window.dispatchEvent(event);
  };

  return {
    sendMode,
    updateSendMode,
  };
};