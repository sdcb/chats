import { forwardRef } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';

import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import { IconChevronDown, IconRefresh } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  models: AdminModelDto[];
  readonly?: boolean;
  hidden?: boolean;
  disabled?: boolean;
  isHoverVisible?: boolean;
  onRegenerate: () => void;
  onChangeModel: (model: AdminModelDto) => void;
  // 展示提示用：点击后将使用的模型名称
  regenerateModelName?: string;
}

export const RegenerateWithModelAction = forwardRef<HTMLButtonElement, Props>(
  (props, ref) => {
    const { t } = useTranslation();
    const {
      models,
      readonly,
      hidden,
      disabled,
      isHoverVisible,
      onRegenerate,
      onChangeModel,
      regenerateModelName,
    } = props;

    const Render = () => {
      return (
        <div className={cn(
          isHoverVisible ? 'invisible' : 'visible',
          'flex group-hover:visible focus-within:visible',
        )}>
          {/* 左侧重新生成按钮 */}
          <Tips
            trigger={
              <Button
                disabled={disabled}
                variant="ghost"
                className="p-1 m-0 h-7 w-7 rounded-r-none border-r border-r-border/50 hover:bg-accent hover:border-r-accent-foreground/20"
                onClick={(e) => {
                  onRegenerate();
                  e.stopPropagation();
                }}
              >
                <IconRefresh className="w-4 h-4" />
              </Button>
            }
            side="bottom"
            content={
              regenerateModelName
                ? `${t('Regenerate')}: ${regenerateModelName}`
                : t('Regenerate')!
            }
          />

          {/* 右侧下拉箭头按钮 - 复用现有的 ChatModelDropdownMenu */}
          <Tips
            trigger={
              <span className="inline-flex">
                <ChatModelDropdownMenu
                  ref={ref}
                  models={models}
                  readonly={readonly || disabled}
                  onChangeModel={onChangeModel}
                  hideIcon={true}
                  triggerClassName="p-1 m-0 h-7 w-7 rounded-l-none hover:bg-accent focus:outline-none justify-center"
                  className="px-0"
                  content={<IconChevronDown className="w-4 h-4" />}
                />
              </span>
            }
            side="bottom"
            content={t('Change Model')!}
          />
        </div>
      );
    };

    return <>{!hidden && Render()}</>;
  },
);

RegenerateWithModelAction.displayName = 'RegenerateWithModelAction';

export default RegenerateWithModelAction;
