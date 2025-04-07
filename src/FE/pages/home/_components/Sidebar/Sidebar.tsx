import { ReactNode } from 'react';

import useTranslation from '@/hooks/useTranslation';

import {
  IconCheck,
  IconLayoutSidebar,
  IconLayoutSidebarRight,
  IconSearch,
  IconSquarePlus,
  IconX,
} from '@/components/Icons/index';
import Search from '@/components/Search/Search';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';

import { cn } from '@/lib/utils';

interface Props<T> {
  isLoading?: boolean;
  showOpenButton?: boolean;
  isOpen: boolean;
  addItemButtonTitle: string;
  side: 'left' | 'right';
  items: T[];
  itemComponent?: ReactNode;
  folderComponent?: ReactNode;
  footerComponent?: ReactNode;
  actionComponent?: ReactNode;
  actionConfirmComponent?: ReactNode;
  searchTerm: string;
  messageIsStreaming?: boolean;
  handleSearchTerm: (searchTerm: string) => void;
  toggleOpen: () => void;
  handleCreateItem: () => void;
  hasModel: () => boolean;
}

const Sidebar = <T,>({
  isLoading = false,
  showOpenButton = true,
  isOpen,
  addItemButtonTitle,
  side,
  items,
  itemComponent,
  folderComponent,
  footerComponent,
  actionComponent,
  actionConfirmComponent,
  searchTerm,
  messageIsStreaming,
  handleSearchTerm,
  toggleOpen,
  handleCreateItem,
  hasModel,
}: Props<T>) => {
  const { t } = useTranslation();
  const NoDataRender = () =>
    isLoading === false &&
    items.length === 0 && (
      <div className="select-none text-center flex flex-col justify-center h-56 opacity-50">
        <IconSearch className="mx-auto mb-3" />
        <span className="text-[14px] leading-normal">{t('No data')}</span>
      </div>
    );

  return (
    <>
      <div
        className={`${
          isOpen ? 'w-[260px]' : 'w-0 hidden'
        } fixed top-0 ${side}-0 z-40 flex h-full flex-none flex-col bg-gray-50 bg-sidebar p-2 text-[14px] sm:relative sm:top-0 shadow-sm`}
      >
        <div className="sticky">
          <div
            className={cn(
              'flex items-center pr-1.5 justify-between',
              side === 'right' && 'flex-row-reverse',
            )}
          >
            <Tips
              trigger={
                <Button
                  variant="ghost"
                  className="p-1 m-0 h-auto"
                  onClick={toggleOpen}
                >
                  {side === 'right' ? (
                    <IconLayoutSidebarRight size={26} />
                  ) : (
                    <IconLayoutSidebar size={26} />
                  )}
                </Button>
              }
            />
            {hasModel() && (
              <Tips
                trigger={
                  <Button
                    onClick={() => {
                      handleCreateItem();
                    }}
                    disabled={messageIsStreaming}
                    variant="ghost"
                    className="p-1 m-0 h-auto"
                  >
                    <IconSquarePlus size={26} />
                  </Button>
                }
                content={addItemButtonTitle}
              />
            )}
          </div>
          <div
            className={cn(
              'pr-1.5 h-16',
              actionComponent && 'flex items-center gap-x-1',
            )}
          >
            <Search
              placeholder={t('Search...') || ''}
              searchTerm={searchTerm}
              onSearch={handleSearchTerm}
            />
            {actionComponent}
          </div>
          {actionConfirmComponent}
        </div>

        {isLoading && (
          <div className="h-screen flex flex-col space-y-2 py-2">
            <Skeleton className="h-11 w-full" />
            <Skeleton className="h-11 w-full" />
            <Skeleton className="h-11 w-full" />
          </div>
        )}

        <div className="flex-grow overflow-hidden overflow-y-scroll scroll-container">
          <div className="flex">{folderComponent}</div>

          {items?.length > 0 && !isLoading && (
            <div className="pt-2">{itemComponent}</div>
          )}
          {NoDataRender()}
        </div>
        {footerComponent}
      </div>

      {!isOpen && showOpenButton && (
        <div
          className={`group fixed z-20 ${
            side === 'right' ? 'right-2' : 'left-[8px]'
          }`}
          style={{ top: '8px' }}
        >
          <Button
            className="p-0 m-0 h-auto w-auto bg-transparent hover:bg-muted"
            onClick={toggleOpen}
          >
            <span data-state="closed">
              <div className="flex items-center justify-center">
                {side === 'right' ? (
                  <div className="flex flex-col items-center">
                    <IconLayoutSidebarRight size={26} />
                  </div>
                ) : (
                  <div className="flex flex-col items-center p-1 m-0 h-auto">
                    <IconLayoutSidebar size={26} />
                  </div>
                )}
              </div>
            </span>
          </Button>
          {hasModel() && (
            <Tips
              trigger={
                <Button
                  onClick={() => {
                    handleCreateItem();
                  }}
                  disabled={messageIsStreaming}
                  variant="ghost"
                  className="p-1 m-0 h-auto"
                >
                  <IconSquarePlus size={26} />
                </Button>
              }
              content={addItemButtonTitle}
            />
          )}
        </div>
      )}
    </>
  );
};

export default Sidebar;
