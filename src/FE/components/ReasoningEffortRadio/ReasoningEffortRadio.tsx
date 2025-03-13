import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ReasoningEffortType } from '@/types/model';

import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';
import { IconReasoning } from '../Icons';

interface Props {
  value?: ReasoningEffortType;
  onValueChange: (value: ReasoningEffortType) => void;
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
          'mb-3 text-left  dark:text-neutral-400 flex gap-1 items-center'
        }
      >
        <IconReasoning />
        {t('Reasoning Effort')}
      </label>

      <RadioGroup
        className="flex gap-4"
        defaultValue={'medium'}
        value={value}
        onValueChange={onValueChange}
      >
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="low" id="low" />
          <Label className="text-base" htmlFor="low">
            {t('Low')}
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="medium" id="medium" />
          <Label className="text-base" htmlFor="medium">
            {t('Medium')}
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="high" id="high" />
          <Label className="text-base" htmlFor="high">
            {t('High')}
          </Label>
        </div>
      </RadioGroup>
    </div>
  );
};

export default ReasoningEffortRadio;
