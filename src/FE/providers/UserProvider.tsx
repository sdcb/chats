'use client';

import React, { createContext, useContext, useState } from 'react';

import { UserInfo, getUserInfo, redirectToLoginPage } from '@/utils/user';

const UserContext = createContext<UserInfo | null>(null);

export const UserProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user] = useState<UserInfo | null>(() => {
    const userInfo = getUserInfo();
    if (!userInfo) {
      redirectToLoginPage();
      return null;
    }
    return userInfo;
  });

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};

export const useUserInfo = () => {
  const user = useContext(UserContext) || getUserInfo();
  if (!user) {
    redirectToLoginPage();
    return;
  }
  return user;
};
