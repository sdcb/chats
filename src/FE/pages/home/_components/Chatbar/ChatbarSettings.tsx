import { useContext, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { clearUserInfo, clearUserSession, getLoginUrl } from '@/utils/user';

import { UserRole } from '@/types/adminApis';

import {
  IconBulbFilled,
  IconKey,
  IconLogout,
  IconMoneybag,
  IconPasswordUser,
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

import {
  setShowPromptBar,
  setShowSetting,
} from '../../_actions/setting.actions';
import HomeContext from '../../_contexts/home.context';
import ChangePasswordModal from '../Modal/ChangePasswordModal';
import UserBalanceModal from '../Modal/UserBalanceModal';
import SettingModal from '../Settings/SettingModal';
import SidebarButton from '../Sidebar/SidebarButton';

import { getUserBalanceOnly } from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

const ChatBarSettings = () => {
  const router = useRouter();
  const { t } = useTranslation();
  const [changePwdModalOpen, setChangePwdModalOpen] = useState<boolean>(false);
  const [userBalanceModalOpen, setUserBalanceModalOpen] =
    useState<boolean>(false);
  const [settingSheetOpen, setSettingSheetOpen] = useState<boolean>(false);

  const {
    state: { showPromptBar },
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
      {user?.role === UserRole.admin && (
        <SidebarButton
          text={t('Admin Panel')}
          icon={<IconSettingsCog />}
          onClick={() => {
            router.push('/admin');
          }}
        />
      )}
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
            <SidebarButton
              text={`${t('Account balance')}￥${(+(userBalance || 0)).toFixed(
                2,
              )}`}
              icon={<IconMoneybag />}
              onClick={() => {
                setUserBalanceModalOpen(true);
              }}
            />
            <Separator className="my-2" />
            <SidebarButton
              text={t('Prompt Management')}
              icon={<IconBulbFilled />}
              onClick={() => {
                settingDispatch(setShowPromptBar(!showPromptBar));
              }}
            />
            <SidebarButton
              text={`${t('API Key Management')}`}
              icon={<IconKey />}
              onClick={() => {
                setSettingSheetOpen(true);
              }}
            />
            <SidebarButton
              text={t('Change Password')}
              icon={<IconPasswordUser />}
              onClick={() => {
                setChangePwdModalOpen(true);
              }}
            />
            <SidebarButton
              text={t('Settings')}
              icon={<IconSettings />}
              onClick={() => {
                settingDispatch(setShowSetting(true));
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

      {userBalanceModalOpen && (
        <UserBalanceModal
          isOpen={userBalanceModalOpen}
          onClose={() => {
            setUserBalanceModalOpen(false);
          }}
        />
      )}

      {changePwdModalOpen && (
        <ChangePasswordModal
          isOpen={changePwdModalOpen}
          onClose={() => {
            setChangePwdModalOpen(false);
          }}
        />
      )}

      <SettingModal
        isOpen={settingSheetOpen}
        onClose={() => {
          setSettingSheetOpen(false);
        }}
      />
    </div>
  );
};
export default ChatBarSettings;
