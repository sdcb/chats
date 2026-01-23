import React, { useEffect, useState } from 'react';

import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';
import {
  ChatWindowStyle,
  DEFAULT_FONT_SIZE,
  getSettings,
  MAX_FONT_SIZE,
  MIN_FONT_SIZE,
  saveSettings,
} from '@/utils/settings';

import { IconDesktop, IconMoon, IconSun } from '@/components/Icons';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Slider } from '@/components/ui/slider';
import { Switch } from '@/components/ui/switch';
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
  const [fontSize, setFontSize] = useState(DEFAULT_FONT_SIZE);
  const [hideChatBackground, setHideChatBackground] = useState(false);
  const [hideInputAfterSend, setHideInputAfterSend] = useState(false);
  const [chatWindowStyle, setChatWindowStyle] = useState<ChatWindowStyle>('dialog');

  useEffect(() => {
    getUserBalanceOnly().then((data) => setUserBalance(data));
    // Load font size from settings
    const settings = getSettings();
    setFontSize(settings.fontSize ?? DEFAULT_FONT_SIZE);
    setHideChatBackground(settings.hideChatBackground ?? false);
    setHideInputAfterSend(settings.hideInputAfterSend ?? false);
    setChatWindowStyle(settings.chatWindowStyle ?? 'dialog');
  }, []);

  const handleFontSizeChange = (value: number) => {
    const clampedValue = Math.min(MAX_FONT_SIZE, Math.max(MIN_FONT_SIZE, value));
    setFontSize(clampedValue);
    const settings = getSettings();
    saveSettings({ ...settings, fontSize: clampedValue });
    // Apply font size to document
    document.documentElement.style.setProperty('--chat-font-size', `${clampedValue}px`);
  };

  const handleHideChatBackgroundChange = (checked: boolean) => {
    setHideChatBackground(checked);
    const settings = getSettings();
    saveSettings({ ...settings, hideChatBackground: checked });
    // Apply to document
    document.documentElement.setAttribute('data-hide-chat-background', checked ? 'true' : 'false');
  };

  const handleHideInputAfterSendChange = (checked: boolean) => {
    setHideInputAfterSend(checked);
    const settings = getSettings();
    saveSettings({ ...settings, hideInputAfterSend: checked });
  };

  const handleChatWindowStyleChange = (style: ChatWindowStyle) => {
    setChatWindowStyle(style);
    const settings = getSettings();
    saveSettings({ ...settings, chatWindowStyle: style });
    // Apply to document
    document.documentElement.setAttribute('data-chat-window-style', style);
  };

  // Apply font size on mount
  useEffect(() => {
    document.documentElement.style.setProperty('--chat-font-size', `${fontSize}px`);
  }, [fontSize]);

  // Apply hide chat background on mount
  useEffect(() => {
    document.documentElement.setAttribute('data-hide-chat-background', hideChatBackground ? 'true' : 'false');
  }, [hideChatBackground]);

  // Apply chat window style on mount
  useEffect(() => {
    document.documentElement.setAttribute('data-chat-window-style', chatWindowStyle);
  }, [chatWindowStyle]);

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

          {/* 聊天外观 */}
          <div className="flex flex-col gap-4 pt-4">
            <span className="text-sm font-bold">
              {t('Chat Appearance')}
            </span>

            {/* 聊天窗口样式 */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
              <span className="text-sm font-medium text-muted-foreground">
                {t('Chat Window Style')}
              </span>
              <div className="inline-flex items-stretch rounded-lg bg-muted p-1 gap-1">
                {/* 对话模式 */}
                <button
                  type="button"
                  onClick={() => handleChatWindowStyleChange('dialog')}
                  className={cn(
                    'flex flex-col items-center gap-1.5 px-3 py-2 rounded-md transition-all duration-200',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
                    chatWindowStyle === 'dialog'
                      ? 'bg-background text-foreground shadow-sm'
                      : 'text-muted-foreground hover:text-foreground hover:bg-background/50'
                  )}
                >
                  {/* 预览图 */}
                  <div className="w-24 h-14 rounded bg-muted/50 p-1.5 flex flex-col gap-1 justify-center">
                    <div className="flex items-center gap-1">
                      <div className="w-1 h-1 rounded-full bg-cyan-400"></div>
                      <div className="h-1 w-10 rounded bg-muted-foreground/30"></div>
                    </div>
                    <div className="flex items-center gap-1 pl-3">
                      <div className="w-1 h-1 rounded-full bg-amber-400"></div>
                      <div className="h-1 w-8 rounded bg-muted-foreground/30"></div>
                    </div>
                    <div className="flex items-center gap-1">
                      <div className="w-1 h-1 rounded-full bg-cyan-400"></div>
                      <div className="h-1 w-12 rounded bg-muted-foreground/30"></div>
                    </div>
                    <div className="flex items-center gap-1 pl-3">
                      <div className="w-1 h-1 rounded-full bg-amber-400"></div>
                      <div className="h-1 w-6 rounded bg-muted-foreground/30"></div>
                    </div>
                  </div>
                  <span className="text-xs">{t('Dialog Mode')}</span>
                </button>
                {/* 文档模式 */}
                <button
                  type="button"
                  onClick={() => handleChatWindowStyleChange('document')}
                  className={cn(
                    'flex flex-col items-center gap-1.5 px-3 py-2 rounded-md transition-all duration-200',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
                    chatWindowStyle === 'document'
                      ? 'bg-background text-foreground shadow-sm'
                      : 'text-muted-foreground hover:text-foreground hover:bg-background/50'
                  )}
                >
                  {/* 预览图 */}
                  <div className="w-24 h-14 rounded bg-muted/50 p-1.5 flex flex-col gap-1 justify-center">
                    <div className="h-1 w-14 rounded bg-muted-foreground/30"></div>
                    <div className="h-1 w-12 rounded bg-muted-foreground/30"></div>
                    <div className="h-1 w-16 rounded bg-muted-foreground/30"></div>
                    <div className="h-1 w-10 rounded bg-muted-foreground/30"></div>
                  </div>
                  <span className="text-xs">{t('Document Mode')}</span>
                </button>
              </div>
            </div>

            {/* 隐藏聊天背景 */}
            <div className="flex items-center justify-between">
              <div className="flex flex-col gap-1">
                <span className="text-sm font-medium text-muted-foreground">
                  {t('Hide Chat Background')}
                </span>
                <span className="text-xs text-muted-foreground/70">
                  {t('Make chat area transparent')}
                </span>
              </div>
              <Switch
                checked={hideChatBackground}
                onCheckedChange={handleHideChatBackgroundChange}
              />
            </div>

            {/* 发送后隐藏输入框 */}
            <div className="flex items-center justify-between">
              <div className="flex flex-col gap-1">
                <span className="text-sm font-medium text-muted-foreground">
                  {t('Hide Input After Send')}
                </span>
                <span className="text-xs text-muted-foreground/70">
                  {t('Hide input box after sending message')}
                </span>
              </div>
              <Switch
                checked={hideInputAfterSend}
                onCheckedChange={handleHideInputAfterSendChange}
              />
            </div>

            {/* 字体大小 */}
            <div className="flex flex-col gap-3">
              <div className="flex items-center justify-between">
                <div className="flex flex-col gap-1">
                  <span className="text-sm font-medium text-muted-foreground">
                    {t('Font Size')}
                  </span>
                  <span className="text-xs text-muted-foreground/70">
                    {t('Chat content font size')}
                  </span>
                </div>
                <Input
                  type="number"
                  min={MIN_FONT_SIZE}
                  max={MAX_FONT_SIZE}
                  value={fontSize}
                  onChange={(e) => {
                    const value = parseInt(e.target.value, 10);
                    if (!isNaN(value)) {
                      handleFontSizeChange(value);
                    }
                  }}
                  className="w-16 h-8 text-center"
                />
              </div>
              <div className="flex items-center gap-3">
                <span className="text-xs text-muted-foreground">A</span>
                <Slider
                  value={[fontSize]}
                  min={MIN_FONT_SIZE}
                  max={MAX_FONT_SIZE}
                  step={1}
                  onValueChange={([value]) => handleFontSizeChange(value)}
                  className="flex-1"
                />
                <span className="text-base text-muted-foreground">A</span>
                <span className="text-sm text-muted-foreground min-w-[40px] text-center">
                  {fontSize === DEFAULT_FONT_SIZE ? t('Standard') : ''}
                </span>
              </div>
              {/* 预览 */}
              <div className="mt-2 p-4 rounded-lg bg-muted/50 border">
                <div className="flex items-start gap-3 items-center">
                  <img
                    src="/icons/logo.png"
                    alt="logo"
                    className="w-8 h-8 rounded-full"
                  />
                  <p
                    style={{ fontSize: `${fontSize}px` }}
                    className="text-foreground leading-relaxed"
                  >
                    {t('Font size preview text')}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default GeneralTab;
