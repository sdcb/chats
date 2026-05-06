import { Fragment, forwardRef, useState, ReactNode } from 'react';

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

import { ANIMATION_DURATION_MS } from '@/constants/animation';
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

const truncateMiddle = (text: string, maxLength = 28, prefixLength = 4, suffixLength = 12) => {
  if (text.length <= maxLength) {
    return text;
  }

  const safePrefixLength = Math.max(1, Math.min(prefixLength, maxLength - 4));
  const safeSuffixLength = Math.max(1, Math.min(suffixLength, maxLength - safePrefixLength - 3));

  return `${text.slice(0, safePrefixLength)}...${text.slice(-safeSuffixLength)}`;
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
  const [expandedProviderId, setExpandedProviderId] = useState<number | null>(null);

  const normalizedSearchTerm = searchTerm.trim().toLowerCase();
  const modelGroup = models.reduce((groups, model) => {
    const existingGroup = groups.find((group) => group.providerId === model.modelProviderId);

    if (existingGroup) {
      existingGroup.child.push(model);
    } else {
      groups.push({
        providerId: model.modelProviderId,
        child: [model],
      });
    }

    return groups;
  }, [] as { providerId: number; child: AdminModelDto[] }[])
    .map((group) => {
      if (!normalizedSearchTerm) {
        return group;
      }

      const providerName = feModelProviders[group.providerId]?.name ?? '';
      const translatedProviderName = t(providerName);
      const providerMatched = providerName.toLowerCase().includes(normalizedSearchTerm)
        || translatedProviderName.toLowerCase().includes(normalizedSearchTerm);

      if (providerMatched) {
        return group;
      }

      return {
        ...group,
        child: group.child.filter(model => model.name.toLowerCase().includes(normalizedSearchTerm)),
      };
    })
    .filter(group => group.child.length > 0);

  const handleSearch = (value: string) => {
    setSearchTerm(value);
    setExpandedProviderId(null);
  };

  const handleOpenMenu = (open: boolean) => {
    if (open) {
      setSearchTerm('');
    }
    setExpandedProviderId(null);
  };

  const renderModelItems = (providerId: number, items: AdminModelDto[]) => {
    return items.map((model) => {
      const ApiIcon = getApiTypeIcon(model.apiType);

      return (
        <DropdownMenuItem
          key={model.modelId}
          onClick={(e) => {
            setExpandedProviderId(null);
            onChangeModel(model);
            e.stopPropagation();
          }}
          className="flex max-w-full items-center gap-1"
        >
          <ApiIcon className="w-4 h-4 flex-shrink-0" />
          <span className="block min-w-0 truncate text-sm" title={model.name}>
            {truncateMiddle(model.name)}
          </span>
        </DropdownMenuItem>
      );
    });
  };

  const renderNoModel = () => {
    if (modelGroup.length > 0) {
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
        align="start"
        alignOffset={-6}
        className="w-[min(13rem,calc(100vw-0.5rem))] md:w-52"
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
        <DropdownMenuGroup
          className={cn(
            'max-h-[14.75rem] overflow-y-auto scroller md:max-h-none md:overflow-visible',
            groupClassName,
          )}
        >
          {modelGroup.map((m) => {
            const isExpanded = expandedProviderId === m.providerId;

            return (
              <Fragment key={m.providerId}>
                <div className="md:hidden">
                  <button
                    type="button"
                    className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm outline-none transition-colors hover:bg-accent focus:bg-accent"
                    onClick={(e) => {
                      e.stopPropagation();
                      setExpandedProviderId(current => current === m.providerId ? null : m.providerId);
                    }}
                  >
                    <ModelProviderIcon providerId={m.providerId} />
                    <span className="min-w-0 flex-1 text-nowrap overflow-hidden text-ellipsis whitespace-nowrap">
                      {t(feModelProviders[m.providerId].name)}
                    </span>
                    <IconChevronDown
                      size={16}
                      className={cn('flex-shrink-0 transition-transform', isExpanded && 'rotate-180')}
                      style={{ transitionDuration: `${ANIMATION_DURATION_MS}ms` }}
                    />
                  </button>
                  <div
                    className="grid overflow-hidden"
                    style={{
                      gridTemplateRows: isExpanded ? '1fr' : '0fr',
                      opacity: isExpanded ? 1 : 0,
                      transition: `grid-template-rows ${ANIMATION_DURATION_MS}ms ease, opacity ${ANIMATION_DURATION_MS}ms ease`,
                    }}
                  >
                    <div
                      className="min-h-0 max-h-[min(16rem,50vh)] overflow-y-auto px-1 scroller"
                      style={{ pointerEvents: isExpanded ? 'auto' : 'none' }}
                    >
                      {renderModelItems(m.providerId, m.child)}
                    </div>
                  </div>
                </div>

                <div className="hidden md:block">
                  <DropdownMenuSub>
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
                        sideOffset={4}
                        alignOffset={-4}
                        className="max-h-96 w-fit min-w-[10rem] max-w-[min(18rem,calc(var(--radix-popper-available-width)-0.5rem),calc(100vw-1rem))] overflow-y-auto scroller"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {renderModelItems(m.providerId, m.child)}
                      </DropdownMenuSubContent>
                    </DropdownMenuPortal>
                  </DropdownMenuSub>
                </div>
              </Fragment>
            );
          })}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
});

ChatModelDropdownMenu.displayName = 'ChatModelDropdownMenu';

export default ChatModelDropdownMenu;
