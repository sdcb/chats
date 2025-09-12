import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Copy } from 'lucide-react';
import toast from 'react-hot-toast';
import { z } from 'zod';

import useTranslation from '@/hooks/useTranslation';
import { GetConfigsResult } from '@/types/adminApis';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { 
  Form, 
  FormControl,
  FormField, 
  FormItem, 
  FormLabel, 
  FormMessage 
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';

import { 
  SiteInfoFormData, 
  useConfigManager, 
  compareSiteInfoData 
} from './shared';

interface SiteInfoConfigProps {
  configs: GetConfigsResult[];
  onConfigsUpdate: () => void;
}

// 网站信息配置解析函数
const parseSiteInfoConfig = (value: string): SiteInfoFormData => {
  const data = JSON.parse(value);
  return {
    customizedLine1: data.customizedLine1 || '',
    customizedLine2: data.customizedLine2 || '',
  };
};

export default function SiteInfoConfig({ configs, onConfigsUpdate }: SiteInfoConfigProps) {
  const { t } = useTranslation();
  const [forceUpdateTrigger, setForceUpdateTrigger] = useState({});
  
  const defaultData: SiteInfoFormData = {
    customizedLine1: '',
    customizedLine2: '',
  };

  const configManager = useConfigManager(
    'siteInfo',
    defaultData,
    parseSiteInfoConfig
  );

  // 创建网站信息配置的Schema
  const siteInfoSchema = z.object({
    customizedLine1: z.string(),
    customizedLine2: z.string(),
  });

  const form = useForm<SiteInfoFormData>({
    resolver: zodResolver(siteInfoSchema),
    defaultValues: defaultData,
  });

  // 自定义的变化检测函数
  const hasChanges = () => {
    const configExists = configs.find(item => item.key === 'siteInfo');
    
    // 如果enabled=false且配置不存在，不能保存（防止二次删除）
    if (!configManager.enabled && !configExists) {
      return false;
    }
    
    // 检查enabled状态是否有变化
    const enabledChanged = configManager.enabled !== configManager.initialState.enabled;
    
    // 检查表单数据是否有变化
    const currentData = form.getValues();
    const dataChanged = !compareSiteInfoData(currentData, configManager.initialState.data);
    
    // enabled=true时总是可以保存，或者有数据变化时可以保存
    return configManager.enabled || enabledChanged || dataChanged;
  };

  // 监听表单变化，触发重新渲染
  useEffect(() => {
    const subscription = form.watch((value) => {
      configManager.setMemoryData({
        customizedLine1: value.customizedLine1 || '',
        customizedLine2: value.customizedLine2 || '',
      });
      setForceUpdateTrigger({}); // 触发重新渲染以更新保存按钮显示
    });

    return () => subscription.unsubscribe();
  }, [form, configManager]);

  // 监听enabled状态变化
  useEffect(() => {
    setForceUpdateTrigger({});
  }, [configManager.enabled]);

  // 初始化表单数据
  useEffect(() => {
    const formData = configManager.initializeConfig(configs, defaultData);
    form.setValue('customizedLine1', formData.customizedLine1);
    form.setValue('customizedLine2', formData.customizedLine2);
  }, [configs]); // 移除 form 和 configManager 的依赖

  const onSubmit = async (values: SiteInfoFormData) => {
    await configManager.saveConfig(
      values,
      'Site information configuration',
      t,
      onConfigsUpdate
    );
  };

  const copyConfigAsJson = () => {
    let dataToExport = {};
    
    if (configManager.enabled) {
      const config = configs.find(item => item.key === 'siteInfo');
      if (config) {
        try {
          dataToExport = JSON.parse(config.value);
        } catch (e) {
          console.error('Failed to parse config:', e);
          return;
        }
      }
    } else {
      dataToExport = configManager.memoryData;
    }
    
    const jsonString = JSON.stringify(dataToExport, null, 2);
    navigator.clipboard.writeText(jsonString).then(() => {
      toast.success(t('Copied to clipboard'));
    }).catch(() => {
      toast.error(t('Failed to copy'));
    });
  };

  return (
    <Card className="w-full">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-6">
        <div className="flex items-center space-x-3">
          <CardTitle className="text-lg font-medium">
            {t('Site Information Configuration')}
          </CardTitle>
          <Button
            variant="outline"
            size="sm"
            onClick={copyConfigAsJson}
            className="h-8 w-8 p-0"
          >
            <Copy className="h-4 w-4" />
          </Button>
        </div>
        <div className="flex items-center space-x-2">
          <span className="text-sm">{configManager.enabled ? t('Enabled') : t('Disabled')}</span>
          <Switch
            checked={configManager.enabled}
            onCheckedChange={configManager.setEnabled}
          />
        </div>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            <FormField
              control={form.control}
              name="customizedLine1"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('Customized Line 1')}</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('Enter customized line 1 content')}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="customizedLine2"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('Customized Line 2')}</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('Enter customized line 2 content')}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {hasChanges() && (
              <div className="flex justify-end">
                <Button 
                  type="submit" 
                  disabled={configManager.saving}
                  className="min-w-24"
                >
                  {configManager.saving ? t('Saving...') : t('Save')}
                </Button>
              </div>
            )}
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}