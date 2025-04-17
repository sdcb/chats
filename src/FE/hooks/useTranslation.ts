import {
  SUPPORTED_LANGUAGES,
  getLanguage,
  setLanguage,
} from '@/utils/language';

import en from '../locales/en.json';
import zhCN from '../locales/zh-CN.json';

const TRANSLATIONS = {
  'zh-CN': zhCN,
  en: en,
};

let globalLanguage = getLanguage();

let globalForceUpdate: (() => void) | null = null;

const useTranslation = () => {
  function t(message: string, params = {}) {
    const translations = TRANSLATIONS[globalLanguage as keyof typeof TRANSLATIONS];
    let msg = (translations as any)[message] || message;

    Object.keys(params).forEach((k) => {
      const key = k as keyof typeof params;
      msg = msg?.replaceAll(`{{${key}}}`, params[key]);
    });
    return msg;
  }

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
