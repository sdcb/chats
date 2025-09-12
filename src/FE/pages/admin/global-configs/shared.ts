import { useState } from 'react';
import toast from 'react-hot-toast';
import { z } from 'zod';
import { getConfigs, putConfigs, deleteConfigs } from '@/apis/adminApis';
import { GetConfigsResult } from '@/types/adminApis';

// 共享类型定义
export interface SiteInfoFormData {
  customizedLine1: string;
  customizedLine2: string;
}

export interface TencentSmsFormData {
  secretId: string;
  secretKey: string;
  sdkAppId: string;
  signName: string;
  templateId: string;
}

export interface ConfigState<T> {
  enabled: boolean;
  data: T;
}

// 共享的配置管理Hook
export function useConfigManager<T>(
  configKey: string,
  initialData: T,
  parseConfigValue: (value: string) => T
) {
  const [enabled, setEnabled] = useState(false);
  const [memoryData, setMemoryData] = useState<T>(initialData);
  const [initialState, setInitialState] = useState<ConfigState<T>>({
    enabled: false,
    data: initialData,
  });
  const [saving, setSaving] = useState(false);
  const [isInitialized, setIsInitialized] = useState(false);

  // 检测是否有变化的函数
  const hasChanges = (currentData: T, configs: GetConfigsResult[]) => {
    const configExists = configs.find(item => item.key === configKey);
    
    // 如果enabled=false且配置不存在，不能保存（防止二次删除）
    if (!enabled && !configExists) {
      return false;
    }
    
    // 检查enabled状态是否有变化
    const enabledChanged = enabled !== initialState.enabled;
    
    // 检查表单数据是否有变化（需要在组件中实现具体的比较逻辑）
    const dataChanged = JSON.stringify(currentData) !== JSON.stringify(initialState.data);
    
    // enabled=true时总是可以保存，或者有数据变化时可以保存
    return enabled || enabledChanged || dataChanged;
  };

  // 保存配置的函数
  const saveConfig = async (
    data: T, 
    description: string,
    t: (key: string) => string,
    onSuccess?: () => void
  ) => {
    setSaving(true);
    setMemoryData(data);
    
    try {
      if (enabled) {
        const configValue = JSON.stringify(data);
        await putConfigs({
          key: configKey,
          value: configValue,
          description,
        });
        toast.success(t('Save successful'));
      } else {
        // 如果禁用，删除配置
        try {
          await deleteConfigs(configKey);
          toast.success(t('Save successful'));
        } catch {
          // 如果删除失败（可能是不存在），也认为成功
          toast.success(t('Save successful'));
        }
      }
      
      // 更新初始状态
      setInitialState({
        enabled,
        data,
      });
      
      onSuccess?.();
    } finally {
      setSaving(false);
    }
  };

  // 初始化配置的函数
  const initializeConfig = (configs: GetConfigsResult[], defaultData: T) => {
    const config = configs.find(item => item.key === configKey);
    
    if (config) {
      try {
        const parsedData = parseConfigValue(config.value);
        setMemoryData(parsedData);
        
        // 只在第一次初始化时设置enabled状态
        if (!isInitialized) {
          setEnabled(true);
          setInitialState({
            enabled: true,
            data: parsedData,
          });
        } else {
          // 已经初始化过，只更新初始状态的数据部分
          setInitialState(prevState => ({
            ...prevState,
            data: parsedData,
          }));
        }
        
        setIsInitialized(true);
        return parsedData;
      } catch (e) {
        console.error(`Failed to parse ${configKey} config:`, e);
        
        if (!isInitialized) {
          setEnabled(false);
          setInitialState({
            enabled: false,
            data: defaultData,
          });
        }
        
        setIsInitialized(true);
        return defaultData;
      }
    } else {
      setMemoryData(defaultData);
      
      if (!isInitialized) {
        setEnabled(false);
        setInitialState({
          enabled: false,
          data: defaultData,
        });
      }
      
      setIsInitialized(true);
      return defaultData;
    }
  };

  return {
    enabled,
    setEnabled,
    memoryData,
    setMemoryData,
    initialState,
    setInitialState,
    saving,
    setSaving,
    hasChanges,
    saveConfig,
    initializeConfig,
  };
}

// 数据比较函数
export const compareSiteInfoData = (data1: SiteInfoFormData, data2: SiteInfoFormData): boolean => {
  return data1.customizedLine1 === data2.customizedLine1 &&
         data1.customizedLine2 === data2.customizedLine2;
};

export const compareTencentSmsData = (data1: TencentSmsFormData, data2: TencentSmsFormData): boolean => {
  return data1.secretId === data2.secretId &&
         data1.secretKey === data2.secretKey &&
         data1.sdkAppId === data2.sdkAppId &&
         data1.signName === data2.signName &&
         data1.templateId === data2.templateId;
};