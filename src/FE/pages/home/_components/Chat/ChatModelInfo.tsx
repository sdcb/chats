import { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import { ModelUsageDto } from '@/types/clientApis';

import { getModelUsage } from '@/apis/clientApis';
import { formatDate } from '@/utils/date';

const ChatModelInfo = (props: { modelId: number }) => {
  const { t } = useTranslation();

  const { modelId } = props;

  const [modelUsage, setModelUsage] = useState<ModelUsageDto>();

  useEffect(() => {
    if (modelId) {
      getModelUsage(modelId).then((res) => {
        setModelUsage(res);
      });
    }
  }, [modelId]);

  if (!modelUsage) {
    return <></>;
  }

  return (
    <div className="flex flex-col text-gray-600 text-sm h-5">
      <div className="flex items-center">
        {modelUsage.tokens === 0 && modelUsage.counts === 0 ? (
          <span>
            ￥{modelUsage.inputTokenPrice1M.toFixed(4)}/
            {modelUsage.outputTokenPrice1M.toFixed(4)} (1M tokens)
          </span>
        ) : (
          <div className="flex justify-between text-muted-foreground">
            <div className="flex gap-4">
              {+modelUsage.counts > 0 ? (
                <span>{modelUsage.counts}</span>
              ) : +modelUsage.tokens > 0 ? (
                <span>{formatNumberAsMoney(+modelUsage.tokens)}</span>
              ) : (
                <span>
                  ￥{modelUsage.inputTokenPrice1M.toFixed(4)}/
                  {modelUsage.outputTokenPrice1M.toFixed(4)} (1M tokens)
                </span>
              )}
            </div>
            <div className="flex justify-end">
              {modelUsage.isTerm ? (
                <></>
              ) : (
                <>
                  {formatDate(modelUsage.expires)} {` ${t('become due')}`}
                </>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default ChatModelInfo;
