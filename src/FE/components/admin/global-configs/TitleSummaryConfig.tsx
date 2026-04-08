import React, { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import { getModels, putConfigs, deleteConfigs, getTitleSummaryAdminSettings } from '@/apis/adminApis';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto } from '@/types/adminApis';
import type { TitleSummaryConfig as TitleSummaryConfigValue } from '@/types/clientApis';
import { TitleSummaryModelMode } from '@/types/clientApis';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';

interface TitleSummaryConfigProps {
  onConfigsUpdate?: () => void;
}

const CONFIG_KEY = 'chatTitleSummary';
const DEFAULT_MODE: TitleSummaryModelMode = 'truncate';

const parseConfig = (value: string, defaultTemplate: string): TitleSummaryConfigValue => {
  const parsed = JSON.parse(value);
  return {
    modelMode: parsed.modelMode === 'truncate'
      ? 'truncate'
      : parsed.modelMode === 'current'
        ? 'current'
        : 'specified',
    modelId: typeof parsed.modelId === 'number' ? parsed.modelId : null,
    promptTemplate: typeof parsed.promptTemplate === 'string' ? parsed.promptTemplate : defaultTemplate,
  };
};

export default function TitleSummaryConfig({ onConfigsUpdate }: TitleSummaryConfigProps) {
  const { t } = useTranslation();
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [modelMode, setModelMode] = useState<TitleSummaryModelMode>(DEFAULT_MODE);
  const [modelId, setModelId] = useState<number | null>(null);
  const [defaultTemplate, setDefaultTemplate] = useState('');
  const [promptTemplate, setPromptTemplate] = useState('');
  const [initialState, setInitialState] = useState<{
    enabled: boolean;
    modelMode: TitleSummaryModelMode;
    modelId: number | null;
    promptTemplate: string;
  }>({
    enabled: false,
    modelMode: DEFAULT_MODE,
    modelId: null,
    promptTemplate: '',
  });

  const availableModels = useMemo(
    () => models.filter((item) => item.enabled && item.apiType !== 2),
    [models],
  );

  const defaultModelId = availableModels[0]?.modelId ?? null;

  const loadData = async () => {
    setLoading(true);
    try {
      const [modelData, titleSummarySettings] = await Promise.all([
        getModels(),
        getTitleSummaryAdminSettings(),
      ]);

      setModels(modelData);
      setDefaultTemplate(titleSummarySettings.defaultPromptTemplate);

      const parsed = titleSummarySettings.config;
      const nextState = {
        enabled: parsed != null,
        modelMode: parsed?.modelMode ?? DEFAULT_MODE,
        modelId: parsed?.modelMode === 'specified'
          ? (parsed.modelId ?? modelData.filter((item) => item.enabled && item.apiType !== 2)[0]?.modelId ?? null)
          : null,
        promptTemplate: parsed?.promptTemplate || titleSummarySettings.defaultPromptTemplate,
      };
      setEnabled(nextState.enabled);
      setModelMode(nextState.modelMode);
      setModelId(nextState.modelId);
      setPromptTemplate(nextState.promptTemplate);
      setInitialState(nextState);
    } catch {
      toast.error(t('Failed to load models'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, [t]);

  useEffect(() => {
    if (modelMode === 'specified' && modelId == null && availableModels.length > 0) {
      setModelId(availableModels[0].modelId);
    }
    if (modelMode !== 'specified' && modelId != null) {
      setModelId(null);
    }
  }, [availableModels, modelId, modelMode]);

  const hasChanges = enabled !== initialState.enabled
    || modelMode !== initialState.modelMode
    || modelId !== initialState.modelId
    || promptTemplate !== initialState.promptTemplate;

  const canSave = !loading
    && hasChanges
    && (!enabled || modelMode !== 'specified' || modelId != null);

  const handleSave = async () => {
    setSaving(true);
    try {
      if (!enabled) {
        await deleteConfigs(CONFIG_KEY);
      } else {
        await putConfigs({
          key: CONFIG_KEY,
          value: JSON.stringify({
            modelMode,
            modelId: modelMode === 'specified' ? modelId : null,
            promptTemplate,
          }),
          description: 'Chat title summary configuration',
        });
      }
      toast.success(t('Save successful'));
      await loadData();
      onConfigsUpdate?.();
    } catch {
      toast.error(t('Operation failed, Please try again later, or contact technical personnel'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card className="w-full">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-6">
        <CardTitle className="text-lg font-medium">{t('Title Summary Configuration')}</CardTitle>
        <div className="flex items-center gap-2">
          <span className="text-sm">{enabled ? t('Enabled') : t('Disabled')}</span>
          <Switch checked={enabled} onCheckedChange={setEnabled} />
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="space-y-2">
          <Label>{t('Model Mode')}</Label>
          <Select value={modelMode} onValueChange={(value) => setModelMode(value as TitleSummaryModelMode)}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="truncate">{t('Truncate first 50 chars')}</SelectItem>
              <SelectItem value="current">{t('Current chat model')}</SelectItem>
              <SelectItem value="specified">{t('Specified model')}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {modelMode === 'specified' && (
          <div className="space-y-2">
            <Label>{t('Model')}</Label>
            <Select
              value={modelId != null ? String(modelId) : undefined}
              onValueChange={(value) => setModelId(Number(value))}
            >
              <SelectTrigger>
                <SelectValue placeholder={t('Select an Model')} />
              </SelectTrigger>
              <SelectContent>
                {availableModels.map((item) => (
                  <SelectItem key={item.modelId} value={String(item.modelId)}>
                    {item.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        {modelMode !== 'truncate' && (
          <div className="space-y-2">
            <Label>{t('Prompt Template')}</Label>
            <Textarea
              rows={8}
              value={promptTemplate}
              onChange={(event) => setPromptTemplate(event.target.value)}
              placeholder={defaultTemplate}
            />
          </div>
        )}

        {canSave && (
          <div className="flex justify-end">
            <Button onClick={handleSave} disabled={saving}>
              {saving ? t('Saving...') : t('Save')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
