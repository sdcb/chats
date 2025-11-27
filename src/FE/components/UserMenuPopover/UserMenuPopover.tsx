import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { clearUserInfo, clearUserSession, getLoginUrl } from '@/utils/user';
import { clearChatCache } from '@/utils/chatCache';

import { UserRole } from '@/types/adminApis';

import {
  IconCode,
  IconLogout,
  IconMessage,
  IconSettings,
  IconUserCog,
} from '@/components/Icons/index';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Separator } from '@/components/ui/separator';

import SidebarButton from '../Sidebar/SidebarButton';
import SidebarLink from '../Sidebar/SidebarLink';

import { useUserInfo } from '@/providers/UserProvider';

export enum PageType {
  Chat = 'chat',
  Admin = 'admin',
  Build = 'build',
}

interface UserMenuPopoverProps {
  pageType: PageType;
  trigger: React.ReactNode;
  onOpen?: () => void;
}

const UserMenuPopover = ({
  pageType,
  trigger,
  onOpen,
}: UserMenuPopoverProps) => {
  const router = useRouter();
  const { t } = useTranslation();
  const user = useUserInfo();

  const logout = () => {
    clearUserSession();
    clearUserInfo();
    clearChatCache();
    router.push(getLoginUrl());
  };

  const handleOpenChange = (open: boolean) => {
    if (open && onOpen) {
      onOpen();
    }
  };

  return (
    <Popover onOpenChange={handleOpenChange}>
      <PopoverTrigger className="w-full hover:bg-muted rounded-md">
        {trigger}
      </PopoverTrigger>
      <PopoverContent
        side="top"
        align="start"
        sideOffset={8}
        className="w-[200px] p-2 data-[state=open]:animate-slide-up-in data-[state=closed]:animate-slide-up-out"
      >
        {pageType !== PageType.Chat && (
          <SidebarLink
            text={t('Back to Chat')}
            href="/"
            icon={<IconMessage />}
            onClick={(e) => {
              e.preventDefault();
              router.push('/');
            }}
          />
        )}
        {pageType !== PageType.Admin && user?.role === UserRole.admin && (
          <SidebarLink
            text={t('Admin Panel')}
            href="/admin/dashboard"
            icon={<IconUserCog />}
            onClick={(e) => {
              e.preventDefault();
              router.push('/admin/dashboard');
            }}
          />
        )}
        {pageType !== PageType.Build && (
          <SidebarLink
            text="API"
            href="/build/api-key"
            icon={<IconCode />}
            onClick={(e) => {
              e.preventDefault();
              router.push('/build/api-key');
            }}
          />
        )}
        <SidebarLink
          text={t('User Settings')}
          href="/settings"
          icon={<IconSettings />}
          onClick={(e) => {
            e.preventDefault();
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
  );
};

export default UserMenuPopover;
