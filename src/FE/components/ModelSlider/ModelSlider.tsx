import { FC, useEffect, useState } from 'react';

import { Slider } from '@/components/ui/slider';

import { IconInfo } from '../Icons';
import Tips from '../Tips/Tips';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

interface Props {
  label: string;
  labelClassName?: string;
  defaultValue: number;
  min: number;
  max: number;
  tipsContent?: string;
  onChangeValue: (value: number) => void;
}

const ModelSlider: FC<Props> = ({
  label,
  labelClassName,
  defaultValue,
  min,
  max,
  tipsContent,
  onChangeValue,
}) => {
  const [value, setValue] = useState(defaultValue);
  const handleChangeValue = (value: number[]) => {
    const newValue = value[0];
    setValue(newValue);
    onChangeValue(newValue);
  };

  useEffect(() => {
    setValue(defaultValue);
  }, [defaultValue]);

  return (
    <div className="flex flex-col">
      <label
        className={cn(
          'text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center',
          labelClassName,
        )}
      >
        {label}
        {tipsContent && (
          <Tips
            delayDuration={100}
            trigger={
              <Button variant="ghost" className="p-0 m-0 h-auto">
                <IconInfo size={20} />
              </Button>
            }
            content={tipsContent}
          />
        )}
      </label>
      <span className="text-center text-neutral-900 dark:text-neutral-100">
        {value}
      </span>
      <Slider
        className="cursor-pointer"
        min={min}
        max={max}
        step={1}
        value={[value]}
        onValueChange={handleChangeValue}
      />
    </div>
  );
};

export default ModelSlider;
