import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconReasoning } from '../Icons';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';

interface Props {
  value?: string | null;
  onValueChange: (value: string) => void;
  availableOptions: string[];
}

const ReasoningEffortRadio: FC<Props> = ({
  value = null,
  onValueChange,
  availableOptions,
}) => {
  const { t } = useTranslation();
  const defaultValue = '__default__';

  const optionValues = [
    defaultValue,
    ...availableOptions,
    ...(value && value.trim() !== '' ? [value] : []),
  ].filter((optionValue, index, items) => items.indexOf(optionValue) === index);

  const renderedOptions = optionValues.map(optionValue => ({
    value: optionValue,
    id: optionValue === defaultValue ? 'default' : `reasoning-effort-${optionValue}`,
    label: optionValue === defaultValue ? t('Default') : t(optionValue),
  }));

  return (
    <div className="flex justify-between items-center">
      <label
        className={
          'text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center'
        }
      >
        <IconReasoning size={20} />
        {t('Reasoning Effort')}
      </label>

      <RadioGroup
        className="flex gap-4"
        value={value ?? defaultValue}
        onValueChange={(nextValue) => {
          onValueChange(nextValue === defaultValue ? '' : nextValue);
        }}
      >
        {renderedOptions.map((option) => (
          <div key={option.value} className="flex items-center space-x-2">
            <RadioGroupItem value={option.value} id={option.id} />
            <Label className="text-base" htmlFor={option.id}>
              {option.label}
            </Label>
          </div>
        ))}
      </RadioGroup>
    </div>
  );
};

export default ReasoningEffortRadio;
