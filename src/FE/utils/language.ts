export const DEFAULT_LANGUAGE = 'zh-CN';
export const SUPPORTED_LANGUAGES = ['zh-CN', 'en'];

export const setLanguage = (lang: string) => {
  if (typeof window !== 'undefined') {
    localStorage.setItem('userLanguage', lang);
  }
};

export const getLanguage = () => {
  if (typeof window !== 'undefined') {
    const userLanguage = localStorage.getItem('userLanguage');
    if (userLanguage && SUPPORTED_LANGUAGES.includes(userLanguage)) {
      return userLanguage;
    }
  }
  
  if (typeof navigator !== 'undefined') {
    const browserLang = navigator?.language;
    if (browserLang && SUPPORTED_LANGUAGES.includes(browserLang)) {
      return browserLang;
    }
  }
  
  return DEFAULT_LANGUAGE;
};
