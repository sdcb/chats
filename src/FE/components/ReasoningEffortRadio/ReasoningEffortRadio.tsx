import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ReasoningEffortType } from '@/types/model';

import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';

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
    <div className="flex flex-col">
      <label
        className={
          'mb-3 text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center'
        }
      >
        {t('Reasoning Effort')}
      </label>

      <RadioGroup
        className="flex gap-6"
        defaultValue={'medium'}
        value={value}
        onValueChange={onValueChange}
      >
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="low" id="low" />
          <Label htmlFor="low">{t('Low')}</Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="medium" id="medium" />
          <Label htmlFor="medium">{t('Medium')}</Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="high" id="high" />
          <Label htmlFor="high">{t('High')}</Label>
        </div>
      </RadioGroup>
    </div>
  );
};

export default ReasoningEffortRadio;
