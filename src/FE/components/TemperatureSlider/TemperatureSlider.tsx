import { FC, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { Slider } from '@/components/ui/slider';

import { IconInfo } from '../Icons';
import Tips from '../Tips/Tips';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

interface Props {
  label: string;
  labelClassName?: string;
  defaultTemperature: number;
  min: number;
  max: number;
  onChangeTemperature: (temperature: number) => void;
}

const TemperatureSlider: FC<Props> = ({
  label,
  labelClassName,
  defaultTemperature,
  min,
  max,
  onChangeTemperature,
}) => {
  const [temperature, setTemperature] = useState(defaultTemperature);
  const { t } = useTranslation();
  const handleChange = (value: number[]) => {
    const newValue = value[0];
    setTemperature(newValue);
    onChangeTemperature(newValue);
  };

  useEffect(() => {
    setTemperature(defaultTemperature);
  }, [defaultTemperature]);

  return (
    <div className="flex flex-col">
      <label
        className={cn(
          'text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center',
          labelClassName,
        )}
      >
        {label}
        <Tips
          delayDuration={100}
          trigger={
            <Button variant="ghost" className="p-0 m-0 h-auto">
              <IconInfo size={18} />
            </Button>
          }
          content={t(
            'Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic.',
          )}
        />
      </label>
      <span className="text-center text-neutral-900 dark:text-neutral-100">
        {temperature.toFixed(2)}
      </span>
      <Slider
        className="cursor-pointer"
        min={min}
        max={max}
        step={0.01}
        value={[temperature]}
        onValueChange={handleChange}
      />
      <ul className="mt-2 flex justify-between">
        <span>{t('Precise')}</span>
        <span>{t('Neutral')}</span>
        <span>{t('Creative')}</span>
      </ul>
    </div>
  );
};

export default TemperatureSlider;
