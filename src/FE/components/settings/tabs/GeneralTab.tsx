import React, { useEffect, useState } from 'react';

import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';

import { IconDesktop, IconMoon, IconSun } from '@/components/Icons';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

import { getUserBalanceOnly } from '@/apis/clientApis';

const GeneralTab = () => {
  const { t, language, changeLanguage } = useTranslation();
  const { theme, setTheme } = useTheme();

  const [userBalance, setUserBalance] = useState(0);

  useEffect(() => {
    getUserBalanceOnly().then((data) => setUserBalance(data));
  }, []);

  return (
    <div className="w-full">
      <Card className="border-none">
        <CardContent className="pt-6 gap-4 flex flex-col h-full">
          <div className="flex min-h-10 flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <div className="font-medium min-w-[80px]">
              {t('Account balance')}
            </div>
            <div className="max-w-xs w-full">
              ￥{toFixed(+(userBalance || 0))}
            </div>
          </div>

          <div className="flex min-h-10 flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
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

          <div className="flex min-h-10 flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
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
