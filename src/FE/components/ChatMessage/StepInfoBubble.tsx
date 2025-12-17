import { useState } from 'react';
import { IconInfo } from '@/components/Icons';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { IStepGenerateInfo } from '@/types/chatMessage';
import { requestStepGenerateInfo, AggregatedGenerateInfo } from '@/utils/generateInfoCache';
import { GenerateInfoPopoverContent } from './GenerateInfoPopoverContent';

interface Props {
  stepId: string;
  edited: boolean;
  chatId?: string;
  chatShareId?: string;
  className?: string;
}

// 将单个 step info 转换为 AggregatedGenerateInfo 格式
const toAggregatedInfo = (info: IStepGenerateInfo | null): AggregatedGenerateInfo | null => {
  if (!info) return null;
  
  const cachedTokens = info.inputCachedTokens ?? 0;
  const overallTokens = info.inputOverallTokens ?? cachedTokens;
  const freshTokens = Math.max(0, overallTokens - cachedTokens);
  const cachedPrice = info.inputCachedPrice ?? 0;
  const freshPrice = info.inputFreshPrice ?? Math.max(0, (info.inputPrice ?? 0) - cachedPrice);

  return {
    inputOverallTokens: overallTokens,
    inputFreshTokens: freshTokens,
    inputCachedTokens: cachedTokens,
    outputTokens: info.outputTokens ?? 0,
    inputPrice: info.inputPrice ?? freshPrice + cachedPrice,
    inputFreshPrice: freshPrice,
    inputCachedPrice: cachedPrice,
    outputPrice: info.outputPrice ?? 0,
    reasoningTokens: info.reasoningTokens ?? 0,
    duration: info.duration ?? 0,
    reasoningDuration: info.reasoningDuration ?? 0,
    firstTokenLatency: info.firstTokenLatency ?? 0,
  };
};

export const StepInfoBubble = ({ stepId, edited, chatId, chatShareId, className }: Props) => {
  const [info, setInfo] = useState<IStepGenerateInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [isOpen, setIsOpen] = useState(false);

  // 如果是编辑过的 step 或没有 stepId，不显示
  if (edited || !stepId) {
    return null;
  }

  const handleLoadInfo = async () => {
    if (loaded || loading) return;
    setLoading(true);
    try {
      const result = await requestStepGenerateInfo({ stepId, chatId, chatShareId });
      setInfo(result);
    } finally {
      setLoading(false);
      setLoaded(true);
    }
  };

  const aggregatedInfo = toAggregatedInfo(info);

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          className={`p-1 m-0 h-7 w-7 hover:bg-accent hover:text-accent-foreground transition-colors ${className || ''}`}
          onClick={(e) => {
            e.stopPropagation();
            setIsOpen(!isOpen);
          }}
          onMouseEnter={() => {
            if (window.matchMedia('(hover: hover)').matches) {
              setIsOpen(true);
              handleLoadInfo();
            }
          }}
          onMouseLeave={() => {
            if (window.matchMedia('(hover: hover)').matches) {
              setIsOpen(false);
            }
          }}
        >
          {loading ? (
            <IconInfo size={18} className="animate-pulse opacity-50" />
          ) : (
            <IconInfo size={18} />
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent 
        side="bottom" 
        className="w-auto p-1 shadow-lg border-2"
        onPointerDownOutside={() => setIsOpen(false)}
      >
        <GenerateInfoPopoverContent info={aggregatedInfo} loading={loading} title="Step information" />
      </PopoverContent>
    </Popover>
  );
};

export default StepInfoBubble;
