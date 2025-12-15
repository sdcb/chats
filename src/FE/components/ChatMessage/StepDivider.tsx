import { useState, useEffect } from 'react';
import { IconBolt } from '@/components/Icons';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { IStepGenerateInfo } from '@/types/chatMessage';
import { requestStepGenerateInfo } from '@/utils/generateInfoCache';
import useTranslation from '@/hooks/useTranslation';

interface Props {
  stepId: string;
  stepIndex: number;
  edited: boolean;
  chatId?: string;
  chatShareId?: string;
}

const formatNumber = (num: number | undefined): string => {
  if (num === undefined) return '-';
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M';
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K';
  return num.toString();
};

const formatPrice = (price: number | undefined): string => {
  if (price === undefined || price === 0) return '-';
  if (price < 0.0001) return '<$0.0001';
  return '$' + price.toFixed(4);
};

const formatDuration = (ms: number | undefined): string => {
  if (ms === undefined) return '-';
  if (ms < 1000) return ms + 'ms';
  return (ms / 1000).toFixed(1) + 's';
};

export const StepDivider = ({ stepId, stepIndex, edited, chatId, chatShareId }: Props) => {
  const { t } = useTranslation();
  const [info, setInfo] = useState<IStepGenerateInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);

  // 如果是编辑过的 step，不显示
  if (edited || !stepId) {
    return (
      <div className="flex items-center gap-2 my-2 opacity-50">
        <div className="flex-1 border-t border-dashed border-muted-foreground/30" />
        <span className="text-[10px] text-muted-foreground/50 select-none">
          {t('Step')} {stepIndex + 1} {edited && `(${t('Edited')})`}
        </span>
        <div className="flex-1 border-t border-dashed border-muted-foreground/30" />
      </div>
    );
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

  const totalTokens = info ? (info.inputOverallTokens || 0) + (info.outputTokens || 0) + (info.reasoningTokens || 0) : undefined;

  return (
    <div className="flex items-center gap-2 my-2">
      <div className="flex-1 border-t border-dashed border-muted-foreground/20" />
      <TooltipProvider delayDuration={0}>
        <Tooltip>
          <TooltipTrigger asChild>
            <button
              className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] text-muted-foreground/70 hover:text-muted-foreground hover:bg-muted/50 transition-colors select-none"
              onMouseEnter={handleLoadInfo}
              onClick={handleLoadInfo}
            >
              <IconBolt size={10} />
              {loading ? (
                <span className="animate-pulse">...</span>
              ) : info ? (
                <span>{formatNumber(totalTokens)} tokens</span>
              ) : (
                <span>{t('Step')} {stepIndex + 1}</span>
              )}
            </button>
          </TooltipTrigger>
          {info && (
            <TooltipContent side="top" className="text-xs p-2 max-w-[280px]">
              <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                <span className="text-muted-foreground">{t('Input')}:</span>
                <span>{formatNumber(info.inputOverallTokens)} tokens</span>
                
                {info.inputCachedTokens !== undefined && info.inputCachedTokens > 0 && (
                  <>
                    <span className="text-muted-foreground pl-2">└ {t('Cached')}:</span>
                    <span>{formatNumber(info.inputCachedTokens)} tokens</span>
                  </>
                )}
                
                <span className="text-muted-foreground">{t('Output')}:</span>
                <span>{formatNumber(info.outputTokens)} tokens</span>
                
                {info.reasoningTokens !== undefined && info.reasoningTokens > 0 && (
                  <>
                    <span className="text-muted-foreground">{t('Reasoning')}:</span>
                    <span>{formatNumber(info.reasoningTokens)} tokens</span>
                  </>
                )}
                
                <div className="col-span-2 border-t border-muted my-1" />
                
                <span className="text-muted-foreground">{t('Cost')}:</span>
                <span>{formatPrice((info.inputPrice || 0) + (info.outputPrice || 0))}</span>
                
                <span className="text-muted-foreground">{t('Duration')}:</span>
                <span>{formatDuration(info.duration)}</span>
                
                {info.firstTokenLatency !== undefined && info.firstTokenLatency > 0 && (
                  <>
                    <span className="text-muted-foreground">{t('First Token')}:</span>
                    <span>{formatDuration(info.firstTokenLatency)}</span>
                  </>
                )}
              </div>
            </TooltipContent>
          )}
        </Tooltip>
      </TooltipProvider>
      <div className="flex-1 border-t border-dashed border-muted-foreground/20" />
    </div>
  );
};

export default StepDivider;
