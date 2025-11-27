'use client';

import * as React from 'react';
import { useEffect, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/router';

import { useToast } from '@/hooks/useToast';
import useTranslation from '@/hooks/useTranslation';

import { GetChatVersionResult } from '@/types/clientApis';

import {
  IconChartPie,
  IconFiles,
  IconIdBadge,
  IconLayoutSidebar,
  IconMessages,
  IconMoneybag,
  IconSettings,
  IconSettingsCog,
  IconShieldLock,
  IconUserCog,
  IconUsers,
} from '@/components/Icons/index';
import UserMenuPopover from '@/components/UserMenuPopover/UserMenuPopover';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar';
import { ToastAction } from '@/components/ui/toast';
import { Toaster } from '@/components/ui/toaster';

import { postChatsVersion } from '@/apis/adminApis';
import { useUserInfo } from '@/providers/UserProvider';

interface MenuItem {
  url: string;
  icon: (stroke?: string) => React.ReactNode;
  title: string;
}

const AdminMenu = ({
  menus,
  isActive,
}: {
  menus: MenuItem[];
  isActive: (url: string) => boolean;
}) => {
  const router = useRouter();

  return (
    <SidebarMenu className="px-2.5">
      {menus.map((menu, index) => (
        <SidebarMenuItem key={index}>
          <SidebarMenuButton
            asChild
            isActive={isActive(menu.url)}
            tooltip={menu.title}
            className="flex items-center"
          >
            <Link
              href={menu.url}
              onClick={(e) => {
                e.preventDefault();
                router.push(menu.url);
              }}
            >
              <span className="flex items-center justify-center w-5 h-5 min-w-5 min-h-5">
                {menu.icon(isActive(menu.url) ? '' : '')}
              </span>
              <span>{menu.title}</span>
            </Link>
          </SidebarMenuButton>
        </SidebarMenuItem>
      ))}
    </SidebarMenu>
  );
};

const AdminLayout = ({
  children,
}: {
  children: React.ReactNode;
  className?: string;
}) => {
  const router = useRouter();
  const { t } = useTranslation();
  const { toast } = useToast();
  const [version, setVersion] = useState<GetChatVersionResult>();
  const [selectedMenu, setSelectedMenu] = useState<MenuItem>();
  const user = useUserInfo();

  const isActive = (url: string) => url === router.pathname;

  useEffect(() => {
    setSelectedMenu(menus.find((menu) => isActive(menu.url)));
  }, [router.pathname]);

  const menus: MenuItem[] = [
    {
      url: '/admin/dashboard',
      icon: (stroke?: string) => (
        <IconChartPie strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Dashboard'),
    },
    {
      url: '/admin/model',
      icon: (stroke?: string) => (
        <IconSettingsCog strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Model Configs'),
    },
    {
      url: '/admin/users',
      icon: (stroke?: string) => (
        <IconUsers strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('User Management'),
    },
    {
      url: '/admin/user-models',
      icon: (stroke?: string) => (
        <IconUserCog strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('User Model Permissions'),
    },
    {
      url: '/admin/messages',
      icon: (stroke?: string) => (
        <IconMessages strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('User Messages'),
    },
    {
      url: '/admin/file-service',
      icon: (stroke?: string) => (
        <IconFiles strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('File Service'),
    },
    {
      url: '/admin/login-service',
      icon: (stroke?: string) => (
        <IconShieldLock strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Login Service'),
    },
    {
      url: '/admin/usage',
      icon: (stroke?: string) => (
        <IconMoneybag strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Usage Records'),
    },
    {
      url: '/admin/security-logs',
      icon: (stroke?: string) => (
        <IconShieldLock strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Security Logs'),
    },
    /**{
      url: '/admin/request-logs',
      icon: (stroke?: string) => <IconNotes strokeWidth={1.2} stroke={stroke} />,
      title: t('Request Logs'),
    },**/
    {
      url: '/admin/user-config',
      icon: (stroke?: string) => (
        <IconUserCog strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Account Initial Config'),
    },
    {
      url: '/admin/global-configs',
      icon: (stroke?: string) => (
        <IconSettings strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Global Configs'),
    },
    {
      url: '/admin/invitation-code',
      icon: (stroke?: string) => (
        <IconIdBadge strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('Invitation Code Management'),
    },
  ];

  useEffect(() => {
    document.title = t('Chats Admin Panel');

    postChatsVersion().then((v) => {
      setVersion(v);
      if (v.hasNewVersion) {
        toast({
          description: t(
            'A new version is now available. Update for the latest features and improvements.',
          ),
          duration: 10000,
          action: (
            <ToastAction
              altText={t('Go to upgrade')}
              onClick={() => {
                location.href = 'https://github.com/sdcb/chats/releases';
              }}
            >
              {t('Go to upgrade')}
            </ToastAction>
          ),
        });
      }
    });
  }, []);

  return (
    <SidebarProvider defaultOpen={true}>
      <div className="h-screen w-full flex">
        <Sidebar collapsible="offcanvas" className="mt-0.5">
          <SidebarHeader>
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton
                  asChild
                  className="flex w-full items-center gap-2 px-2 py-3 rounded-md hover:bg-transparent active:bg-transparent"
                >
                  <Link
                    href="/"
                    onClick={(e) => {
                      e.preventDefault();
                      router.push('/');
                    }}
                  >
                    <div className="flex items-center gap-2">
                      <img
                        className="h-8 w-8 rounded-sm"
                        alt="Chats Logo"
                        src="/icons/logo.png"
                      />
                      <span className="text-base font-semibold">
                        Chats
                        {version?.currentVersion && (
                          <Badge variant="outline" className="ml-2 text-xs">
                            {version?.currentVersion}
                          </Badge>
                        )}
                      </span>
                    </div>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarHeader>
          <SidebarContent className="mt-5">
            <AdminMenu menus={menus} isActive={isActive} />
          </SidebarContent>
          <SidebarFooter>
            <SidebarMenu className="px-1">
              <SidebarMenuItem>
                <UserMenuPopover
                  isAdminPage={true}
                  trigger={
                    <SidebarMenuButton className="h-10">
                      <Avatar className="h-6 w-6 rounded-lg">
                        <AvatarFallback className="rounded-lg w-6 h-6 bg-foreground text-background">
                          {user?.username?.substring(0, 1)}
                        </AvatarFallback>
                      </Avatar>
                      <div className="grid flex-1 text-left text-sm leading-tight">
                        <span className="truncate font-semibold">
                          {user?.username}
                        </span>
                      </div>
                    </SidebarMenuButton>
                  }
                />
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarFooter>
        </Sidebar>
        <div className="w-full flex flex-col">
          <div className="flex p-3 items-center border-b">
            <SidebarTrigger
              className="mr-2"
              icon={<IconLayoutSidebar size={26} strokeWidth={1} />}
            />
            <h1 className="font-medium">
              {selectedMenu?.title || t('Chats Admin Panel')}
            </h1>
          </div>
          <div className="flex-1 overflow-auto p-4">{children}</div>
        </div>
        <Toaster />
      </div>
    </SidebarProvider>
  );
};

export default AdminLayout;
