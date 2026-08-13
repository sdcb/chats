export interface UserInfo {
  username: string;
  role: string;
  apiKeyEnabled: boolean;
}

export const saveUserInfo = (user: UserInfo) => {
  const json = JSON.stringify(user);
  const value = btoa(encodeURIComponent(json));
  localStorage.setItem('user', value);
};
export const clearUserInfo = () => {
  localStorage.removeItem('user');
};

export const getUserInfo = () => {
  try {
    const value = localStorage.getItem('user');
    const user = decodeURIComponent(atob(value || ''));
    if (!user) {
      return null;
    }
    return JSON.parse(user) as UserInfo;
  } catch {
    return null;
  }
};

export const getLoginUrl = () => {
  return '/login';
};

export const redirectToLoginPage = () => {
  location.href = getLoginUrl();
};

export const redirectToHomePage = (ms?: number) => {
  const toHome = () => {
    location.href = '/';
  };

  ms ? setTimeout(toHome, ms) : toHome();
};

export const setUserSession = (sessionId: string) => {
  let expires = new Date();
  expires.setMinutes(expires.getMinutes() + 10080);
  localStorage.setItem(
    'session',
    JSON.stringify({
      sessionId,
      expires: expires.getTime(),
    }),
  );
};

export const getUserSession = (): string => {
  if (typeof localStorage === 'undefined') return '';
  const session = JSON.parse(localStorage.getItem('session') || '{}');
  if (session?.expires && session?.expires > new Date().getTime())
    return session.sessionId;
  return '';
};

export const clearUserSession = () => {
  localStorage.removeItem('session');
};
