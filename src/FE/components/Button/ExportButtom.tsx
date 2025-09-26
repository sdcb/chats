import React from 'react';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';

import { IconArrowDown } from '@/components/Icons';
import { Button } from '@/components/ui/button';

interface ExportProps {
  exportUrl: string;
  params: Record<string, any>;
  fileName?: string;
  buttonText?: string;
  className?: string;
  disabled?: boolean;
}

const ExportButton: React.FC<ExportProps> = ({
  exportUrl,
  params,
  buttonText,
  className,
  disabled = false,
}) => {
  const { t } = useTranslation();

  const handleExport = () => {
    const form = document.createElement('form');
    form.method = 'GET';
    form.action = getApiUrl() + exportUrl;
    form.target = '_blank';

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = key;
        input.value = value.toString();
        form.appendChild(input);
      }
    });

    document.body.appendChild(form);
    form.submit();
    document.body.removeChild(form);
  };

  // 如果没有提供buttonText，显示为图标按钮
  if (!buttonText) {
    return (
      <Button 
        variant="ghost" 
        size="icon"
        onClick={handleExport} 
        className={className}
        disabled={disabled}
        title={t('Export to Excel')}
      >
        <IconArrowDown size={18} />
      </Button>
    );
  }

  // 如果提供了buttonText，显示为普通文本按钮
  return (
    <Button 
      variant="default" 
      onClick={handleExport} 
      className={className}
      disabled={disabled}
    >
      {buttonText}
    </Button>
  );
};

export default ExportButton;
