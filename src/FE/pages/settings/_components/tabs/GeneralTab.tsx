import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import { useTheme } from 'next-themes';
import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { clearUserInfo, clearUserSession, getLoginUrl } from '@/utils/user';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Form, FormField } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';
import { Label } from '@/components/ui/label';
import { IconMoon, IconSun } from '@/components/Icons';

import { changeUserPassword } from '@/apis/clientApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

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
                <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4 mr-1" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
                  <line x1="8" y1="21" x2="16" y2="21"></line>
                  <line x1="12" y1="17" x2="12" y2="21"></line>
                </svg>
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
