import React from 'react';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';

import { Button } from '@/components/ui/button';

interface ExportProps {
  exportUrl: string;
  params: Record<string, any>;
  fileName?: string;
  buttonText?: string;
  className?: string;
}

const ExportButton: React.FC<ExportProps> = ({
  exportUrl,
  params,
  buttonText,
  className,
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

  return (
    <Button variant="outline" onClick={handleExport} className={className}>
      {buttonText || t('Export')}
    </Button>
  );
};

export default ExportButton;
