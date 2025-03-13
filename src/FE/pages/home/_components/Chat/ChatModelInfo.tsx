import { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import { ModelUsageDto } from '@/types/clientApis';

import { getModelUsage } from '@/apis/clientApis';

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

  const getTitle = () => {
    if (modelUsage) {
      if (modelUsage.tokens === 0 && modelUsage.counts === 0) {
        return t('unit-price');
      } else if (+modelUsage.counts > 0) {
        return t('Remaining Chat Counts');
      } else if (+modelUsage.tokens > 0) {
        return t('Remaining Tokens');
      } else {
        return t('unit-price');
      }
    }
    return '';
  };

  if (!modelUsage) {
    return <></>;
  }

  return (
    <div className="flex flex-col text-gray-600 text-sm">
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
                  {new Date(modelUsage.expires).toLocaleDateString()}{' '}
                  {` ${t('become due')}`}
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
