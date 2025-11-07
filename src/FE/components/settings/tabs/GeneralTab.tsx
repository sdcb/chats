import React, { useEffect, useState } from 'react';

import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';

import { IconDesktop, IconMoon, IconSun } from '@/components/Icons';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

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
              <div className="grid grid-cols-3 gap-2">
                <button
                  type="button"
                  onClick={() => setTheme('system')}
                  className={cn(
                    'flex flex-col items-center justify-center p-3 rounded-lg border-2 transition-all hover:border-primary/50',
                    theme === 'system'
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-background'
                  )}
                >
                  <IconDesktop size={24} className="mb-1" />
                  <span className="text-xs">{t('System')}</span>
                </button>
                <button
                  type="button"
                  onClick={() => setTheme('light')}
                  className={cn(
                    'flex flex-col items-center justify-center p-3 rounded-lg border-2 transition-all hover:border-primary/50',
                    theme === 'light'
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-background'
                  )}
                >
                  <IconSun size={24} className="mb-1" />
                  <span className="text-xs">{t('Light')}</span>
                </button>
                <button
                  type="button"
                  onClick={() => setTheme('dark')}
                  className={cn(
                    'flex flex-col items-center justify-center p-3 rounded-lg border-2 transition-all hover:border-primary/50',
                    theme === 'dark'
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-background'
                  )}
                >
                  <IconMoon size={24} className="mb-1" />
                  <span className="text-xs">{t('Dark')}</span>
                </button>
              </div>
            </div>
          </div>

          <div className="flex min-h-10 flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <div className="font-medium min-w-[80px]">{t('Language')}</div>
            <div className="max-w-xs w-full">
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => changeLanguage('zh-CN')}
                  className={cn(
                    'flex flex-col items-center justify-center p-3 rounded-lg border-2 transition-all hover:border-primary/50',
                    language === 'zh-CN'
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-background'
                  )}
                >
                  <span className="text-2xl mb-1">🇨🇳</span>
                  <span className="text-xs">简体中文</span>
                </button>
                <button
                  type="button"
                  onClick={() => changeLanguage('en')}
                  className={cn(
                    'flex flex-col items-center justify-center p-3 rounded-lg border-2 transition-all hover:border-primary/50',
                    language === 'en'
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-background'
                  )}
                >
                  <span className="text-2xl mb-1">🇺🇸</span>
                  <span className="text-xs">English</span>
                </button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default GeneralTab;
