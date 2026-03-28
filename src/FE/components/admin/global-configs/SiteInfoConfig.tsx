import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Copy } from 'lucide-react';
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

interface SiteInfoFormData {
  customizedLine1: string;
  customizedLine2: string;
}

interface SiteInfoConfigProps {
}

// 网站信息配置解析函数
const parseSiteInfoConfig = (value: string): SiteInfoFormData => {
  const data = JSON.parse(value);
  return {
    customizedLine1: data.customizedLine1 || '',
    customizedLine2: data.customizedLine2 || '',
  };
};

export default function SiteInfoConfig({}: SiteInfoConfigProps) {
  const { t } = useTranslation();
  const [forceUpdateTrigger, setForceUpdateTrigger] = useState({});
  const [config, setConfig] = useState<GetConfigsResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [memoryData, setMemoryData] = useState<SiteInfoFormData>({
    customizedLine1: '',
    customizedLine2: '',
  });
  const [initialEnabled, setInitialEnabled] = useState(false);
  const [initialData, setInitialData] = useState<SiteInfoFormData>({
    customizedLine1: '',
    customizedLine2: '',
  });
  
  const defaultData: SiteInfoFormData = {
    customizedLine1: '',
    customizedLine2: '',
  };

  // 创建网站信息配置的Schema
  const siteInfoSchema = z.object({
    customizedLine1: z.string(),
    customizedLine2: z.string(),
  });

  const form = useForm<SiteInfoFormData>({
    resolver: zodResolver(siteInfoSchema),
    defaultValues: defaultData,
  });

  const loadConfig = async () => {
    setLoading(true);
    try {
      const nextConfig = await getConfig('siteInfo');
      setConfig(nextConfig);
      const formData = nextConfig ? parseSiteInfoConfig(nextConfig.value) : defaultData;
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
    const dataChanged = currentData.customizedLine1 !== initialData.customizedLine1
      || currentData.customizedLine2 !== initialData.customizedLine2;
    
    // enabled=true时总是可以保存，或者有数据变化时可以保存
    return enabled || enabledChanged || dataChanged;
  };

  // 监听表单变化，触发重新渲染
  useEffect(() => {
    const subscription = form.watch((value) => {
      setMemoryData({
        customizedLine1: value.customizedLine1 || '',
        customizedLine2: value.customizedLine2 || '',
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

  const onSubmit = async (values: SiteInfoFormData) => {
    setSaving(true);
    try {
      if (enabled) {
        await putConfigs({
          key: 'siteInfo',
          value: JSON.stringify(values),
          description: 'Site information configuration',
        });
        toast.success(t('Save successful'));
      } else {
        try {
          await deleteConfigs('siteInfo');
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
