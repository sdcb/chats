import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconSettings } from '../Icons';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';

interface Props {
  value?: string;
  onValueChange: (value: string) => void;
}

const ImageSizeRadio: FC<Props> = ({
  value = '0',
  onValueChange,
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex justify-between items-center">
      <label
        className={
          'mb-2 text-left dark:text-neutral-400 flex gap-1 items-center'
        }
      >
        <IconSettings />
        {t('Image Size')}
      </label>

      <RadioGroup
        className="flex gap-4"
        defaultValue={'0'}
        value={value}
        onValueChange={onValueChange}
      >
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="0" id="default-size" />
          <Label className="text-base" htmlFor="default-size">
            {t('Default')}
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="1" id="size-1024x1024" />
          <Label className="text-base" htmlFor="size-1024x1024">
            1024×1024
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="2" id="size-1536x1024" />
          <Label className="text-base" htmlFor="size-1536x1024">
            1536×1024
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="3" id="size-1024x1536" />
          <Label className="text-base" htmlFor="size-1024x1536">
            1024×1536
          </Label>
        </div>
      </RadioGroup>
    </div>
  );
};

export default ImageSizeRadio;
