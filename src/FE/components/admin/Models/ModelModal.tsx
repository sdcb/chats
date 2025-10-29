import React, { useEffect, useState, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  AdminModelDto,
  ErrorResult,
  GetModelKeysResult,
  UpdateModelDto,
  ValidateModelParams,
} from '@/types/adminApis';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form, FormField } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import FormSelect from '@/components/ui/form/select';
import { LabelSwitch } from '@/components/ui/label-switch';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Label } from '@/components/ui/label';
import ChatResponseConfig from './ChatResponseConfig';
import ImageGenerationConfig from './ImageGenerationConfig';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import {
  postModels,
  postModelValidate,
  putModels,
} from '@/apis/adminApis';
import { 
  DEFAULT_CHAT_MODEL_CONFIG, 
  DEFAULT_IMAGE_MODEL_CONFIG 
} from '@/constants/modelDefaults';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  isOpen: boolean;
  modelKeys: GetModelKeysResult[];
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
  // For edit mode
  selected?: AdminModelDto;
  // For add mode: preselect model key when creating a model
  defaultModelKeyId?: number;
}

const ModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const [validating, setValidating] = useState(false);
  const [isInitialLoad, setIsInitialLoad] = useState(true);
  const { 
    isOpen, 
    onClose, 
    onSuccessful, 
    modelKeys, 
    defaultModelKeyId, 
    selected 
  } = props;

  // Determine if this is edit mode
  const isEditMode = !!selected;

  // 使用 useMemo 确保 schema 稳定
  const formSchema = useMemo(() => z.object({
    name: z.string().min(1, t('This field is require')),
    enabled: z.boolean(),
    deploymentName: z.string().min(1, t('This field is require')),
    modelKeyId: z
      .string()
      .min(1, t('This field is require'))
      .default('0'),
    inputPrice1M: z.coerce.number(),
    outputPrice1M: z.coerce.number(),
    modelId: z.string().optional(),
    
    // === 新增 18 个字段 ===
    allowSearch: z.boolean(),
    allowVision: z.boolean(),
    allowSystemPrompt: z.boolean(),
    allowStreaming: z.boolean(),
    allowCodeExecution: z.boolean(),
    allowToolCall: z.boolean(),
    thinkTagParserEnabled: z.boolean(),
    
    minTemperature: z.coerce.number().min(0).max(2),
    maxTemperature: z.coerce.number().min(0).max(2),
    
    contextWindow: z.coerce.number().min(0),
    maxResponseTokens: z.coerce.number().min(0),
    
    reasoningEffortOptions: z.string(), // 存储为逗号分隔的字符串
    supportedImageSizes: z.string(), // 存储为逗号分隔的字符串
    
    apiType: z.coerce.number(),
    useAsyncApi: z.boolean(),
    useMaxCompletionTokens: z.boolean(),
    isLegacy: z.boolean(),
  })
  .refine((data) => {
    // ChatCompletion/Response API 验证
    if (data.apiType === 0 || data.apiType === 1) {
      // 温度验证
      if (data.minTemperature > data.maxTemperature) {
        return false;
      }
      // 上下文窗口必须有值
      if (data.contextWindow <= 0) {
        return false;
      }
      // 最大响应token数必须有值
      if (data.maxResponseTokens <= 0) {
        return false;
      }
      // 最大响应token数要小于上下文窗口
      if (data.maxResponseTokens >= data.contextWindow) {
        return false;
      }
    }
    return true;
  }, {
    message: t('minTemperature must be less than or equal to maxTemperature'),
    path: ['maxTemperature'],
  })
  .refine((data) => {
    // ChatCompletion/Response: 上下文窗口必须有值
    if (data.apiType === 0 || data.apiType === 1) {
      return data.contextWindow > 0;
    }
    return true;
  }, {
    message: t('Context window is required'),
    path: ['contextWindow'],
  })
  .refine((data) => {
    // ChatCompletion/Response: 最大响应token数必须有值
    if (data.apiType === 0 || data.apiType === 1) {
      return data.maxResponseTokens > 0;
    }
    return true;
  }, {
    message: t('Max response tokens is required'),
    path: ['maxResponseTokens'],
  })
  .refine((data) => {
    // ChatCompletion/Response: 最大响应token数要小于上下文窗口
    if (data.apiType === 0 || data.apiType === 1) {
      return data.maxResponseTokens < data.contextWindow;
    }
    return true;
  }, {
    message: t('Max response tokens must be less than context window'),
    path: ['maxResponseTokens'],
  })
  .refine((data) => {
    // ImageGeneration: 支持的图片尺寸必须有值
    if (data.apiType === 2) {
      return data.supportedImageSizes.trim().length > 0;
    }
    return true;
  }, {
    message: t('Supported image sizes is required'),
    path: ['supportedImageSizes'],
  })
  .refine((data) => {
    // ImageGeneration: 支持的图片尺寸格式验证 (宽x高)
    if (data.apiType === 2 && data.supportedImageSizes.trim().length > 0) {
      const sizes = data.supportedImageSizes.split(',').map(s => s.trim()).filter(s => s !== '');
      // 验证每个尺寸格式: 数字x数字 (如: 1024x1024)
      const sizeRegex = /^\d+x\d+$/;
      return sizes.every(size => sizeRegex.test(size));
    }
    return true;
  }, {
    message: t('Invalid image size format, use format like: 1024x1024'),
    path: ['supportedImageSizes'],
  })
  .refine((data) => {
    // ImageGeneration: 最大批量生成图片数量必须有值且小于128
    if (data.apiType === 2) {
      return data.maxResponseTokens > 0 && data.maxResponseTokens <= 128;
    }
    return true;
  }, {
    message: t('Max batch count must be between 1 and 128'),
    path: ['maxResponseTokens'],
  }), [t]);

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    mode: 'onChange', // 实时验证，确保 isValid 状态准确
    defaultValues: {
      name: '',
      enabled: true,
      deploymentName: '',
      modelKeyId: '',
      inputPrice1M: 0,
      outputPrice1M: 0,
      modelId: '',
      ...DEFAULT_CHAT_MODEL_CONFIG,
      reasoningEffortOptions: '',
      supportedImageSizes: '',
    },
  });

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    const dto: UpdateModelDto = {
      deploymentName: values.deploymentName,
      enabled: values.enabled,
      inputTokenPrice1M: values.inputPrice1M,
      outputTokenPrice1M: values.outputPrice1M,
      modelKeyId: parseInt(values.modelKeyId),
      name: values.name,
      
      // === 新增字段 ===
      allowSearch: values.allowSearch,
      allowVision: values.allowVision,
      allowSystemPrompt: values.allowSystemPrompt,
      allowStreaming: values.allowStreaming,
      allowCodeExecution: values.allowCodeExecution,
      allowToolCall: values.allowToolCall,
      thinkTagParserEnabled: values.thinkTagParserEnabled,
      
      minTemperature: values.minTemperature,
      maxTemperature: values.maxTemperature,
      
      contextWindow: values.contextWindow,
      maxResponseTokens: values.maxResponseTokens,
      
      reasoningEffortOptions: values.reasoningEffortOptions
        .split(',')
        .map((x) => parseInt(x.trim()))
        .filter((x) => !isNaN(x)),
      supportedImageSizes: values.supportedImageSizes
        .split(',')
        .map((x) => x.trim())
        .filter((x) => x !== ''),
      
      apiType: values.apiType,
      useAsyncApi: values.useAsyncApi,
      useMaxCompletionTokens: values.useMaxCompletionTokens,
      isLegacy: values.isLegacy,
    };

    const apiCall = isEditMode 
      ? putModels(values.modelId!, dto)
      : postModels(dto);

    apiCall.then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    }).catch((error) => {
      console.error('Save error:', error);
      toast.error(t('Save failed'));
    });
  };

  const onValidate = async () => {
    const values = form.getValues();
    
    // 检查必要字段是否已填写
    if (!values.modelKeyId || values.modelKeyId === '0') {
      toast.error(t('Please select model key first'));
      return;
    }

    if (!values.deploymentName) {
      toast.error(t('Please enter deployment name'));
      return;
    }

    setValidating(true);
    
    try {
      // 构造完整的配置对象进行验证
      const params: ValidateModelParams = {
        name: values.name,
        enabled: values.enabled,
        deploymentName: values.deploymentName,
        modelKeyId: parseInt(values.modelKeyId),
        inputTokenPrice1M: values.inputPrice1M,
        outputTokenPrice1M: values.outputPrice1M,
        
        allowSearch: values.allowSearch,
        allowVision: values.allowVision,
        allowSystemPrompt: values.allowSystemPrompt,
        allowStreaming: values.allowStreaming,
        allowCodeExecution: values.allowCodeExecution,
        allowToolCall: values.allowToolCall,
        thinkTagParserEnabled: values.thinkTagParserEnabled,
        
        minTemperature: values.minTemperature,
        maxTemperature: values.maxTemperature,
        
        contextWindow: values.contextWindow,
        maxResponseTokens: values.maxResponseTokens,
        
        reasoningEffortOptions: values.reasoningEffortOptions
          .split(',')
          .map((x) => parseInt(x.trim()))
          .filter((x) => !isNaN(x)),
        supportedImageSizes: values.supportedImageSizes
          .split(',')
          .map((x) => x.trim())
          .filter((x) => x !== ''),
        
        apiType: values.apiType,
        useAsyncApi: values.useAsyncApi,
        useMaxCompletionTokens: values.useMaxCompletionTokens,
        isLegacy: values.isLegacy,
      };

      const result: ErrorResult = await postModelValidate(params);
      
      if (result.isSuccess) {
        toast.success(t('Verified Successfully'));
      } else {
        toast.error(result.errorMessage || t('Model validation failed'));
      }
    } catch (error: any) {
      console.error('Validation error:', error);
      try {
        const errorResponse = await error.json();
        toast.error(errorResponse.message || t('Validation request failed'));
      } catch {
        toast.error(t('Validation request failed, please try again later'));
      }
    } finally {
      setValidating(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      form.reset();
      setIsInitialLoad(true); // 重置初始加载标记
      
      if (isEditMode && selected) {
        // Edit mode: populate form with existing data
        const {
          name,
          modelId,
          enabled,
          modelKeyId,
          deploymentName,
          inputTokenPrice1M,
          outputTokenPrice1M,
          allowSearch,
          allowVision,
          allowSystemPrompt,
          allowStreaming,
          allowCodeExecution,
          allowToolCall,
          thinkTagParserEnabled,
          minTemperature,
          maxTemperature,
          contextWindow,
          maxResponseTokens,
          reasoningEffortOptions,
          supportedImageSizes,
          apiType,
          useAsyncApi,
          useMaxCompletionTokens,
          isLegacy,
        } = selected;
        
        form.setValue('name', name);
        form.setValue('modelId', modelId.toString());
        form.setValue('enabled', enabled);
        form.setValue('modelKeyId', modelKeyId.toString());
        form.setValue('deploymentName', deploymentName);
        form.setValue('inputPrice1M', inputTokenPrice1M);
        form.setValue('outputPrice1M', outputTokenPrice1M);
        
        form.setValue('allowSearch', allowSearch);
        form.setValue('allowVision', allowVision);
        form.setValue('allowSystemPrompt', allowSystemPrompt);
        form.setValue('allowStreaming', allowStreaming);
        form.setValue('allowCodeExecution', allowCodeExecution);
        form.setValue('allowToolCall', allowToolCall);
        form.setValue('thinkTagParserEnabled', thinkTagParserEnabled);
        
        form.setValue('minTemperature', minTemperature);
        form.setValue('maxTemperature', maxTemperature);
        
        form.setValue('contextWindow', contextWindow);
        form.setValue('maxResponseTokens', maxResponseTokens);
        
        form.setValue('reasoningEffortOptions', reasoningEffortOptions.join(', '));
        form.setValue('supportedImageSizes', supportedImageSizes.join(', '));
        
        form.setValue('apiType', apiType);
        form.setValue('useAsyncApi', useAsyncApi);
        form.setValue('useMaxCompletionTokens', useMaxCompletionTokens);
        form.setValue('isLegacy', isLegacy);
      } else {
        // Add mode: set default values
        if (defaultModelKeyId !== undefined) {
          form.setValue('modelKeyId', defaultModelKeyId.toString());
        }
        // 使用默认配置（ChatCompletion）
        Object.keys(DEFAULT_CHAT_MODEL_CONFIG).forEach((key) => {
          const value = DEFAULT_CHAT_MODEL_CONFIG[key as keyof typeof DEFAULT_CHAT_MODEL_CONFIG];
          if (Array.isArray(value)) {
            form.setValue(key as any, '');
          } else {
            form.setValue(key as any, value);
          }
        });
      }
    }
  }, [isOpen, selected, defaultModelKeyId, isEditMode]);

  const getAvailableModelKeys = () => {
    if (isEditMode && selected) {
      // In edit mode, only show keys for the same provider
      return modelKeys.filter(
        (x) => x.modelProviderId === selected.modelProviderId,
      );
    } else {
      // In add mode, show all keys
      return modelKeys;
    }
  };

  // 监听 apiType 变化
  const apiType = form.watch('apiType');
  
  // 当 apiType 变化时，设置合理的默认值
  useEffect(() => {
    if (!isOpen) return;
    
    // 跳过初始加载（避免覆盖编辑模式的数据）
    if (isInitialLoad) {
      setIsInitialLoad(false);
      return;
    }
    
    const currentApiType = typeof apiType === 'string' ? parseInt(apiType) : apiType;
    
    // 编辑模式下不自动更改
    if (isEditMode && selected) {
      return;
    }
    
    if (currentApiType === 2) {
      // ImageGeneration 默认值 - 应用完整配置
      const imageDefaults = DEFAULT_IMAGE_MODEL_CONFIG;
      form.setValue('reasoningEffortOptions', imageDefaults.reasoningEffortOptions.join(', '));
      form.setValue('supportedImageSizes', imageDefaults.supportedImageSizes.join(', '));
      form.setValue('maxResponseTokens', imageDefaults.maxResponseTokens);
      form.setValue('allowStreaming', imageDefaults.allowStreaming);
      form.setValue('contextWindow', imageDefaults.contextWindow);
      form.setValue('allowVision', imageDefaults.allowVision);
      form.setValue('allowSearch', imageDefaults.allowSearch);
      form.setValue('allowSystemPrompt', imageDefaults.allowSystemPrompt);
      form.setValue('allowCodeExecution', imageDefaults.allowCodeExecution);
      form.setValue('allowToolCall', imageDefaults.allowToolCall);
      form.setValue('thinkTagParserEnabled', imageDefaults.thinkTagParserEnabled);
    } else if (currentApiType === 0 || currentApiType === 1) {
      // ChatCompletion / Response 默认值
      const chatDefaults = DEFAULT_CHAT_MODEL_CONFIG;
      form.setValue('reasoningEffortOptions', ''); // 推理模型默认关闭
      form.setValue('supportedImageSizes', '');
      form.setValue('maxResponseTokens', chatDefaults.maxResponseTokens);
      form.setValue('allowStreaming', chatDefaults.allowStreaming);
      form.setValue('contextWindow', chatDefaults.contextWindow);
      form.setValue('allowVision', chatDefaults.allowVision);
      form.setValue('allowSearch', chatDefaults.allowSearch);
      form.setValue('allowSystemPrompt', chatDefaults.allowSystemPrompt);
      form.setValue('allowCodeExecution', chatDefaults.allowCodeExecution);
      form.setValue('allowToolCall', chatDefaults.allowToolCall);
      form.setValue('thinkTagParserEnabled', chatDefaults.thinkTagParserEnabled);
    }
  }, [apiType, isOpen, isEditMode, selected, isInitialLoad, form]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-3/4 max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {isEditMode ? t('Edit Model') : t('Add Model')}
          </DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)}>
            <div className="space-y-4">
              {/* ========== 1. 基础信息（所有模型都需要）========== */}
              <div className="grid grid-cols-2 gap-4">
                <FormField
                    key="name"
                    control={form.control}
                    name="name"
                    render={({ field }) => {
                      return (
                        <FormInput field={field} label={t('Model Display Name')!} />
                      );
                    }}
                  ></FormField>
                  <div className="flex justify-between">
                    <FormField
                      key="modelKeyId"
                      control={form.control}
                      name="modelKeyId"
                      render={({ field }) => {
                        return (
                          <FormSelect
                            className="w-full"
                            field={field}
                            label={t('Model Keys')!}
                            items={getAvailableModelKeys().map((keys) => ({
                              name: keys.name,
                              value: keys.id.toString(),
                            }))}
                          />
                        );
                      }}
                    ></FormField>
                    <div
                      hidden={!form.getValues('modelKeyId')}
                      className="text-sm w-36 mt-12 text-right"
                    >
                      <Popover>
                        <PopoverTrigger>
                          <span className="text-primary invisible sm:visible">
                            {t('Click View Configs')}
                          </span>
                        </PopoverTrigger>
                        <PopoverContent className="w-full">
                          {JSON.stringify(
                            modelKeys
                              .find((x) => x.id === +form.getValues('modelKeyId')!)
                              ?.toConfigs(),
                            null,
                            2,
                          )}
                        </PopoverContent>
                      </Popover>
                    </div>
                  </div>
                  
                  {/* 部署名称 + 是否过时 */}
                  <FormField
                    key="deploymentName"
                    control={form.control}
                    name="deploymentName"
                    render={({ field }) => {
                      return (
                        <FormInput label={t('Deployment Name')!} field={field} />
                      );
                    }}
                  ></FormField>
                  <FormField
                    control={form.control}
                    name="isLegacy"
                    render={({ field }) => (
                      <LabelSwitch
                        checked={field.value}
                        onCheckedChange={field.onChange}
                        label={t('Is Outdated')!}
                      />
                    )}
                  />
                  
                  {/* 模型价格 */}
                  <FormField
                    key="inputPrice1M"
                    control={form.control}
                    name="inputPrice1M"
                    render={({ field }) => {
                      return (
                        <FormInput
                          type="number"
                          label={`${t('1M input tokens price')}(${t('Yuan')})`}
                          field={field}
                        />
                      );
                    }}
                  ></FormField>
                  <FormField
                    key="outputPrice1M"
                    control={form.control}
                    name="outputPrice1M"
                    render={({ field }) => {
                      return (
                        <FormInput
                          type="number"
                          label={`${t('1M output tokens price')}(${t('Yuan')})`}
                          field={field}
                        />
                      );
                    }}
                  ></FormField>
              </div>
              
              {/* ========== 2. API 类型选择 ========== */}
              <div className="border-t pt-4">
                <div className="grid grid-cols-2 gap-4">
                  <FormField
                    key="apiType"
                    control={form.control}
                    name="apiType"
                    render={({ field }) => (
                      <div className="space-y-2">
                        <Label>{t('API Type')}</Label>
                        <RadioGroup
                          value={field.value.toString()}
                          onValueChange={(value) => field.onChange(Number(value))}
                          className="flex flex-row gap-4"
                        >
                          <div className="flex items-center space-x-2">
                            <RadioGroupItem value="0" id="api-type-0" />
                            <Label htmlFor="api-type-0" className="font-normal cursor-pointer">
                              ChatCompletion
                            </Label>
                          </div>
                          <div className="flex items-center space-x-2">
                            <RadioGroupItem value="1" id="api-type-1" />
                            <Label htmlFor="api-type-1" className="font-normal cursor-pointer">
                              Response
                            </Label>
                          </div>
                          <div className="flex items-center space-x-2">
                            <RadioGroupItem value="2" id="api-type-2" />
                            <Label htmlFor="api-type-2" className="font-normal cursor-pointer">
                              ImageGeneration
                            </Label>
                          </div>
                        </RadioGroup>
                      </div>
                    )}
                  />
                  
                  {/* 仅 apiType=1 (Response) 显示 Use Async API */}
                  {apiType === 1 && (
                    <FormField
                      control={form.control}
                      name="useAsyncApi"
                      render={({ field }) => (
                        <LabelSwitch
                          checked={field.value}
                          onCheckedChange={field.onChange}
                          label={t('Use Async API')!}
                        />
                      )}
                    />
                  )}
                </div>
              </div>
              
              {/* ========== 3. API 类型特定配置 ========== */}
              {(apiType === 0 || apiType === 1) && (
                <ChatResponseConfig control={form.control} setValue={form.setValue} />
              )}
              
              {apiType === 2 && (
                <ImageGenerationConfig control={form.control} />
              )}
            </div>
            
            <DialogFooter className="pt-4 mt-4 border-t">
              <div className="flex items-center justify-between w-full">
                <FormField
                  control={form.control}
                  name="enabled"
                  render={({ field }) => (
                    <LabelSwitch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      label={t('Is it enabled')!}
                    />
                  )}
                />
                <div className="flex gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={onValidate}
                    disabled={validating}
                  >
                    {validating ? t('Validating...') : t('Validate')}
                  </Button>
                  <Button type="submit">{t('Save')}</Button>
                </div>
              </div>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};

export default ModelModal;
