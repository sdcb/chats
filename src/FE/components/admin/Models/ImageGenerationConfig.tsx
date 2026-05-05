import React from 'react';
import { Control } from 'react-hook-form';
import useTranslation from '@/hooks/useTranslation';
import { FormField, FormControl, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import { Input } from '@/components/ui/input';
import { LabelSwitch } from '@/components/ui/label-switch';

interface ImageGenerationConfigProps {
  control: Control<any>;
}

/**
 * ImageGeneration API 配置组件 (apiType=2)
 */
const ImageGenerationConfig: React.FC<ImageGenerationConfigProps> = ({ control }) => {
  const { t } = useTranslation();

  return (
    <div className="space-y-4">
      {/* 第一行：流式预览 + 图片质量 */}
      <div className="border-t pt-4">
        <div className="grid grid-cols-2 gap-4">
          <FormField
            control={control}
            name="allowStreaming"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Return Intermediate Preview Images')!}
              />
            )}
          />
          <FormField
            control={control}
            name="supportedEfforts"
            render={({ field }) => (
              <FormInput
                label={t('Supported Image Quality Options')!}
                field={field}
                options={{ placeholder: t('e.g. low, medium, high')! }}
              />
            )}
          />
        </div>
      </div>

      {/* 第二行：图片尺寸 + 批量生成数量 */}
      <div className="border-t pt-4">
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <FormField
              control={control}
              name="supportedImageSizes"
              render={({ field }) => (
                <FormItem className="py-2">
                  <FormLabel>{t('Supported Image Sizes')}</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('e.g. 1024x1024, 1536x1024')!}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={control}
              name="maxResponseTokens"
              render={({ field }) => (
                <FormInput
                  type="number"
                  label={t('Max Batch Image Count')!}
                  field={field}
                />
              )}
            />
          </div>

          <div className="grid grid-cols-1 gap-4">
            <FormField
              control={control}
              name="supportedFormats"
              render={({ field }) => (
                <FormItem className="py-2">
                  <FormLabel>{t('Supported Output Formats')}</FormLabel>
                  <FormControl>
                    <Input
                      placeholder={t('e.g. png, jpeg, webp')!}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        </div>
      </div>
    </div>
  );
};

export default ImageGenerationConfig;
