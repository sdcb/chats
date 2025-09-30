import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney, toFixed } from '@/utils/common';

import { IChatMessage } from '@/types/chatMessage';

import { IconInfo } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { useState } from 'react';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  message: IChatMessage;
}

export const GenerateInformationAction = (props: Props) => {
  const { t } = useTranslation();
  const { message, hidden, disabled } = props;
  const [isOpen, setIsOpen] = useState(false);

  const GenerateInformation = (props: { 
    name: string; 
    value: string;
    icon?: string;
  }) => {
    const { name, value, icon } = props;
    return (
      <div className="flex items-center justify-between py-0.5 px-1.5 rounded hover:bg-accent/50 transition-colors">
        <span className="text-[11px] font-medium text-muted-foreground flex items-center gap-1">
          {icon && <span className="text-xs">{icon}</span>}
          {t(name)}
        </span>
        <span className="text-[11px] font-semibold text-foreground ml-3">
          {value}
        </span>
      </div>
    );
  };

  const Render = () => {
    return (
      <Popover open={isOpen} onOpenChange={setIsOpen}>
        <PopoverTrigger asChild>
          <Button
            disabled={disabled}
            variant="ghost"
            className="p-1 m-0 h-7 w-7 hover:bg-accent hover:text-accent-foreground transition-colors"
            onClick={(e) => {
              e.stopPropagation();
              setIsOpen(!isOpen);
            }}
            onMouseEnter={(e) => {
              // 只在非触摸设备上启用悬停效果
              if (window.matchMedia('(hover: hover)').matches) {
                setIsOpen(true);
              }
            }}
            onMouseLeave={(e) => {
              // 只在非触摸设备上启用悬停效果
              if (window.matchMedia('(hover: hover)').matches) {
                setIsOpen(false);
              }
            }}
          >
            <IconInfo />
          </Button>
        </PopoverTrigger>
        <PopoverContent 
          side="bottom" 
          className="w-auto p-1 shadow-lg border-2"
          onPointerDownOutside={() => setIsOpen(false)}
        >
          <div className="min-w-[180px]">
            <div className="mb-2 pb-1.5 border-b">
              <Label className="text-xs font-semibold flex items-center justify-center gap-1.5">
                <span className="text-sm">📊</span>
                {t('Generate information')}
              </Label>
            </div>
            <div className="space-y-0.5">
              <GenerateInformation
                name={'total duration'}
                value={message.duration?.toLocaleString() + 'ms'}
                icon="⏱️"
              />
              <GenerateInformation
                name={'first token latency'}
                value={message.firstTokenLatency?.toLocaleString() + 'ms'}
                icon="⚡"
              />
              <GenerateInformation
                name={'prompt tokens'}
                value={`${message.inputTokens?.toLocaleString()}`}
                icon="📥"
              />
              <GenerateInformation
                name={'response tokens'}
                value={`${(
                  message.outputTokens - message.reasoningTokens
                ).toLocaleString()}`}
                icon="📤"
              />
              {!!message.reasoningTokens && (
                <GenerateInformation
                  name={'reasoning tokens'}
                  value={`${message.reasoningTokens.toLocaleString()}`}
                  icon="🧠"
                />
              )}
              <GenerateInformation
                name={'response speed'}
                value={
                  message.duration
                    ? toFixed(
                        (message.outputTokens / (message.duration || 0)) *
                          1000,
                      ) + ' token/s'
                    : '-'
                }
                icon="🚀"
              />
              {(message.inputPrice > 0 || message.outputPrice > 0) && (
                <div className="pt-1.5 mt-1.5 border-t space-y-0.5">
                  {message.inputPrice > 0 && (
                    <GenerateInformation
                      name={'prompt cost'}
                      value={'￥' + formatNumberAsMoney(+message.inputPrice, 6)}
                      icon="💰"
                    />
                  )}
                  {message.outputPrice > 0 && (
                    <GenerateInformation
                      name={'response cost'}
                      value={'￥' + formatNumberAsMoney(+message.outputPrice, 6)}
                      icon="💵"
                    />
                  )}
                  <GenerateInformation
                    name={'total cost'}
                    value={
                      '￥' +
                      formatNumberAsMoney(
                        +message.inputPrice + +message.outputPrice,
                        6,
                      )
                    }
                    icon="💳"
                  />
                </div>
              )}
            </div>
          </div>
        </PopoverContent>
      </Popover>
    );
  };

  return <>{!hidden && Render()}</>;
};

export default GenerateInformationAction;
