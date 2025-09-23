import { useContext, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { clearUserInfo, clearUserSession, getLoginUrl } from '@/utils/user';

import { UserRole } from '@/types/adminApis';

import {
  IconLogout,
  IconSettings,
  IconSettingsCog,
  IconUser,
} from '@/components/Icons/index';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Separator } from '@/components/ui/separator';

import HomeContext from '@/contexts/home.context';
import SidebarButton from '../Sidebar/SidebarButton';

import { getUserBalanceOnly } from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

const ChatBarSettings = () => {
  const router = useRouter();
  const { t } = useTranslation();

  const {
    settingDispatch,
  } = useContext(HomeContext);
  const user = useUserInfo();

  const [userBalance, setUserBalance] = useState<number>(0);
  const logout = () => {
    clearUserSession();
    clearUserInfo();
    router.push(getLoginUrl());
  };

  const getUserBalance = () => {
    getUserBalanceOnly().then((data) => setUserBalance(data));
  };

  const handleClickUserMore = () => {
    getUserBalance();
  };

  return (
    <div className="flex flex-col items-center space-y-1 border-t border-black/5 dark:border-white/10 pt-2 text-sm">
      {user?.username && (
        <Popover>
          <PopoverTrigger className="w-full hover:bg-muted rounded-md">
            <SidebarButton
              className="capitalize"
              text={user?.username}
              icon={<IconUser />}
              onClick={handleClickUserMore}
            />
          </PopoverTrigger>
          <PopoverContent className="w-[244px]">
            {user?.role === UserRole.admin && (
              <SidebarButton
                text={t('Admin Panel')}
                icon={<IconSettingsCog />}
                onClick={() => {
                  router.push('/admin/dashboard');
                }}
              />
            )}
            <SidebarButton
              text={t('Settings')}
              icon={<IconSettings />}
              onClick={() => {
                router.push('/settings');
              }}
            />
            <Separator className="my-2" />
            <SidebarButton
              text={t('Log out')}
              icon={<IconLogout />}
              onClick={logout}
            />
          </PopoverContent>
        </Popover>
      )}
    </div>
  );
};
export default ChatBarSettings;
