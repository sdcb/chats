import { useEffect, useState } from 'react';

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
const listeners = new Set<(lang: string) => void>();

const useTranslation = () => {
  const [language, setLanguage_] = useState(globalLanguage);

  useEffect(() => {
    const handleLanguageChange = (newLang: string) => {
      setLanguage_(newLang);
    };

    listeners.add(handleLanguageChange);

    return () => {
      listeners.delete(handleLanguageChange);
    };
  }, []);

  function t(message: string, params = {}) {
    const translations = TRANSLATIONS[language as keyof typeof TRANSLATIONS];
    let msg = (translations as any)[message] || message;

    Object.keys(params).forEach((k) => {
      const key = k as keyof typeof params;
      msg = msg?.replaceAll(`{{${key}}}`, params[key]);
    });
    return msg;
  }

  const changeLanguage = (newLang: string) => {
    if (SUPPORTED_LANGUAGES.includes(newLang)) {
      globalLanguage = newLang;
      setLanguage(newLang);
      listeners.forEach((listener) => listener(newLang));
    }
  };

  return {
    t,
    language,
    changeLanguage,
    supportedLanguages: SUPPORTED_LANGUAGES,
  };
};

export default useTranslation;
