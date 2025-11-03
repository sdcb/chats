import React from 'react';
import { AdminModelDto } from '@/types/adminApis';
import useTranslation from '@/hooks/useTranslation';

import {
  IconTokens
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
import ImageSizeRadio from '@/components/ImageSizeRadio/ImageSizeRadio';
import ImageQualityRadio from '@/components/ImageQualityRadio/ImageQualityRadio';

interface ImageGenerationPresetConfigProps {
  model: AdminModelDto;
  imageSize: number;
  reasoningEffort: number;
  maxOutputTokens: number | null;
  onChangeImageSize: (value: string) => void;
  onChangeImageQuality: (value: string) => void;
  onChangeMaxOutputTokens: (value: number | null) => void;
}

/**
 * ImageGeneration API 预设配置组件 (apiType=2)
 */
const ImageGenerationPresetConfig: React.FC<ImageGenerationPresetConfigProps> = ({
  model,
  imageSize,
  reasoningEffort,
  maxOutputTokens,
  onChangeImageSize,
  onChangeImageQuality,
  onChangeMaxOutputTokens,
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-2">
      {/* Image Size */}
      {model.supportedImageSizes?.length > 0 && (
        <ImageSizeRadio
          value={`${imageSize}`}
          onValueChange={onChangeImageSize}
        />
      )}

      {/* Image Quality (使用 reasoningEffort 字段存储) */}
      {model.reasoningEffortOptions && 
       model.reasoningEffortOptions.length > 0 && (
        <ImageQualityRadio
          value={`${reasoningEffort}`}
          availableOptions={model.reasoningEffortOptions}
          onValueChange={onChangeImageQuality}
        />
      )}

      {/* Batch Image Count (使用 maxOutputTokens 字段存储) */}
      <div className="flex flex-col gap-4">
        <div className="flex justify-between">
          <div className="flex gap-1 items-center text-neutral-700 dark:text-neutral-400">
            <IconTokens size={16} />
            {t('Batch Image Count')}
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              if (maxOutputTokens === null) {
                onChangeMaxOutputTokens(model.maxResponseTokens);
              } else {
                onChangeMaxOutputTokens(null);
              }
            }}
            className="h-6 px-2 text-xs"
          >
            {maxOutputTokens === null ? t('Default') : t('Custom')}
          </Button>
        </div>
        {maxOutputTokens !== null && (
          <div className="px-2">
            <Slider
              className="cursor-pointer"
              min={1}
              max={Math.min(model.maxResponseTokens, 128)}
              step={1}
              value={[maxOutputTokens || model.maxResponseTokens]}
              onValueChange={(values) => {
                onChangeMaxOutputTokens(values[0]);
              }}
            />
            <div className="text-xs text-gray-500 mt-1">
              {maxOutputTokens || model.maxResponseTokens}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default ImageGenerationPresetConfig;
