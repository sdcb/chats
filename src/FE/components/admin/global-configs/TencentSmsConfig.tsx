import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Eye, EyeOff, Copy } from 'lucide-react';
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
  TencentSmsFormData, 
  useConfigManager, 
  compareTencentSmsData 
} from '@/utils/globalConfigsShared';

interface TencentSmsConfigProps {
  configs: GetConfigsResult[];
  onConfigsUpdate: () => void;
}

// 腾讯短信配置解析函数
const parseTencentSmsConfig = (value: string): TencentSmsFormData => {
  const data = JSON.parse(value);
  return {
    secretId: data.secretId || '',
    secretKey: data.secretKey || '',
    sdkAppId: data.sdkAppId || '',
    signName: data.signName || '',
    templateId: data.templateId || '',
  };
};

export default function TencentSmsConfig({ configs, onConfigsUpdate }: TencentSmsConfigProps) {
  const { t } = useTranslation();
  const [showSecretKey, setShowSecretKey] = useState(false);
  const [forceUpdateTrigger, setForceUpdateTrigger] = useState({});
  
  const defaultData: TencentSmsFormData = {
    secretId: '',
    secretKey: '',
    sdkAppId: '',
    signName: '',
    templateId: '',
  };

  const configManager = useConfigManager(
    'tencentSms',
    defaultData,
    parseTencentSmsConfig
  );

  // 创建腾讯短信配置的Schema
  const tencentSmsSchema = z.object({
    secretId: z.string().min(1, t('Secret ID is required')),
    secretKey: z.string().min(1, t('Secret Key is required')),
    sdkAppId: z.string().min(1, t('SDK App ID is required')).regex(/^\d+$/, t('SDK App ID must be a number')),
    signName: z.string().min(1, t('Sign Name is required')),
    templateId: z.string().min(1, t('Template ID is required')).regex(/^\d+$/, t('Template ID must be a number')),
  });

  const form = useForm<TencentSmsFormData>({
    resolver: zodResolver(tencentSmsSchema),
    defaultValues: defaultData,
  });

  // 自定义的变化检测函数
  const hasChanges = () => {
    const configExists = configs.find(item => item.key === 'tencentSms');
    
    // 如果enabled=false且配置不存在，不能保存（防止二次删除）
    if (!configManager.enabled && !configExists) {
      return false;
    }
    
    // 检查enabled状态是否有变化
    const enabledChanged = configManager.enabled !== configManager.initialState.enabled;
    
    // 检查表单数据是否有变化
    const currentData = form.getValues();
    const dataChanged = !compareTencentSmsData(currentData, configManager.initialState.data);
    
    // enabled=true时总是可以保存，或者有数据变化时可以保存
    return configManager.enabled || enabledChanged || dataChanged;
  };

  // 监听表单变化，触发重新渲染
  useEffect(() => {
    const subscription = form.watch((value) => {
      configManager.setMemoryData({
        secretId: value.secretId || '',
        secretKey: value.secretKey || '',
        sdkAppId: value.sdkAppId || '',
        signName: value.signName || '',
        templateId: value.templateId || '',
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
    form.setValue('secretId', formData.secretId);
    form.setValue('secretKey', formData.secretKey);
    form.setValue('sdkAppId', formData.sdkAppId);
    form.setValue('signName', formData.signName);
    form.setValue('templateId', formData.templateId);
  }, [configs]); // 移除 form 和 configManager 的依赖

  const onSubmit = async (values: TencentSmsFormData) => {
    await configManager.saveConfig(
      values,
      'Tencent SMS configuration',
      t,
      onConfigsUpdate
    );
  };

  const copyConfigAsJson = () => {
    let dataToExport = {};
    
    if (configManager.enabled) {
      const config = configs.find(item => item.key === 'tencentSms');
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
            {t('Tencent SMS Configuration')}
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
              name="secretId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Secret ID</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('Enter Tencent Cloud Secret ID')}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="secretKey"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Secret Key</FormLabel>
                  <div className="flex space-x-2">
                    <FormControl>
                      <Input
                        type={showSecretKey ? 'text' : 'password'}
                        placeholder={t('Enter Tencent Cloud Secret Key')}
                        {...field}
                      />
                    </FormControl>
                    <Button
                      type="button"
                      variant="outline"
                      size="icon"
                      onClick={() => setShowSecretKey(!showSecretKey)}
                      className="shrink-0"
                    >
                      {showSecretKey ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </Button>
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="sdkAppId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>SDK App ID</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('Enter SMS application SDK App ID')}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="signName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('Sign Name')}</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('Enter SMS signature name')}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="templateId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('Template ID')}</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('Enter SMS template ID')}
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