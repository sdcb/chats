import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import { PropsMessage } from '@/types/components/chat';

import { IconInfo } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';

interface Props {
  hidden?: boolean;
  message: PropsMessage;
}

export const GenerateInformationAction = (props: Props) => {
  const { t } = useTranslation();
  const { message, hidden } = props;

  const GenerateInformation = (props: { name: string; value: string }) => {
    const { name, value } = props;
    return (
      <Label key={name} className="text-xs">
        {t(name)}
        {': '}
        {value}
      </Label>
    );
  };

  const Render = () => {
    return (
      <Tips
        side="bottom"
        trigger={
          <Button
            variant="ghost"
            className="p-1 m-0 h-7 w-7"
            onClick={(e) => {
              e.stopPropagation();
            }}
          >
            <IconInfo />
          </Button>
        }
        content={
          <div className="w-50">
            <div className="grid gap-4">
              <div className="pt-1 pb-2">
                <Label className="font-medium">
                  {t('Generate information')}
                </Label>
              </div>
            </div>
            <div className="grid">
              <div className="grid grid-cols-1 items-center">
                <GenerateInformation
                  name={'total duration'}
                  value={message?.duration.toLocaleString() + 'ms'}
                />
                <GenerateInformation
                  name={'first token latency'}
                  value={message?.firstTokenLatency.toLocaleString() + 'ms'}
                />
                <GenerateInformation
                  name={'prompt tokens'}
                  value={`${message.inputTokens.toLocaleString()}`}
                />
                <GenerateInformation
                  name={'response tokens'}
                  value={`${(
                    message.outputTokens - message.reasoningTokens
                  ).toLocaleString()}`}
                />
                {!!message.reasoningTokens && (
                  <GenerateInformation
                    name={'reasoning tokens'}
                    value={`${message.reasoningTokens.toLocaleString()}`}
                  />
                )}
                <GenerateInformation
                  name={'response speed'}
                  value={
                    message?.duration
                      ? (
                          ((message.outputTokens - message.reasoningTokens) /
                            (message?.duration || 0)) *
                          1000
                        ).toFixed(2) + ' token/s'
                      : '-'
                  }
                />
                {message.inputPrice > 0 && (
                  <GenerateInformation
                    name={'prompt cost'}
                    value={'￥' + formatNumberAsMoney(+message.inputPrice, 6)}
                  />
                )}
                {message.outputPrice > 0 && (
                  <GenerateInformation
                    name={'response cost'}
                    value={'￥' + formatNumberAsMoney(+message.outputPrice, 6)}
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
                />
              </div>
            </div>
          </div>
        }
      />
    );
  };

  return <>{!hidden && Render()}</>;
};

export default GenerateInformationAction;
