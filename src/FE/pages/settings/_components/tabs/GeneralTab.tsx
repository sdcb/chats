import React from 'react';

import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { IconDesktop, IconMoon, IconSun } from '@/components/Icons';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

const GeneralTab = () => {
  const { t, language, changeLanguage } = useTranslation();
  const { theme, setTheme } = useTheme();

  return (
    <div className="w-full">
      <Card className="border-none">
        <CardContent className="pt-6">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <div className="font-medium min-w-[80px]">{t('Theme')}</div>
            <div className="max-w-xs w-full">
              <Select value={theme} onValueChange={(value) => setTheme(value)}>
                <SelectTrigger className="w-full flex items-center p-0 m-0 leading-[0px]">
                  <SelectValue placeholder={t('Select Theme')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="system">
                    <div className="flex items-center">
                      <IconDesktop size={16} className="mr-2" />
                      {t('System')}
                    </div>
                  </SelectItem>
                  <SelectItem value="light">
                    <div className="flex items-center">
                      <IconSun size={16} className="mr-2" />
                      {t('Light')}
                    </div>
                  </SelectItem>
                  <SelectItem value="dark">
                    <div className="flex items-center">
                      <IconMoon size={16} className="mr-2" />
                      {t('Dark')}
                    </div>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 mt-4">
            <div className="font-medium min-w-[80px]">{t('Language')}</div>
            <div className="max-w-xs w-full">
              <Select
                value={language}
                onValueChange={(value) => changeLanguage(value)}
              >
                <SelectTrigger className="w-full">
                  <SelectValue placeholder={t('Select Language')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="zh-CN">{t('Chinese')}</SelectItem>
                  <SelectItem value="en">{t('English')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default GeneralTab;
