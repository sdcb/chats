import { FC } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconReasoning } from '../Icons';
import { Label } from '../ui/label';
import { RadioGroup, RadioGroupItem } from '../ui/radio-group';

interface Props {
  value?: string;
  onValueChange: (value: string) => void;
  availableOptions: number[]; // 可用的选项值
}

const ReasoningEffortRadio: FC<Props> = ({
  value = '0',
  onValueChange,
  availableOptions,
}) => {
  const { t } = useTranslation();

  // 定义所有可能的选项
  const allOptions = [
    { value: '0', id: 'default', label: t('Default') },
    { value: '1', id: 'minimal', label: t('Minimal') },
    { value: '2', id: 'low', label: t('Low') },
    { value: '3', id: 'medium', label: t('Medium') },
    { value: '4', id: 'high', label: t('High') },
  ];

  // 根据后端返回的选项过滤
  // Default(0) 总是显示，其他选项根据 availableOptions 决定
  const filteredOptions = allOptions.filter(option => {
    if (option.value === '0') return true; // Default 总是显示
    return availableOptions.includes(parseInt(option.value));
  });

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
        {filteredOptions.map((option) => (
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
