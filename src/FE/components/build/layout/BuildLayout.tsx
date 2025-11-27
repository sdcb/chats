'use client';

import * as React from 'react';
import { useEffect, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import {
  IconChartHistogram,
  IconKey,
  IconLayoutSidebar,
  IconNotes,
} from '@/components/Icons/index';
import UserMenuPopover, { PageType } from '@/components/UserMenuPopover/UserMenuPopover';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
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
import { Toaster } from '@/components/ui/toaster';

import { useUserInfo } from '@/providers/UserProvider';

interface MenuItem {
  url: string;
  icon: (stroke?: string) => React.ReactNode;
  title: string;
}

const BuildMenu = ({
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

const BuildLayout = ({
  children,
}: {
  children: React.ReactNode;
  className?: string;
}) => {
  const router = useRouter();
  const { t } = useTranslation();
  const [selectedMenu, setSelectedMenu] = useState<MenuItem>();
  const user = useUserInfo();

  const isActive = (url: string) => url === router.pathname;

  const menus: MenuItem[] = [
    {
      url: '/build/api-key',
      icon: (stroke?: string) => (
        <IconKey strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('API Key'),
    },
    {
      url: '/build/docs',
      icon: (stroke?: string) => (
        <IconNotes strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('API Docs'),
    },
    {
      url: '/build/usage',
      icon: (stroke?: string) => (
        <IconChartHistogram strokeWidth={1.2} stroke={stroke} />
      ),
      title: t('API Usage Records'),
    },
  ];

  useEffect(() => {
    setSelectedMenu(menus.find((menu) => isActive(menu.url)));
  }, [router.pathname]);

  useEffect(() => {
    document.title = 'Chats API';
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
                        Chats API
                      </span>
                    </div>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarHeader>
          <SidebarContent className="mt-5">
            <BuildMenu menus={menus} isActive={isActive} />
          </SidebarContent>
          <SidebarFooter>
            <SidebarMenu className="px-1">
              <SidebarMenuItem>
                <UserMenuPopover
                  pageType={PageType.Build}
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
              {selectedMenu?.title || t('Chats Build')}
            </h1>
          </div>
          <div className="flex-1 overflow-auto p-4">{children}</div>
        </div>
        <Toaster />
      </div>
    </SidebarProvider>
  );
};

export default BuildLayout;
