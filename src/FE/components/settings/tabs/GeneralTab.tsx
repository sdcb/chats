import React, { useEffect, useState } from 'react';

import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';

import { IconDesktop, IconMoon, IconSun } from '@/components/Icons';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

import { getUserBalanceOnly } from '@/apis/clientApis';

interface SegmentedControlOption<T extends string> {
  value: T;
  label: string;
  icon?: React.ReactNode;
}

interface SegmentedControlProps<T extends string> {
  options: SegmentedControlOption<T>[];
  value: T | undefined;
  onChange: (value: T) => void;
  className?: string;
}

function SegmentedControl<T extends string>({
  options,
  value,
  onChange,
  className,
}: SegmentedControlProps<T>) {
  return (
    <div
      className={cn(
        'inline-flex items-center rounded-lg bg-muted p-1 gap-1',
        className
      )}
    >
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          onClick={() => onChange(option.value)}
          className={cn(
            'inline-flex items-center justify-center gap-2 px-3 py-2 text-sm font-medium rounded-md transition-all duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            value === option.value
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground hover:bg-background/50'
          )}
        >
          {option.icon && <span className="shrink-0">{option.icon}</span>}
          <span>{option.label}</span>
        </button>
      ))}
    </div>
  );
}

const GeneralTab = () => {
  const { t, language, changeLanguage } = useTranslation();
  const { theme, setTheme } = useTheme();

  const [userBalance, setUserBalance] = useState(0);

  useEffect(() => {
    getUserBalanceOnly().then((data) => setUserBalance(data));
  }, []);

  const themeOptions: SegmentedControlOption<string>[] = [
    {
      value: 'system',
      label: t('System'),
      icon: <IconDesktop size={16} />,
    },
    {
      value: 'light',
      label: t('Light'),
      icon: <IconSun size={16} />,
    },
    {
      value: 'dark',
      label: t('Dark'),
      icon: <IconMoon size={16} />,
    },
  ];

  const languageOptions: SegmentedControlOption<string>[] = [
    {
      value: 'zh-CN',
      label: '简体中文',
      icon: <span className="text-base">🇨🇳</span>,
    },
    {
      value: 'en',
      label: 'English',
      icon: <span className="text-base">🇺🇸</span>,
    },
  ];

  return (
    <div className="w-full">
      <Card className="border-none">
        <CardContent className="pt-6 space-y-3">
          {/* 余额 */}
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-muted-foreground">
              {t('Account balance')}
            </span>
            <span className="text-base font-semibold tabular-nums">
              ￥{toFixed(+(userBalance || 0))}
            </span>
          </div>

          {/* 主题 */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <span className="text-sm font-medium text-muted-foreground">
              {t('Theme')}
            </span>
            <SegmentedControl
              options={themeOptions}
              value={theme}
              onChange={setTheme}
            />
          </div>

          {/* 语言 */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <span className="text-sm font-medium text-muted-foreground">
              {t('Language')}
            </span>
            <SegmentedControl
              options={languageOptions}
              value={language}
              onChange={changeLanguage}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default GeneralTab;
