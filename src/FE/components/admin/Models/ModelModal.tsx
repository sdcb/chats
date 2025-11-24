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
  ApiType,
  getDefaultConfigByApiType 
} from '@/constants/modelDefaults';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Tips from '@/components/Tips/Tips';

// 表单类型：基于 UpdateModelDto，但因 HTML 表单和自定义组件的限制，需要做以下调整：
// 1. modelKeyId: number → string (FormSelect 组件要求 select value 必须是 string)
// 2. reasoningEffortOptions: number[] → string (OptionButtonGroup 组件使用逗号分隔的字符串)
// 3. supportedImageSizes: string[] → string (Input 组件接收用户输入的逗号分隔字符串)
// 4. 添加 modelId?: string (仅编辑模式需要，用于标识要更新的模型)
type ModelFormValues = Omit<UpdateModelDto, 'modelKeyId' | 'reasoningEffortOptions' | 'supportedImageSizes'> & {
  modelId?: string;
  modelKeyId: string;
  reasoningEffortOptions: string;
  supportedImageSizes: string;
};

interface IProps {
  isOpen: boolean;
  modelKeys: GetModelKeysResult[];
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
  // For edit mode
  selected?: AdminModelDto;
  // For add mode: provide partial default values (e.g., from quick add)
  // 使用 UpdateModelDto 作为默认值类型（更直接，自动转换在 useEffect 中处理）
  defaultValues?: Partial<UpdateModelDto>;
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
    defaultValues,
    selected 
  } = props;

  // Determine if this is edit mode
  const isEditMode = !!selected;

  // 创建 form schema
  const formSchema = useMemo(() => z.object({
    // 基础字段
    name: z.string().min(1, t('This field is require')),
    enabled: z.boolean(),
    deploymentName: z.string().min(1, t('This field is require')),
    modelKeyId: z.string().min(1, t('This field is require')).default('0'),
    inputTokenPrice1M: z.coerce.number(),
    outputTokenPrice1M: z.coerce.number(),
    modelId: z.string().optional(),
    
    // === 功能开关 ===
    allowSearch: z.boolean(),
    allowVision: z.boolean(),
    supportsVisionLink: z.boolean(),
    allowStreaming: z.boolean(),
    allowCodeExecution: z.boolean(),
    allowToolCall: z.boolean(),
    thinkTagParserEnabled: z.boolean(),
    
    // === 温度范围 ===
    minTemperature: z.coerce.number().min(0).max(2),
    maxTemperature: z.coerce.number().min(0).max(2),
    
    // === Token 配置 ===
    contextWindow: z.coerce.number().min(0),
    maxResponseTokens: z.coerce.number().min(0),
    maxThinkingBudget: z.number().min(0).nullable(),
    
    // === 数组字段（表单中用字符串） ===
    reasoningEffortOptions: z.string(),
    supportedImageSizes: z.string(),
    
    // === API 配置 ===
    apiType: z.coerce.number(),
    useAsyncApi: z.boolean(),
    useMaxCompletionTokens: z.boolean(),
    isLegacy: z.boolean(),
  } satisfies Record<keyof ModelFormValues, z.ZodTypeAny>)
  .refine((data) => {
    // ChatCompletion/Response/AnthropicMessages: 温度验证
    if (data.apiType === 0 || data.apiType === 1 || data.apiType === 3) {
      return data.minTemperature <= data.maxTemperature;
    }
    return true;
  }, {
    message: t('minTemperature must be less than or equal to maxTemperature'),
    path: ['maxTemperature'],
  })
  .refine((data) => {
    // ChatCompletion/Response/AnthropicMessages: 上下文窗口必须有值
    if (data.apiType === 0 || data.apiType === 1 || data.apiType === 3) {
      return data.contextWindow > 0;
    }
    return true;
  }, {
    message: t('Context window is required'),
    path: ['contextWindow'],
  })
  .refine((data) => {
    // ChatCompletion/Response/AnthropicMessages: 最大响应token数必须有值
    if (data.apiType === 0 || data.apiType === 1 || data.apiType === 3) {
      return data.maxResponseTokens > 0;
    }
    return true;
  }, {
    message: t('Max response tokens is required'),
    path: ['maxResponseTokens'],
  })
  .refine((data) => {
    // ChatCompletion/Response/AnthropicMessages: 最大响应token数要小于上下文窗口
    if (data.apiType === 0 || data.apiType === 1 || data.apiType === 3) {
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
  })
  .refine((data) => {
    // ChatCompletion/Response/AnthropicMessages: maxThinkingBudget 如果有值，必须小于 maxResponseTokens
    if ((data.apiType === 0 || data.apiType === 1 || data.apiType === 3) && data.maxThinkingBudget !== null && data.maxThinkingBudget !== undefined) {
      return data.maxThinkingBudget < data.maxResponseTokens;
    }
    return true;
  }, {
    message: t('Max thinking budget must be less than max response tokens'),
    path: ['maxThinkingBudget'],
  }), [t]);

  const form = useForm<ModelFormValues>({
    resolver: zodResolver(formSchema),
    mode: 'onChange',
    defaultValues: {
      name: '',
      enabled: true,
      deploymentName: '',
      modelKeyId: '0',
      inputTokenPrice1M: 0,
      outputTokenPrice1M: 0,
      modelId: '',
      apiType: ApiType.ChatCompletion,
      allowSearch: false,
      allowVision: false,
      supportsVisionLink: false,
      allowStreaming: false,
      allowCodeExecution: false,
      allowToolCall: false,
      thinkTagParserEnabled: false,
      minTemperature: 0,
      maxTemperature: 2,
      contextWindow: 0,
      maxResponseTokens: 0,
      maxThinkingBudget: null,
      reasoningEffortOptions: '',
      supportedImageSizes: '',
      useAsyncApi: false,
      useMaxCompletionTokens: false,
      isLegacy: false,
    },
  });

  const onSubmit = (values: ModelFormValues) => {
    const dto: UpdateModelDto = {
      deploymentName: values.deploymentName,
      enabled: values.enabled,
      inputTokenPrice1M: values.inputTokenPrice1M,
      outputTokenPrice1M: values.outputTokenPrice1M,
      modelKeyId: parseInt(values.modelKeyId),
      name: values.name,
      
      // === 新增字段（直接映射，无需转换）===
      allowSearch: values.allowSearch,
      allowVision: values.allowVision,
      supportsVisionLink: values.supportsVisionLink,
      allowStreaming: values.allowStreaming,
      allowCodeExecution: values.allowCodeExecution,
      allowToolCall: values.allowToolCall,
      thinkTagParserEnabled: values.thinkTagParserEnabled,
      
      minTemperature: values.minTemperature,
      maxTemperature: values.maxTemperature,
      
      contextWindow: values.contextWindow,
      maxResponseTokens: values.maxResponseTokens,
      maxThinkingBudget: values.maxThinkingBudget,
      
      // 数组字段：从逗号分隔的字符串转为数组
      reasoningEffortOptions: (() => {
        const items = values.reasoningEffortOptions
          .split(',')
          .map((x) => parseInt(x.trim()))
          .filter((x) => !isNaN(x));
        return items;
      })(),
      supportedImageSizes: (() => {
        const items = values.supportedImageSizes
          .split(',')
          .map((x) => x.trim())
          .filter((x) => x !== '');
        return items;
      })(),
      
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
        inputTokenPrice1M: values.inputTokenPrice1M,
        outputTokenPrice1M: values.outputTokenPrice1M,
        
        allowSearch: values.allowSearch,
        allowVision: values.allowVision,
        supportsVisionLink: values.supportsVisionLink,
        allowStreaming: values.allowStreaming,
        allowCodeExecution: values.allowCodeExecution,
        allowToolCall: values.allowToolCall,
        thinkTagParserEnabled: values.thinkTagParserEnabled,
        
        minTemperature: values.minTemperature,
        maxTemperature: values.maxTemperature,
        
        contextWindow: values.contextWindow,
        maxResponseTokens: values.maxResponseTokens,
        maxThinkingBudget: values.maxThinkingBudget,
        
        reasoningEffortOptions: (() => {
          const items = values.reasoningEffortOptions
            .split(',')
            .map((x) => parseInt(x.trim()))
            .filter((x) => !isNaN(x));
          return items;
        })(),
        supportedImageSizes: (() => {
          const items = values.supportedImageSizes
            .split(',')
            .map((x) => x.trim())
            .filter((x) => x !== '');
          return items;
        })(),
        
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
          supportsVisionLink,
          allowStreaming,
          allowCodeExecution,
          allowToolCall,
          thinkTagParserEnabled,
          minTemperature,
          maxTemperature,
          contextWindow,
          maxResponseTokens,
          maxThinkingBudget,
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
        form.setValue('inputTokenPrice1M', inputTokenPrice1M);
        form.setValue('outputTokenPrice1M', outputTokenPrice1M);
        
        form.setValue('allowSearch', allowSearch);
        form.setValue('allowVision', allowVision);
        form.setValue('supportsVisionLink', supportsVisionLink);
        form.setValue('allowStreaming', allowStreaming);
        form.setValue('allowCodeExecution', allowCodeExecution);
        form.setValue('allowToolCall', allowToolCall);
        form.setValue('thinkTagParserEnabled', thinkTagParserEnabled);
        
        form.setValue('minTemperature', minTemperature);
        form.setValue('maxTemperature', maxTemperature);
        
        form.setValue('contextWindow', contextWindow);
        form.setValue('maxResponseTokens', maxResponseTokens);
        form.setValue('maxThinkingBudget', maxThinkingBudget);
        
        form.setValue(
          'reasoningEffortOptions',
          reasoningEffortOptions ? reasoningEffortOptions.join(', ') : '',
        );
        form.setValue(
          'supportedImageSizes',
          supportedImageSizes ? supportedImageSizes.join(', ') : '',
        );
        
        form.setValue('apiType', apiType);
        form.setValue('useAsyncApi', useAsyncApi);
        form.setValue('useMaxCompletionTokens', useMaxCompletionTokens);
        form.setValue('isLegacy', isLegacy);
      } else {
        // Add mode: set default values
        // 先应用基础的 ChatCompletion 默认配置
        const chatDefaults = getDefaultConfigByApiType(ApiType.ChatCompletion);
        Object.entries(chatDefaults).forEach(([key, value]) => {
          if (Array.isArray(value)) {
            form.setValue(key as any, '');
          } else {
            form.setValue(key as any, value);
          }
        });
        
        // 然后应用来自 props 的 defaultValues (可能来自快速添加)
        // defaultValues 是 Partial<UpdateModelDto> 格式，数组需要转换为逗号分隔的字符串
        if (defaultValues) {
          Object.entries(defaultValues).forEach(([key, value]) => {
            if (value !== undefined) {
              if (Array.isArray(value)) {
                form.setValue(key as any, value.join(', '));
              } else if (
                value === null &&
                (key === 'reasoningEffortOptions' || key === 'supportedImageSizes')
              ) {
                form.setValue(key as any, '');
              } else if (key === 'modelKeyId' && typeof value === 'number') {
                form.setValue(key as any, value.toString());
              } else {
                form.setValue(key as any, value);
              }
            }
          });
        }
      }
    }
  }, [isOpen, selected, defaultValues, isEditMode]);

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
    
    // 根据 API 类型应用默认配置
    const defaults = getDefaultConfigByApiType(currentApiType as ApiType);
    Object.entries(defaults).forEach(([key, value]) => {
      if (key === 'reasoningEffortOptions' || key === 'supportedImageSizes') {
        if (Array.isArray(value)) {
          form.setValue(key as any, value.join(', '));
        } else {
          form.setValue(key as any, '');
        }
      } else if (value !== undefined) {
        form.setValue(key as any, value);
      }
    });
  }, [apiType, isOpen, isEditMode, selected, isInitialLoad, form]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-[calc(100vw-2rem)] sm:max-w-2xl max-h-[90vh] overflow-y-auto">
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
                      className="text-sm w-36 mt-12 text-right hidden sm:block"
                    >
                      <Popover>
                        <PopoverTrigger>
                          <span className="text-primary">
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
                    key="inputTokenPrice1M"
                    control={form.control}
                    name="inputTokenPrice1M"
                    render={({ field }) => {
                      return (
                        <FormInput
                          type="number"
                          label={t('1M input tokens price')!}
                          field={field}
                        />
                      );
                    }}
                  ></FormField>
                  <FormField
                    key="outputTokenPrice1M"
                    control={form.control}
                    name="outputTokenPrice1M"
                    render={({ field }) => {
                      return (
                        <FormInput
                          type="number"
                          label={t('1M output tokens price')!}
                          field={field}
                        />
                      );
                    }}
                  ></FormField>
              </div>
              
              {/* ========== 2. API 类型选择 ========== */}
              <div className="border-t pt-4">
                <div className="space-y-2">
                  <Label>{t('API Type')}</Label>
                  <div className="flex gap-2">
                    <Tips
                      trigger={
                        <Button
                          type="button"
                          variant={apiType === 0 ? 'default' : 'outline'}
                          onClick={() => form.setValue('apiType', 0)}
                          className="flex-1"
                        >
                          Chat Completions
                        </Button>
                      }
                      content={t('Default choice for most compatibility considerations, it is OpenAI\'s original protocol')}
                    />
                    <Tips
                      trigger={
                        <Button
                          type="button"
                          variant={apiType === 1 ? 'default' : 'outline'}
                          onClick={() => form.setValue('apiType', 1)}
                          className="flex-1"
                        >
                          Responses
                        </Button>
                      }
                      content={t('Generally only applicable to new models like OpenAI GPT-5')}
                    />
                    <Tips
                      trigger={
                        <Button
                          type="button"
                          variant={apiType === 3 ? 'default' : 'outline'}
                          onClick={() => form.setValue('apiType', 3)}
                          className="flex-1"
                        >
                          Messages
                        </Button>
                      }
                      content={t('Applicable to Anthropic Claude models')}
                    />
                    <Tips
                      trigger={
                        <Button
                          type="button"
                          variant={apiType === 2 ? 'default' : 'outline'}
                          onClick={() => form.setValue('apiType', 2)}
                          className="flex-1"
                        >
                          Images
                        </Button>
                      }
                      content={t('Applicable to image generation models, such as gpt-image-1')}
                    />
                  </div>
                </div>
              </div>
              
              {/* ========== 3. API 类型特定配置 ========== */}
              {(apiType === 0 || apiType === 1 || apiType === 3) && (
                <ChatResponseConfig control={form.control} setValue={form.setValue} watch={form.watch} apiType={apiType} />
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
