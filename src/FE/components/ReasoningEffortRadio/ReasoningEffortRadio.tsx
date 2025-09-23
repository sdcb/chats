import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconReasoning } from '../Icons';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';

interface Props {
  value?: string;
  onValueChange: (value: string) => void;
}

const ReasoningEffortRadio: FC<Props> = ({
  value = 'medium',
  onValueChange,
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex justify-between items-center">
      <label
        className={
          'text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center'
        }
      >
        <IconReasoning size={16} />
        {t('Reasoning Effort')}
      </label>

      <RadioGroup
        className="flex gap-4"
        defaultValue={'0'}
        value={value}
        onValueChange={onValueChange}
      >
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="0" id="default" />
          <Label className="text-base" htmlFor="default">
            {t('Default')}
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="1" id="low" />
          <Label className="text-base" htmlFor="low">
            {t('Low')}
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="2" id="medium" />
          <Label className="text-base" htmlFor="medium">
            {t('Medium')}
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="3" id="high" />
          <Label className="text-base" htmlFor="high">
            {t('High')}
          </Label>
        </div>
      </RadioGroup>
    </div>
  );
};

export default ReasoningEffortRadio;
