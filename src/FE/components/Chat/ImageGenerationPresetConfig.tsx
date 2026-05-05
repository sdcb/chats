import React from 'react';
import { AdminModelDto } from '@/types/adminApis';
import useTranslation from '@/hooks/useTranslation';

import {
  IconTokens
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Slider } from '@/components/ui/slider';
import ImageSizeRadio from '@/components/ImageSizeRadio/ImageSizeRadio';
import ImageQualityRadio from '@/components/ImageQualityRadio/ImageQualityRadio';

interface ImageGenerationPresetConfigProps {
  model: AdminModelDto;
  imageSize: string | null;
  reasoningEffort: string | null;
  format: string | null;
  compression: number | null;
  maxOutputTokens: number | null;
  onChangeImageSize: (value: string | null) => void;
  onChangeImageQuality: (value: string) => void;
  onChangeFormat: (value: string | null) => void;
  onChangeCompression: (value: number | null) => void;
  onChangeMaxOutputTokens: (value: number | null) => void;
}

/**
 * ImageGeneration API 预设配置组件 (apiType=2)
 */
const ImageGenerationPresetConfig: React.FC<ImageGenerationPresetConfigProps> = ({
  model,
  imageSize,
  reasoningEffort,
  format,
  compression,
  maxOutputTokens,
  onChangeImageSize,
  onChangeImageQuality,
  onChangeFormat,
  onChangeCompression,
  onChangeMaxOutputTokens,
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-2">
      {/* Image Size */}
      {model.supportedImageSizes?.length > 0 && (
        <ImageSizeRadio
          value={imageSize}
          onValueChange={onChangeImageSize}
          supportedSizes={model.supportedImageSizes}
        />
      )}

      {/* Image Quality (使用 reasoningEffort 字段存储) */}
      {model.supportedEfforts && 
       model.supportedEfforts.length > 0 && (
        <ImageQualityRadio
          value={reasoningEffort}
          availableOptions={model.supportedEfforts}
          onValueChange={onChangeImageQuality}
        />
      )}

      {model.supportedFormats?.length > 0 && (
        <div className="flex flex-col gap-4">
          <div className="flex justify-between">
            <div className="text-neutral-700 dark:text-neutral-400">
              {t('Output Format')}
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                if (format === null) {
                  onChangeFormat(model.supportedFormats[0] ?? null);
                } else {
                  onChangeFormat(null);
                }
              }}
              className="h-6 px-2 text-sm"
            >
              {format === null ? t('Default') : t('Custom')}
            </Button>
          </div>
          {format !== null && (
            <div className="flex flex-wrap gap-2 px-2">
              {model.supportedFormats.map((item) => (
                <Button
                  key={item}
                  type="button"
                  variant={format === item ? 'default' : 'outline'}
                  onClick={() => onChangeFormat(item)}
                  className="capitalize"
                >
                  {item}
                </Button>
              ))}
            </div>
          )}
        </div>
      )}

      {model.supportedFormats?.length > 0 && (
        <div className="flex flex-col gap-4">
          <div className="flex justify-between">
            <div className="text-neutral-700 dark:text-neutral-400">
              {t('Output Compression')}
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                if (compression === null) {
                  onChangeCompression(100);
                } else {
                  onChangeCompression(null);
                }
              }}
              className="h-6 px-2 text-sm"
            >
              {compression === null ? t('Default') : t('Custom')}
            </Button>
          </div>
          {compression !== null && (
            <div className="px-2 space-y-2">
              <Slider
                className="cursor-pointer"
                min={0}
                max={100}
                step={1}
                value={[compression]}
                onValueChange={(values) => {
                  onChangeCompression(values[0]);
                }}
              />
              <Input
                type="number"
                min={0}
                max={100}
                value={compression}
                onChange={(event) => {
                  const raw = event.target.value;
                  onChangeCompression(raw === '' ? null : Number(raw));
                }}
              />
            </div>
          )}
        </div>
      )}

      {/* Batch Image Count (使用 maxOutputTokens 字段存储) */}
      <div className="flex flex-col gap-4">
        <div className="flex justify-between">
          <div className="flex gap-1 items-center text-neutral-700 dark:text-neutral-400">
            <IconTokens size={20} />
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
            className="h-6 px-2 text-sm"
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
