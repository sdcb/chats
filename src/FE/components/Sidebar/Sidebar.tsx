import {
  PointerEvent as ReactPointerEvent,
  ReactNode,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';

import { useIsMobile } from '@/hooks/useMobile';
import useTranslation from '@/hooks/useTranslation';

import {
  IconLayoutSidebar,
  IconLayoutSidebarRight,
  IconLoader,
  IconSearch,
  IconSquarePlus,
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
  handleCreateItem: () => void | Promise<void>;
  hasModel: () => boolean;
  resizable?: boolean;
  desktopWidth?: number;
  desktopMinWidth?: number;
  desktopMaxWidth?: number;
  onDesktopWidthChange?: (
    width: number,
    options?: { persist?: boolean },
  ) => void;
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
  resizable = false,
  desktopWidth,
  desktopMinWidth = 280,
  desktopMaxWidth = 520,
  onDesktopWidthChange,
}: Props<T>) => {
  const { t } = useTranslation();
  const isMobile = useIsMobile();
  const [isCreating, setIsCreating] = useState(false);
  const latestWidthRef = useRef<number>(desktopWidth ?? desktopMinWidth);
  const dragCleanupRef = useRef<(() => void) | null>(null);

  const handleCreate = async () => {
    console.log('handleCreate called, isCreating before:', isCreating);
    setIsCreating(true);
    console.log('isCreating set to true');
    try {
      await handleCreateItem();
      console.log('handleCreateItem completed');
    } finally {
      setIsCreating(false);
      console.log('isCreating set to false');
    }
  };

  const NoDataRender = () =>
    isLoading === false &&
    items.length === 0 && (
      <div className="select-none text-center flex flex-col justify-center h-56 opacity-50">
        <IconSearch className="mx-auto mb-3" />
        <span className="text-[14px] leading-normal">{t('No data')}</span>
      </div>
    );

  const restoreDragStyles = useCallback(() => {
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  }, []);

  useEffect(() => {
    latestWidthRef.current = desktopWidth ?? desktopMinWidth;
  }, [desktopMinWidth, desktopWidth]);

  useEffect(() => {
    return () => {
      dragCleanupRef.current?.();
      restoreDragStyles();
    };
  }, [restoreDragStyles]);

  const clampDesktopWidth = useCallback(
    (width: number) => {
      const maxWidth = Math.max(desktopMinWidth, desktopMaxWidth);
      return Math.max(desktopMinWidth, Math.min(width, maxWidth));
    },
    [desktopMaxWidth, desktopMinWidth],
  );

  const handleResizeStart = useCallback(
    (e: ReactPointerEvent<HTMLDivElement>) => {
      if (!resizable || isMobile || !isOpen || !onDesktopWidthChange) return;
      if (e.button !== 0) return;

      e.preventDefault();
      e.stopPropagation();

      const startX = e.clientX;
      const startWidth = latestWidthRef.current;
      document.body.style.cursor = 'col-resize';
      document.body.style.userSelect = 'none';

      const onMove = (event: PointerEvent) => {
        const delta = side === 'left'
          ? event.clientX - startX
          : startX - event.clientX;
        const nextWidth = clampDesktopWidth(startWidth + delta);
        latestWidthRef.current = nextWidth;
        onDesktopWidthChange(nextWidth);
      };

      const onUp = () => {
        dragCleanupRef.current?.();
        dragCleanupRef.current = null;
        restoreDragStyles();
        onDesktopWidthChange(latestWidthRef.current, { persist: true });
      };

      dragCleanupRef.current?.();
      dragCleanupRef.current = () => {
        window.removeEventListener('pointermove', onMove);
        window.removeEventListener('pointerup', onUp);
      };

      window.addEventListener('pointermove', onMove);
      window.addEventListener('pointerup', onUp);
    },
    [
      clampDesktopWidth,
      isMobile,
      isOpen,
      onDesktopWidthChange,
      resizable,
      restoreDragStyles,
      side,
    ],
  );

  const desktopSidebarWidth = clampDesktopWidth(desktopWidth ?? desktopMinWidth);
  const showResizeRail = resizable && isOpen && !isMobile;

  return (
    <>
      <div
        className={cn(
          isOpen ? 'w-full sm:w-auto' : 'w-0 hidden',
          'fixed top-0 z-40 flex h-full flex-none flex-col bg-card p-2 text-[14px] shadow-md sm:relative sm:top-0',
          side === 'right' ? 'right-0' : 'left-0',
        )}
        style={!isMobile && isOpen ? { width: `${desktopSidebarWidth}px` } : undefined}
      >
        <div className="sticky mt-2">
          <div
            className={cn(
              'flex items-center pr-1 justify-between',
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
                      handleCreate();
                    }}
                    disabled={messageIsStreaming || isCreating}
                    variant="ghost"
                    className="p-1 m-0 h-auto"
                  >
                    {isCreating ? (
                      <IconLoader size={26} className="animate-spin" />
                    ) : (
                      <IconSquarePlus size={26} />
                    )}
                  </Button>
                }
                content={addItemButtonTitle}
              />
            )}
          </div>
          <div className="mt-3">
            <Search
              placeholder={t('Search...') || ''}
              searchTerm={searchTerm}
              onSearch={handleSearchTerm}
            />
            {!searchTerm && actionComponent && (
              <div className="relative">
                <div className="absolute right-1 bottom-2">
                  {actionComponent}
                </div>
              </div>
            )}
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

          {items?.length > 0 && !isLoading && <div>{itemComponent}</div>}
          {NoDataRender()}
        </div>
        {footerComponent}
        {showResizeRail && (
          <div
            aria-hidden="true"
            className={cn(
              'absolute inset-y-0 z-10 hidden w-3 sm:block',
              side === 'right'
                ? 'left-0 -translate-x-1/2 cursor-col-resize'
                : 'right-0 translate-x-1/2 cursor-col-resize',
            )}
            onPointerDown={handleResizeStart}
          >
            <div className="mx-auto h-full w-[2px] bg-transparent transition-colors hover:bg-border" />
          </div>
        )}
      </div>

      {!isOpen && showOpenButton && (
        <div
          className={`group fixed bg-card pt-2 z-20 h-12 rounded-sm ${
            side === 'right' ? 'right-2' : 'left-2'
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
                    handleCreate();
                  }}
                  disabled={messageIsStreaming || isCreating}
                  variant="ghost"
                  className="p-1 m-0 h-auto"
                >
                  {isCreating ? (
                    <IconLoader size={26} className="animate-spin" />
                  ) : (
                    <IconSquarePlus size={26} />
                  )}
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
