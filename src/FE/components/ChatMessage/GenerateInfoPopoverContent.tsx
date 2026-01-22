import useTranslation from '@/hooks/useTranslation';
import { formatNumberAsMoney, toFixed } from '@/utils/common';
import { AggregatedGenerateInfo } from '@/utils/generateInfoCache';
import { Skeleton } from '@/components/ui/skeleton';
import { Label } from '@/components/ui/label';

interface GenerateInfoItemProps {
  name: string;
  value: string;
  icon?: string;
  loading?: boolean;
}

const GenerateInfoItem = ({ name, value, icon, loading }: GenerateInfoItemProps) => {
  const { t } = useTranslation();
  return (
    <div className="flex items-center justify-between py-0.5 px-1.5 rounded hover:bg-accent/50 transition-colors">
      <span className="text-[11px] font-medium text-muted-foreground flex items-center gap-1">
        {icon && <span className="text-xs">{icon}</span>}
        {t(name)}
      </span>
      {loading ? (
        <Skeleton className="h-3 w-20 ml-3" />
      ) : (
        <span className="text-[11px] font-semibold text-foreground ml-3">
          {value}
        </span>
      )}
    </div>
  );
};

interface GenerateInfoPopoverContentProps {
  info: AggregatedGenerateInfo | null;
  loading: boolean;
  title?: string;
  titleParams?: Record<string, any>;
  avgDurationMs?: number | null;
  avgFirstTokenLatencyMs?: number | null;
}

export const GenerateInfoPopoverContent = ({
  info,
  loading,
  title,
  titleParams,
  avgDurationMs,
  avgFirstTokenLatencyMs,
}: GenerateInfoPopoverContentProps) => {
  const { t } = useTranslation();

  const inputCachedTokens = info?.inputCachedTokens ?? 0;
  const totalInputTokens = info?.inputOverallTokens;

  const inputCachedPrice = info?.inputCachedPrice ?? 0;
  const totalInputPrice = info?.inputPrice ?? 0;
  const outputPrice = info?.outputPrice ?? 0;

  const showInputTotalCost = (!!info && totalInputPrice > 0) || loading;
  const showInputCachedCost = (!!info && inputCachedPrice > 0) || loading;
  const showOutputCost = (!!info && outputPrice > 0) || loading;
  const totalCost = totalInputPrice + outputPrice;

  return (
    <div className="min-w-[180px]">
      <div className="mb-2 pb-1.5 border-b">
        <Label className="text-xs font-semibold flex items-center justify-center gap-1.5">
          <span className="text-sm">📊</span>
          {t(title || 'Generate information', titleParams)}
        </Label>
      </div>
      <div className="space-y-0.5">
        <GenerateInfoItem
          name={'total duration'}
          value={info ? `${info.duration.toLocaleString()} ms` : '-'}
          icon="⏱️"
          loading={loading}
        />
        {avgDurationMs !== undefined && (
          <GenerateInfoItem
            name={'average duration'}
            value={avgDurationMs !== null ? `${avgDurationMs.toLocaleString()} ms` : '-'}
            icon="⏱️"
            loading={loading}
          />
        )}
        <GenerateInfoItem
          name={'first token latency'}
          value={info ? `${info.firstTokenLatency.toLocaleString()} ms` : '-'}
          icon="⚡"
          loading={loading}
        />
        {avgFirstTokenLatencyMs !== undefined && (
          <GenerateInfoItem
            name={'average first token latency'}
            value={avgFirstTokenLatencyMs !== null ? `${avgFirstTokenLatencyMs.toLocaleString()} ms` : '-'}
            icon="⚡"
            loading={loading}
          />
        )}
        <GenerateInfoItem
          name={'prompt tokens'}
          value={totalInputTokens !== undefined ? `${totalInputTokens.toLocaleString()}` : '-'}
          icon="📥"
          loading={loading}
        />
        {(inputCachedTokens > 0 || loading) && (
          <GenerateInfoItem
            name={'prompt tokens (cached)'}
            value={`${inputCachedTokens.toLocaleString()}`}
            icon="♻️"
            loading={loading}
          />
        )}
        <GenerateInfoItem
          name={'response tokens'}
          value={info ? `${(info.outputTokens - info.reasoningTokens).toLocaleString()}` : '-'}
          icon="📤"
          loading={loading}
        />
        {info && info.reasoningTokens > 0 && (
          <GenerateInfoItem
            name={'reasoning tokens'}
            value={`${info.reasoningTokens.toLocaleString()}`}
            icon="🧠"
            loading={loading}
          />
        )}
        <GenerateInfoItem
          name={'response speed'}
          value={info && info.duration ? `${toFixed((info.outputTokens / info.duration) * 1000)} token/s` : '-'}
          icon="🚀"
          loading={loading}
        />
        {(showInputTotalCost || showInputCachedCost || showOutputCost) && (
          <div className="pt-1.5 mt-1.5 border-t space-y-0.5">
            {showInputTotalCost && (
              <GenerateInfoItem
                name={'Input cost'}
                value={info ? '￥' + formatNumberAsMoney(+totalInputPrice, 6) : '-'}
                icon="💰"
                loading={loading}
              />
            )}
            {showInputCachedCost && (
              <GenerateInfoItem
                name={'Input cost (cached)'}
                value={info ? '￥' + formatNumberAsMoney(+inputCachedPrice, 6) : '-'}
                icon="♻️"
                loading={loading}
              />
            )}
            {showOutputCost && (
              <GenerateInfoItem
                name={'Response cost'}
                value={info ? '￥' + formatNumberAsMoney(+outputPrice, 6) : '-'}
                icon="💵"
                loading={loading}
              />
            )}
            <GenerateInfoItem
              name={'total cost'}
              value={info ? '￥' + formatNumberAsMoney(totalCost, 6) : '-'}
              icon="💳"
              loading={loading}
            />
          </div>
        )}
      </div>
    </div>
  );
};

export default GenerateInfoPopoverContent;
