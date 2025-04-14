import React from 'react';

import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { IconDesktop, IconMoon, IconSun } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

const GenerateTab = () => {
  const { t } = useTranslation();
  const { theme, setTheme } = useTheme();

  return (
    <div className="w-full">
      <Card className="border-none">
        <CardContent className="pt-6">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <div className="font-medium min-w-[80px]">{t('Theme')}</div>
            <div className="flex flex-wrap gap-2 max-w-full overflow-x-auto py-1">
              <Button
                variant={theme === 'system' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setTheme('system')}
                className="flex items-center shrink-0"
              >
                <IconDesktop size={16} className="mr-1" />
                {t('System')}
              </Button>
              <Button
                variant={theme === 'light' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setTheme('light')}
                className="flex items-center shrink-0"
              >
                <IconSun size={16} className="mr-1" />
                {t('Light')}
              </Button>
              <Button
                variant={theme === 'dark' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setTheme('dark')}
                className="flex items-center shrink-0"
              >
                <IconMoon size={16} className="mr-1" />
                {t('Dark')}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default GenerateTab;
