import { FC, useState, useEffect } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconPhoto } from '../Icons';
import { Button } from '../ui/button';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';
import { cn } from '@/lib/utils';

interface Props {
  value?: string | null;
  onValueChange: (value: string | null) => void;
  supportedSizes: string[]; // 支持的图片尺寸列表，如 ["1024x1024", "1792x1024"]
}

const ImageSizeRadio: FC<Props> = ({
  value = null,
  onValueChange,
  supportedSizes,
}) => {
  const { t } = useTranslation();
  
  // 判断是否为自定义模式（非 null 值）
  const isCustom = value !== null && value !== '';
  const [isExpanded, setIsExpanded] = useState(isCustom);

  // 同步isExpanded状态与value
  useEffect(() => {
    setIsExpanded(value !== null && value !== '');
  }, [value]);

  const handleToggleMode = () => {
    if (isExpanded) {
      // 切换到默认模式
      onValueChange(null);
      setIsExpanded(false);
    } else {
      // 切换到自定义模式，默认选择第一个自定义选项
      onValueChange(supportedSizes[0] || null);
      setIsExpanded(true);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-between">
        <div className="flex gap-1 items-center text-neutral-700 dark:text-neutral-400">
          <IconPhoto size={20} />
          {t('Image Size')}
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={handleToggleMode}
          className="h-6 px-2 text-sm"
        >
          {isExpanded ? t('Custom') : t('Default')}
        </Button>
      </div>
      <div className={cn('hidden', isExpanded && 'flex justify-end gap-2')}>
        <RadioGroup
          className="flex gap-3 flex-wrap"
          value={value || ''}
          onValueChange={(v) => onValueChange(v || null)}
        >
          {supportedSizes.map((size) => (
            <div key={size} className="flex items-center space-x-1.5">
              <RadioGroupItem value={size} id={`size-${size}`} />
              <Label className="text-sm cursor-pointer" htmlFor={`size-${size}`}>
                {size.replace('x', '×')}
              </Label>
            </div>
          ))}
        </RadioGroup>
      </div>
    </div>
  );
};

export default ImageSizeRadio;
