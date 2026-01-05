import {
  SUPPORTED_LANGUAGES,
  getLanguage,
  setLanguage,
} from '@/utils/language';

import { useCallback } from 'react';

import zhCN from '../locales/zh-CN.json';

const TRANSLATIONS = {
  'zh-CN': zhCN,
};

let globalLanguage = getLanguage();

let globalForceUpdate: (() => void) | null = null;

export const translate = (message: string, params: Record<string, any> = {}) => {
  if (globalLanguage === 'en') {
    let msg = message;
    Object.keys(params).forEach((k) => {
      const key = k as keyof typeof params;
      msg = msg?.replaceAll(`{{${key}}}`, params[key]);
    });
    return msg;
  }

  const translations = TRANSLATIONS[globalLanguage as keyof typeof TRANSLATIONS];
  let msg = (translations as any)?.[message] || message;

  Object.keys(params).forEach((k) => {
    const key = k as keyof typeof params;
    msg = msg?.replaceAll(`{{${key}}}`, params[key]);
  });
  return msg;
};

const useTranslation = () => {
  const t = useCallback(
    (message: string, params = {}) => translate(message, params),
    [],
  );

  const setForceUpdate = (forceUpdate: () => void) => {
    globalForceUpdate = forceUpdate;
  };

  const changeLanguage = (newLang: string) => {
    if (SUPPORTED_LANGUAGES.includes(newLang)) {
      globalLanguage = newLang;
      setLanguage(newLang);
      if (globalForceUpdate) {
        globalForceUpdate();
      }
      window.location.reload();
    }
  };

  return {
    t,
    language: globalLanguage,
    changeLanguage,
    setForceUpdate,
    supportedLanguages: SUPPORTED_LANGUAGES,
  };
};

export default useTranslation;
