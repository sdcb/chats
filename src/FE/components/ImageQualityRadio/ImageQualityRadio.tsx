import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconReasoning } from '../Icons';
import { Button } from '../ui/button';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';

interface Props {
  value?: string | null;
  onValueChange: (value: string) => void;
  availableOptions: string[];
}

const ImageQualityRadio: FC<Props> = ({
  value = null,
  onValueChange,
  availableOptions,
}) => {
  const { t } = useTranslation();
  const allOptions = [
    { value: 'low', id: 'low', label: t('low') },
    { value: 'medium', id: 'medium', label: t('medium') },
    { value: 'high', id: 'high', label: t('high') },
  ];

  const filteredOptions = allOptions.filter(option => availableOptions.includes(option.value));

  return (
    <div className="flex flex-col gap-2">
      <div className="flex justify-between items-center">
      <label
        className={
          'text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center'
        }
      >
        <IconReasoning size={20} />
        {t('Image Quality')}
      </label>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onValueChange(value == null ? filteredOptions[0]?.value ?? '' : '')}
          className="h-6 px-2 text-sm"
        >
          {value == null ? t('Default') : t('Custom')}
        </Button>
      </div>

      {value != null && (
        <RadioGroup
          className="flex flex-wrap gap-4 px-2"
          value={value}
          onValueChange={onValueChange}
        >
          {filteredOptions.map((option) => (
            <div key={option.value} className="flex items-center space-x-2">
              <RadioGroupItem value={option.value} id={option.id} />
              <Label className="text-base" htmlFor={option.id}>
                {option.label}
              </Label>
            </div>
          ))}
        </RadioGroup>
      )}
    </div>
  );
};

export default ImageQualityRadio;
