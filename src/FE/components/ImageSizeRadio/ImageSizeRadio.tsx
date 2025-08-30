import { FC, useState, useEffect } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconSettings } from '../Icons';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';
import { cn } from '@/lib/utils';

interface Props {
  value?: string;
  onValueChange: (value: string) => void;
}

const ImageSizeRadio: FC<Props> = ({
  value = '0',
  onValueChange,
}) => {
  const { t } = useTranslation();
  
  // 判断是否为自定义模式（非默认值）
  const isCustom = value !== '0';
  const [isExpanded, setIsExpanded] = useState(isCustom);

  // 同步isExpanded状态与value
  useEffect(() => {
    setIsExpanded(value !== '0');
  }, [value]);

  const handleToggleMode = () => {
    if (isExpanded) {
      // 切换到默认模式
      onValueChange('0');
      setIsExpanded(false);
    } else {
      // 切换到自定义模式，默认选择第一个自定义选项
      onValueChange('1');
      setIsExpanded(true);
    }
  };

  const getSizeLabel = (value: string) => {
    switch (value) {
      case '1':
        return '1024×1024';
      case '2':
        return '1536×1024';
      case '3':
        return '1024×1536';
      default:
        return t('Default');
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-between text-sm">
        <div className="flex gap-1 items-center">
          <IconSettings />
          {t('Image Size')}
        </div>
        <div className="text-gray-600 cursor-pointer" onClick={handleToggleMode}>
          {isExpanded ? t('Custom') : t('Default')}
        </div>
      </div>
      <div className={cn('hidden', isExpanded && 'flex justify-between gap-2')}>
        <RadioGroup
          className="flex gap-4"
          value={value}
          onValueChange={onValueChange}
        >
          <div className="flex items-center space-x-2">
            <RadioGroupItem value="1" id="size-1024x1024" />
            <Label className="text-base cursor-pointer" htmlFor="size-1024x1024">
              1024×1024
            </Label>
          </div>
          <div className="flex items-center space-x-2">
            <RadioGroupItem value="2" id="size-1536x1024" />
            <Label className="text-base cursor-pointer" htmlFor="size-1536x1024">
              1536×1024
            </Label>
          </div>
          <div className="flex items-center space-x-2">
            <RadioGroupItem value="3" id="size-1024x1536" />
            <Label className="text-base cursor-pointer" htmlFor="size-1024x1536">
              1024×1536
            </Label>
          </div>
        </RadioGroup>
      </div>
    </div>
  );
};

export default ImageSizeRadio;
