import React, { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import {
  deleteTitleSummarySettings,
  getTitleSummaryDefaultTemplate,
  getTitleSummarySettings,
  getUserModels,
  putTitleSummarySettings,
} from '@/apis/clientApis';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto } from '@/types/adminApis';
import {
  TitleSummaryConfig,
  TitleSummaryModelMode,
  TitleSummarySettingsDto,
} from '@/types/clientApis';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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

export default function TitleSummaryTab() {
  const defaultMode: TitleSummaryModelMode = 'truncate';
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [removing, setRemoving] = useState(false);
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [settings, setSettings] = useState<TitleSummarySettingsDto | null>(null);
  const [enabled, setEnabled] = useState(false);
  const [modelMode, setModelMode] = useState<TitleSummaryModelMode>(defaultMode);
  const [modelId, setModelId] = useState<number | null>(null);
  const [defaultTemplate, setDefaultTemplate] = useState('');
  const [promptTemplate, setPromptTemplate] = useState('');
  const [initialUserConfig, setInitialUserConfig] = useState<TitleSummaryConfig | null>(null);

  const availableModels = useMemo(
    () => models.filter((item) => item.enabled && item.apiType !== 2),
    [models],
  );

  const loadData = async () => {
    setLoading(true);
    try {
      const [nextSettings, nextModels, nextDefaultTemplate] = await Promise.all([
        getTitleSummarySettings(),
        getUserModels(),
        getTitleSummaryDefaultTemplate(),
      ]);
      setSettings(nextSettings);
      setModels(nextModels);
      setDefaultTemplate(nextDefaultTemplate.promptTemplate);
      const fallbackMode = nextSettings.userConfig?.modelMode
        ?? nextSettings.adminConfig?.modelMode
        ?? defaultMode;
      const fallbackModelId = nextSettings.resolvedConfig.modelId;
      const nextUserConfig = nextSettings.userConfig;

      setEnabled(nextUserConfig != null || nextSettings.adminConfig != null);
      setModelMode(nextUserConfig?.modelMode ?? fallbackMode);
      setModelId(nextUserConfig?.modelId ?? fallbackModelId);
      setPromptTemplate(nextUserConfig?.promptTemplate ?? '');
      setInitialUserConfig(nextUserConfig);
    } catch {
      toast.error(t('Failed to load settings'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  useEffect(() => {
    if (modelMode === 'specified' && modelId == null && availableModels.length > 0) {
      setModelId(availableModels[0].modelId);
    }
    if (modelMode !== 'specified' && modelId != null) {
      setModelId(null);
    }
  }, [availableModels, modelId, modelMode]);

  const hasUserOverride = initialUserConfig != null;
  const hasAdminConfig = settings?.adminConfig != null;
  const initialEnabled = hasUserOverride || hasAdminConfig;
  const resolvedPromptTemplate = settings?.resolvedConfig.promptTemplate ?? defaultTemplate;
  const initialModelMode = initialUserConfig?.modelMode
    ?? settings?.adminConfig?.modelMode
    ?? defaultMode;
  const initialModelId = initialUserConfig?.modelId ?? settings?.resolvedConfig.modelId ?? null;
  const initialPromptTemplate = initialUserConfig?.promptTemplate ?? '';
  const normalizedModelId = modelMode === 'specified' ? modelId : null;
  const configChanged = modelMode !== initialModelMode
    || normalizedModelId !== initialModelId
    || promptTemplate !== initialPromptTemplate;
  const hasChanges = !loading && (
    enabled !== initialEnabled
    || (enabled && configChanged)
  );
  const switchDisabled = hasAdminConfig && !hasUserOverride;

  const handleSave = async () => {
    setSaving(true);
    try {
      if (!enabled) {
        await deleteTitleSummarySettings();
        toast.success(t('Save successful'));
        await loadData();
        return;
      }

      const body: TitleSummaryConfig = {
        modelMode,
        modelId: normalizedModelId,
        promptTemplate,
      };
      await putTitleSummarySettings(body);
      toast.success(t('Save successful'));
      await loadData();
    } catch {
      toast.error(t('Operation failed, Please try again later, or contact technical personnel'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    setRemoving(true);
    try {
      await deleteTitleSummarySettings();
      toast.success(t('Delete successful'));
      await loadData();
    } catch {
      toast.error(t('Delete failed'));
    } finally {
      setRemoving(false);
    }
  };

  if (loading) {
    return <div>{t('Loading...')}</div>;
  }

  return (
    <div className="w-full">
      <Card className="border-none">
        <CardContent className="pt-6 space-y-6">
          <div className="flex flex-col gap-1">
            <span className="text-sm font-medium text-muted-foreground">{t('Inherit status')}</span>
            <span className="text-sm">
              {hasUserOverride ? t('Using personal override') : t('Inheriting system settings')}
            </span>
          </div>

          <div className="flex items-center justify-between">
            <div className="flex flex-col gap-1">
              <span className="text-sm font-medium text-muted-foreground">{t('Enable title summary')}</span>
              <span className="text-xs text-muted-foreground">
                {hasAdminConfig && !hasUserOverride
                  ? t('System config keeps this enabled; save changes below to create a personal override')
                  : hasAdminConfig
                    ? t('Using your personal override for title summary')
                    : t('No system config exists; enabling here uses your own balance')}
              </span>
            </div>
            <Switch checked={enabled} onCheckedChange={setEnabled} disabled={switchDisabled} />
          </div>

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
                placeholder={t('Leave empty to inherit the system template')}
              />
              {!promptTemplate && (
                <div className="text-xs text-muted-foreground whitespace-pre-wrap rounded-md border bg-muted/40 p-3">
                  {resolvedPromptTemplate}
                </div>
              )}
            </div>
          )}

          <div className="flex gap-2 justify-end">
            {hasUserOverride && (
              <Button variant="outline" onClick={handleDelete} disabled={removing}>
                {removing ? t('Deleting...') : t('Revert to inherit')}
              </Button>
            )}
            {hasChanges && (
              <Button
                onClick={handleSave}
                disabled={saving || (enabled && modelMode === 'specified' && normalizedModelId == null)}
              >
                {saving ? t('Saving...') : t('Save')}
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
