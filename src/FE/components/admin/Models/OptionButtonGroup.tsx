import React from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';

interface Option {
  label: string;
  value: string;
}

interface OptionButtonGroupProps {
  label: string;
  options: Option[];
  value: string;
  onChange: (value: string) => void;
}

/**
 * 选项按钮组组件
 * 用于显示多个可选项作为按钮组，用户点击选择
 */
const OptionButtonGroup: React.FC<OptionButtonGroupProps> = ({
  label,
  options,
  value,
  onChange,
}) => {
  // 将逗号分隔的字符串转为数组
  const selectedValues = value ? value.split(',').map(v => v.trim()) : [];

  const toggleOption = (optionValue: string) => {
    let newValues: string[];
    
    if (selectedValues.includes(optionValue)) {
      // 如果已选中，则取消选中
      newValues = selectedValues.filter(v => v !== optionValue);
    } else {
      // 如果未选中，则添加选中
      newValues = [...selectedValues, optionValue];
    }
    
    // 如果是数字值，按数字排序；否则保持选项顺序
    const firstOption = options[0]?.value;
    if (firstOption && !isNaN(Number(firstOption))) {
      newValues.sort((a, b) => parseInt(a) - parseInt(b));
    } else {
      // 按照 options 中定义的顺序排序
      newValues.sort((a, b) => {
        const indexA = options.findIndex(opt => opt.value === a);
        const indexB = options.findIndex(opt => opt.value === b);
        return indexA - indexB;
      });
    }
    
    // 转回逗号分隔的字符串
    onChange(newValues.join(', '));
  };

  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <div className="flex gap-2">
        {options.map((option) => {
          const isSelected = selectedValues.includes(option.value);
          return (
            <Button
              key={option.value}
              type="button"
              variant={isSelected ? 'default' : 'outline'}
              onClick={() => toggleOption(option.value)}
              className="flex-1"
            >
              {option.label}
            </Button>
          );
        })}
      </div>
    </div>
  );
};

export default OptionButtonGroup;
