import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Eye, EyeOff, Copy } from 'lucide-react';
import toast from 'react-hot-toast';
import { z } from 'zod';

import { deleteConfigs, getConfig, putConfigs } from '@/apis/adminApis';
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

interface TencentSmsFormData {
  secretId: string;
  secretKey: string;
  sdkAppId: string;
  signName: string;
  templateId: string;
}

interface TencentSmsConfigProps {
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

export default function TencentSmsConfig({}: TencentSmsConfigProps) {
  const { t } = useTranslation();
  const [showSecretKey, setShowSecretKey] = useState(false);
  const [forceUpdateTrigger, setForceUpdateTrigger] = useState({});
  const [config, setConfig] = useState<GetConfigsResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [memoryData, setMemoryData] = useState<TencentSmsFormData>({
    secretId: '',
    secretKey: '',
    sdkAppId: '',
    signName: '',
    templateId: '',
  });
  const [initialEnabled, setInitialEnabled] = useState(false);
  const [initialData, setInitialData] = useState<TencentSmsFormData>({
    secretId: '',
    secretKey: '',
    sdkAppId: '',
    signName: '',
    templateId: '',
  });
  
  const defaultData: TencentSmsFormData = {
    secretId: '',
    secretKey: '',
    sdkAppId: '',
    signName: '',
    templateId: '',
  };

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

  const loadConfig = async () => {
    setLoading(true);
    try {
      const nextConfig = await getConfig('tencentSms');
      setConfig(nextConfig);
      const formData = nextConfig ? parseTencentSmsConfig(nextConfig.value) : defaultData;
      setEnabled(nextConfig != null);
      setMemoryData(formData);
      setInitialEnabled(nextConfig != null);
      setInitialData(formData);
      form.reset(formData);
    } catch {
      toast.error(t('Failed to fetch configs'));
    } finally {
      setLoading(false);
    }
  };

  // 自定义的变化检测函数
  const hasChanges = () => {
    const configExists = config != null;
    
    // 如果enabled=false且配置不存在，不能保存（防止二次删除）
    if (!enabled && !configExists) {
      return false;
    }
    
    // 检查enabled状态是否有变化
    const enabledChanged = enabled !== initialEnabled;
    
    // 检查表单数据是否有变化
    const currentData = form.getValues();
    const dataChanged = currentData.secretId !== initialData.secretId
      || currentData.secretKey !== initialData.secretKey
      || currentData.sdkAppId !== initialData.sdkAppId
      || currentData.signName !== initialData.signName
      || currentData.templateId !== initialData.templateId;
    
    // enabled=true时总是可以保存，或者有数据变化时可以保存
    return enabled || enabledChanged || dataChanged;
  };

  // 监听表单变化，触发重新渲染
  useEffect(() => {
    const subscription = form.watch((value) => {
      setMemoryData({
        secretId: value.secretId || '',
        secretKey: value.secretKey || '',
        sdkAppId: value.sdkAppId || '',
        signName: value.signName || '',
        templateId: value.templateId || '',
      });
      setForceUpdateTrigger({}); // 触发重新渲染以更新保存按钮显示
    });

    return () => subscription.unsubscribe();
  }, [form]);

  // 监听enabled状态变化
  useEffect(() => {
    setForceUpdateTrigger({});
  }, [enabled]);

  useEffect(() => {
    void loadConfig();
  }, []);

  const onSubmit = async (values: TencentSmsFormData) => {
    setSaving(true);
    try {
      if (enabled) {
        await putConfigs({
          key: 'tencentSms',
          value: JSON.stringify(values),
          description: 'Tencent SMS configuration',
        });
        toast.success(t('Save successful'));
      } else {
        try {
          await deleteConfigs('tencentSms');
        } catch {
        }
        toast.success(t('Save successful'));
      }
      await loadConfig();
    } finally {
      setSaving(false);
    }
  };

  const copyConfigAsJson = () => {
    let dataToExport = {};
    
    if (enabled) {
      if (config) {
        try {
          dataToExport = JSON.parse(config.value);
        } catch (e) {
          console.error('Failed to parse config:', e);
          return;
        }
      }
    } else {
      dataToExport = memoryData;
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
          <span className="text-sm">{enabled ? t('Enabled') : t('Disabled')}</span>
          <Switch
            checked={enabled}
            onCheckedChange={setEnabled}
          />
        </div>
      </CardHeader>
      <CardContent>
        {loading ? (
          <div>{t('Loading...')}</div>
        ) : (
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
                  disabled={saving}
                  className="min-w-24"
                >
                  {saving ? t('Saving...') : t('Save')}
                </Button>
              </div>
            )}
          </form>
        </Form>
        )}
      </CardContent>
    </Card>
  );
}
