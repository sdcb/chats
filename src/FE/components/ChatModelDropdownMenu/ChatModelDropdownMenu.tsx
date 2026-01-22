import { forwardRef, useState, ReactNode } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { IconChevronDown, IconMessage, IconMessageStar, IconPhoto } from '@/components/Icons';
import Search from '@/components/Search/Search';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuPortal,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

import { cn } from '@/lib/utils';

// Helper function to get icon based on API type
const getApiTypeIcon = (apiType: number) => {
  switch (apiType) {
    case 0: // ChatCompletion
      return IconMessage;
    case 1: // Response
      return IconMessageStar;
    case 2: // ImageGeneration
      return IconPhoto;
    default:
      return IconMessage;
  }
};

const ChatModelDropdownMenu = forwardRef<HTMLButtonElement, {
  models: AdminModelDto[];
  modelId?: number;
  modelName?: string;
  readonly?: boolean;
  showRegenerate?: boolean;
  content?: string | ReactNode;
  className?: string;
  triggerClassName?: string;
  groupClassName?: string;
  hideIcon?: boolean;
  onChangeModel: (model: AdminModelDto) => void;
}>(({
  models,
  readonly,
  content,
  className,
  hideIcon,
  triggerClassName,
  groupClassName,
  onChangeModel,
}, ref) => {
  const { t } = useTranslation();
  const [searchTerm, setSearchTerm] = useState('');

  let modelGroup = [] as { providerId: number; child: AdminModelDto[] }[];
  const groupModel = () => {
    const modelList = searchTerm
      ? models.filter((model) => model.name.toLowerCase().includes(searchTerm))
      : models;
    modelList.forEach((m) => {
      const model = modelGroup.find((x) => x.providerId === m.modelProviderId);
      if (model) {
        model.child.push(m);
      } else {
        modelGroup.push({
          providerId: m.modelProviderId,
          child: [m],
        });
      }
    });
  };
  groupModel();

  const handleSearch = (value: string) => {
    setSearchTerm(value);
    groupModel();
  };

  const handleOpenMenu = () => {
    setSearchTerm('');
  };

  const renderNoModel = () => {
    if (models.length > 0) {
      return null;
    }
    return (
      <div className="p-2 mx-1 text-center text-muted-foreground text-sm">
        {t('No data')}
      </div>
    );
  };

  return (
    <DropdownMenu onOpenChange={handleOpenMenu}>
      <DropdownMenuTrigger
        ref={ref}
        disabled={readonly}
        className={cn(
          'focus:outline-none rounded-sm p-1 m-0 h-7 flex items-center border-neutral-600',
          triggerClassName,
        )}
      >
        <>
          <span
            className={cn(
              'flex font-medium px-1 items-center md:w-full text-nowrap overflow-hidden text-ellipsis whitespace-nowrap w-auto',
              className,
            )}
          >
            {content && content}
          </span>
          {!readonly && !hideIcon && <IconChevronDown />}
        </>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="w-40 md:w-52"
        onClick={(e) => e.stopPropagation()}
      >
        <Search
          className="p-2 mx-1"
          containerClassName="pt-1 pb-1"
          placeholder="Search..."
          searchTerm={searchTerm}
          onSearch={handleSearch}
        />
        {renderNoModel()}
        <DropdownMenuGroup className={groupClassName}>
          {modelGroup.map((m) => {
            return (
              <DropdownMenuSub key={m.providerId}>
                <DropdownMenuSubTrigger
                  key={`trigger-${m.providerId}`}
                  className="p-2 flex gap-2"
                >
                  <ModelProviderIcon providerId={m.providerId} />
                  <span className="w-full text-nowrap overflow-hidden text-ellipsis whitespace-nowrap">
                    {t(feModelProviders[m.providerId].name)}
                  </span>
                </DropdownMenuSubTrigger>
                <DropdownMenuPortal>
                  <DropdownMenuSubContent
                    className="max-h-96 overflow-y-auto custom-scrollbar max-w-[64px] md:max-w-[256px]"
                    onClick={(e) => e.stopPropagation()}
                  >
                    {m.child.map((x) => {
                      const ApiIcon = getApiTypeIcon(x.apiType);
                      return (
                        <DropdownMenuItem
                          key={x.modelId}
                          onClick={(e) => {
                            onChangeModel(x);
                            e.stopPropagation();
                          }}
                          className="flex items-center gap-1"
                        >
                          <ApiIcon className="w-3.5 h-3.5 flex-shrink-0" />
                          <span className="truncate text-sm">{x.name}</span>
                        </DropdownMenuItem>
                      );
                    })}
                  </DropdownMenuSubContent>
                </DropdownMenuPortal>
              </DropdownMenuSub>
            );
          })}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
});

ChatModelDropdownMenu.displayName = 'ChatModelDropdownMenu';

export default ChatModelDropdownMenu;
