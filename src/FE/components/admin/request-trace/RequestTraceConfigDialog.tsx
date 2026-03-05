import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Copy } from 'lucide-react';
import toast from 'react-hot-toast';
import { z } from 'zod';

import { getConfigs, putConfigs } from '@/apis/adminApis';
import useTranslation from '@/hooks/useTranslation';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';

import { IconCode, IconFileText, IconFilter, IconSettingsCog } from '@/components/Icons';
import { cn } from '@/lib/utils';

// ────────────────────────────── types ──────────────────────────────

interface RequestTraceFilters {
  sourcePatterns: string[] | null;
  includeUrlPatterns: string[] | null;
  excludeUrlPatterns: string[] | null;
  methods: string[] | null;
  statusCodes: string[] | null;
  minDurationMs: number | null;
}

interface RequestTraceHeaderConfig {
  includeRequestHeaders: string[] | null;
  includeResponseHeaders: string[] | null;
  redactRequestHeaders: string[];
  redactResponseHeaders: string[];
}

interface RequestTraceBodyConfig {
  captureRequestBody: boolean;
  captureResponseBody: boolean;
  captureRawRequestBody: boolean;
  captureRawResponseBody: boolean;
  maxTextCharsForTruncate: number;
  allowedContentTypes: string[] | null;
  redactJsonFields: string[];
}

interface RequestTraceDirectionConfig {
  enabled: boolean;
  sampleRate: number;
  retentionDays: number | null;
  filters: RequestTraceFilters;
  headers: RequestTraceHeaderConfig;
  body: RequestTraceBodyConfig;
}

interface DirectionFormInput {
  enabled: boolean;
  sampleRate: string;
  retentionDays: '7' | '30' | '90' | 'null';
  sourcePatterns: string;
  includeUrlPatterns: string;
  excludeUrlPatterns: string;
  methods: string;
  statusCodes: string;
  minDurationMs: string;
  includeRequestHeaders: string;
  includeResponseHeaders: string;
  redactRequestHeaders: string;
  redactResponseHeaders: string;
  captureRequestBody: boolean;
  captureResponseBody: boolean;
  captureRawRequestBody: boolean;
  captureRawResponseBody: boolean;
  maxTextCharsForTruncate: string;
  allowedContentTypes: string;
  redactJsonFields: string;
}

type TraceDirection = 'inbound' | 'outbound';

// ────────────────────────────── defaults ──────────────────────────────

const DEFAULT_DIRECTION_CONFIG: RequestTraceDirectionConfig = {
  enabled: false,
  sampleRate: 1,
  retentionDays: 30,
  filters: {
    sourcePatterns: null,
    includeUrlPatterns: null,
    excludeUrlPatterns: null,
    methods: null,
    statusCodes: null,
    minDurationMs: null,
  },
  headers: {
    includeRequestHeaders: null,
    includeResponseHeaders: null,
    redactRequestHeaders: ['authorization', 'cookie', 'x-api-key', 'proxy-authorization'],
    redactResponseHeaders: ['set-cookie'],
  },
  body: {
    captureRequestBody: true,
    captureResponseBody: true,
    captureRawRequestBody: false,
    captureRawResponseBody: false,
    maxTextCharsForTruncate: 5 * 1024 * 1024,
    allowedContentTypes: null,
    redactJsonFields: ['password', 'token', 'secret', 'apiKey', 'access_token', 'refresh_token'],
  },
};

// ────────────────────────────── helpers ──────────────────────────────

const splitLines = (value: string) =>
  value
    .split('\n')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);

const toNullableArray = (value: string): string[] | null => {
  const list = splitLines(value);
  return list.length > 0 ? list : null;
};

const toRequiredArray = (value: string): string[] => splitLines(value);

const toTextareaValue = (value: string[] | null | undefined) =>
  value && value.length > 0 ? value.join('\n') : '';

const parseNonNegativeIntOrNull = (value: string): number | null => {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number.parseInt(trimmed, 10);
  if (!Number.isFinite(parsed) || parsed < 0) return null;
  return parsed;
};

const normalizeDirectionConfig = (rawValue: unknown): RequestTraceDirectionConfig => {
  if (!rawValue || typeof rawValue !== 'object') return DEFAULT_DIRECTION_CONFIG;

  const data = rawValue as Partial<RequestTraceDirectionConfig>;
  const body = (data.body ?? {}) as Partial<RequestTraceBodyConfig>;

  return {
    enabled: data.enabled ?? DEFAULT_DIRECTION_CONFIG.enabled,
    sampleRate: typeof data.sampleRate === 'number' ? data.sampleRate : DEFAULT_DIRECTION_CONFIG.sampleRate,
    retentionDays:
      data.retentionDays === null
        ? null
        : typeof data.retentionDays === 'number'
          ? data.retentionDays
          : DEFAULT_DIRECTION_CONFIG.retentionDays,
    filters: {
      sourcePatterns: data.filters?.sourcePatterns ?? DEFAULT_DIRECTION_CONFIG.filters.sourcePatterns,
      includeUrlPatterns: data.filters?.includeUrlPatterns ?? DEFAULT_DIRECTION_CONFIG.filters.includeUrlPatterns,
      excludeUrlPatterns: data.filters?.excludeUrlPatterns ?? DEFAULT_DIRECTION_CONFIG.filters.excludeUrlPatterns,
      methods: data.filters?.methods ?? DEFAULT_DIRECTION_CONFIG.filters.methods,
      statusCodes: data.filters?.statusCodes ?? DEFAULT_DIRECTION_CONFIG.filters.statusCodes,
      minDurationMs: data.filters?.minDurationMs ?? DEFAULT_DIRECTION_CONFIG.filters.minDurationMs,
    },
    headers: {
      includeRequestHeaders: data.headers?.includeRequestHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.includeRequestHeaders,
      includeResponseHeaders: data.headers?.includeResponseHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.includeResponseHeaders,
      redactRequestHeaders: data.headers?.redactRequestHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.redactRequestHeaders,
      redactResponseHeaders: data.headers?.redactResponseHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.redactResponseHeaders,
    },
    body: {
      captureRequestBody: body.captureRequestBody ?? DEFAULT_DIRECTION_CONFIG.body.captureRequestBody,
      captureResponseBody: body.captureResponseBody ?? DEFAULT_DIRECTION_CONFIG.body.captureResponseBody,
      captureRawRequestBody: body.captureRawRequestBody ?? DEFAULT_DIRECTION_CONFIG.body.captureRawRequestBody,
      captureRawResponseBody: body.captureRawResponseBody ?? DEFAULT_DIRECTION_CONFIG.body.captureRawResponseBody,
      maxTextCharsForTruncate: body.maxTextCharsForTruncate ?? DEFAULT_DIRECTION_CONFIG.body.maxTextCharsForTruncate,
      allowedContentTypes: body.allowedContentTypes ?? DEFAULT_DIRECTION_CONFIG.body.allowedContentTypes,
      redactJsonFields: body.redactJsonFields ?? DEFAULT_DIRECTION_CONFIG.body.redactJsonFields,
    },
  };
};

const parseDirectionConfigFromValue = (value?: string): RequestTraceDirectionConfig => {
  if (!value) return DEFAULT_DIRECTION_CONFIG;
  try {
    return normalizeDirectionConfig(JSON.parse(value));
  } catch {
    return DEFAULT_DIRECTION_CONFIG;
  }
};

const toFormValues = (config: RequestTraceDirectionConfig): DirectionFormInput => ({
  enabled: config.enabled,
  sampleRate: String(config.sampleRate),
  retentionDays:
    config.retentionDays === null
      ? 'null'
      : config.retentionDays === 7
        ? '7'
        : config.retentionDays === 90
          ? '90'
          : '30',
  sourcePatterns: toTextareaValue(config.filters.sourcePatterns),
  includeUrlPatterns: toTextareaValue(config.filters.includeUrlPatterns),
  excludeUrlPatterns: toTextareaValue(config.filters.excludeUrlPatterns),
  methods: toTextareaValue(config.filters.methods),
  statusCodes: toTextareaValue(config.filters.statusCodes),
  minDurationMs: config.filters.minDurationMs === null || config.filters.minDurationMs === undefined ? '' : String(config.filters.minDurationMs),
  includeRequestHeaders: toTextareaValue(config.headers.includeRequestHeaders),
  includeResponseHeaders: toTextareaValue(config.headers.includeResponseHeaders),
  redactRequestHeaders: toTextareaValue(config.headers.redactRequestHeaders),
  redactResponseHeaders: toTextareaValue(config.headers.redactResponseHeaders),
  captureRequestBody: config.body.captureRequestBody,
  captureResponseBody: config.body.captureResponseBody,
  captureRawRequestBody: config.body.captureRawRequestBody,
  captureRawResponseBody: config.body.captureRawResponseBody,
  maxTextCharsForTruncate: String(config.body.maxTextCharsForTruncate),
  allowedContentTypes: toTextareaValue(config.body.allowedContentTypes),
  redactJsonFields: toTextareaValue(config.body.redactJsonFields),
});

const toDirectionConfig = (data: DirectionFormInput): RequestTraceDirectionConfig => ({
  enabled: data.enabled,
  sampleRate: Number.parseFloat(data.sampleRate),
  retentionDays: data.retentionDays === 'null' ? null : Number.parseInt(data.retentionDays, 10),
  filters: {
    sourcePatterns: toNullableArray(data.sourcePatterns),
    includeUrlPatterns: toNullableArray(data.includeUrlPatterns),
    excludeUrlPatterns: toNullableArray(data.excludeUrlPatterns),
    methods: toNullableArray(data.methods),
    statusCodes: toNullableArray(data.statusCodes),
    minDurationMs: parseNonNegativeIntOrNull(data.minDurationMs),
  },
  headers: {
    includeRequestHeaders: toNullableArray(data.includeRequestHeaders),
    includeResponseHeaders: toNullableArray(data.includeResponseHeaders),
    redactRequestHeaders: toRequiredArray(data.redactRequestHeaders),
    redactResponseHeaders: toRequiredArray(data.redactResponseHeaders),
  },
  body: {
    captureRequestBody: data.captureRequestBody,
    captureResponseBody: data.captureResponseBody,
    captureRawRequestBody: data.captureRawRequestBody,
    captureRawResponseBody: data.captureRawResponseBody,
    maxTextCharsForTruncate: Number.parseInt(data.maxTextCharsForTruncate, 10),
    allowedContentTypes: toNullableArray(data.allowedContentTypes),
    redactJsonFields: toRequiredArray(data.redactJsonFields),
  },
});

// ────────────────────────────── schema ──────────────────────────────

const createSchema = (t: (key: string) => string) => {
  const sampleRateSchema = z
    .string()
    .trim()
    .refine((v) => v.length > 0, t('Sample rate is required'))
    .refine((v) => {
      const n = Number.parseFloat(v);
      return Number.isFinite(n) && n >= 0 && n <= 1;
    }, t('Sample rate must be between 0 and 1'));

  const maxTextCharsSchema = z
    .string()
    .trim()
    .refine((v) => v.length > 0, t('Max text chars is required'))
    .refine((v) => {
      const n = Number.parseInt(v, 10);
      return Number.isInteger(n) && n > 0;
    }, t('Max text chars must be a positive integer'));

  const minDurationMsSchema = z
    .string()
    .trim()
    .refine((v) => {
      if (!v) return true;
      const n = Number.parseInt(v, 10);
      return Number.isInteger(n) && n >= 0;
    }, t('Min duration must be an integer greater than or equal to 0'));

  const methodsSchema = z
    .string()
    .trim()
    .refine((v) => {
      if (!v) return true;
      return splitLines(v).every((m) => /^[A-Za-z]+$/.test(m));
    }, t('Methods must contain letters only, one method per line'));

  const statusCodesSchema = z
    .string()
    .trim()
    .refine((v) => {
      if (!v) return true;
      return splitLines(v).every((sc) => /^(\d{3}|[1-5]xx)$/i.test(sc));
    }, t('Status codes must be like 200, 429 or 2xx'));

  const retentionDaysSchema = z.enum(['7', '30', '90', 'null']);

  return z.object({
    enabled: z.boolean(),
    sampleRate: sampleRateSchema,
    retentionDays: retentionDaysSchema,
    sourcePatterns: z.string(),
    includeUrlPatterns: z.string(),
    excludeUrlPatterns: z.string(),
    methods: methodsSchema,
    statusCodes: statusCodesSchema,
    minDurationMs: minDurationMsSchema,
    includeRequestHeaders: z.string(),
    includeResponseHeaders: z.string(),
    redactRequestHeaders: z.string(),
    redactResponseHeaders: z.string(),
    captureRequestBody: z.boolean(),
    captureResponseBody: z.boolean(),
    captureRawRequestBody: z.boolean(),
    captureRawResponseBody: z.boolean(),
    maxTextCharsForTruncate: maxTextCharsSchema,
    allowedContentTypes: z.string(),
    redactJsonFields: z.string(),
  });
};

// ────────────────────────────── tab content components ──────────────────────────────

type TabFormProps = {
  t: (key: string) => string;
  form: ReturnType<typeof useForm<DirectionFormInput>>;
};

type QuickPreset = {
  label: string;
  description: string;
  partial: Partial<DirectionFormInput>;
};

const INBOUND_API_GATEWAY_URLS = [
  '/v1/chat/completions',
  '/v1-cached/chat/completions',
  '/v1-cached-createOnly/chat/completions',
  '/v1/images/generations',
  '/v1/images/edits',
  '/v1/models',
  '/v1/messages',
  '/v1/messages/count_tokens',
].join('\n');

function getQuickPresets(direction: TraceDirection, t: (key: string) => string): QuickPreset[] {
  const base: QuickPreset[] = [
    {
      label: t('All Requests'),
      description: t('Trace all requests with default settings'),
      partial: { enabled: true, excludeUrlPatterns: '/api/version/check-update\n/api/admin/request-trace*\n/api/admin/global-configs' },
    },
    {
      label: t('All Failed Requests'),
      description: t('Only trace requests with 4xx/5xx status codes'),
      partial: { enabled: true, statusCodes: '4xx\n5xx' },
    },
  ];

  if (direction === 'inbound') {
    base.push({
      label: t('API Gateway Requests'),
      description: t('Trace OpenAI/Anthropic compatible API gateway endpoints'),
      partial: { enabled: true, includeUrlPatterns: INBOUND_API_GATEWAY_URLS },
    });
  } else {
    base.push({
      label: t('API Gateway Requests'),
      description: t('Trace outbound API gateway calls'),
      partial: { enabled: true },
    });
  }

  return base;
}

function BasicSettingsTab({
  t,
  form,
  direction,
}: TabFormProps & { direction: TraceDirection }) {
  const presets = React.useMemo(() => getQuickPresets(direction, t), [direction, t]);

  const formatDaysLabel = (days: '7' | '30' | '90') => t('{} days').replace('{}', days);

  const retentionLabel = (value: DirectionFormInput['retentionDays']) => {
    if (value === '7') return formatDaysLabel('7');
    if (value === '30') return formatDaysLabel('30');
    if (value === '90') return formatDaysLabel('90');
    return t('Permanent');
  };

  const applyPreset = (preset: QuickPreset) => {
    const defaults = toFormValues(DEFAULT_DIRECTION_CONFIG);
    form.reset({ ...defaults, ...preset.partial });
  };

  return (
    <div className="space-y-4">
      <FormField
        control={form.control}
        name="enabled"
        render={({ field }) => (
          <FormItem className="flex flex-row items-center justify-between">
            <div className="space-y-0.5">
              <FormLabel>{t('Enabled')}</FormLabel>
              <FormDescription>{t('Whether this direction trace is enabled')}</FormDescription>
            </div>
            <FormControl>
              <Switch checked={field.value} onCheckedChange={field.onChange} />
            </FormControl>
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="sampleRate"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('Sample Rate')}</FormLabel>
            <FormControl>
              <Input type="number" step="0.01" min={0} max={1} placeholder="0 ~ 1" {...field} />
            </FormControl>
            <FormDescription>{t('Value between 0 and 1')}</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="retentionDays"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('Retention Policy')}</FormLabel>
            <FormControl>
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger value={field.value}>{retentionLabel(field.value)}</SelectTrigger>
                <SelectContent>
                  <SelectItem value="7">{formatDaysLabel('7')}</SelectItem>
                  <SelectItem value="30">{formatDaysLabel('30')}</SelectItem>
                  <SelectItem value="90">{formatDaysLabel('90')}</SelectItem>
                  <SelectItem value="null">{t('Permanent')}</SelectItem>
                </SelectContent>
              </Select>
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />

      {/* Quick Presets */}
      <div className="pt-2">
        <h4 className="text-sm font-medium mb-2">{t('Quick Presets')}</h4>
        <div className="grid grid-cols-1 gap-2 md:grid-cols-3">
          {presets.map((preset) => (
            <button
              key={preset.label}
              type="button"
              onClick={() => applyPreset(preset)}
              className="flex flex-col items-start gap-0.5 rounded-md border p-3 text-left transition-colors hover:bg-accent/50 hover:border-primary/40"
            >
              <span className="text-sm font-medium">{preset.label}</span>
              <span className="text-xs text-muted-foreground">{preset.description}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

function FiltersTab({ t, form }: TabFormProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <FormField
          control={form.control}
          name="sourcePatterns"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Source Patterns')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line, empty means null')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="methods"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('HTTP Methods')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={`GET\nPOST`} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="includeUrlPatterns"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Include URL Patterns')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line, empty means null')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="excludeUrlPatterns"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Exclude URL Patterns')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line, empty means null')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="statusCodes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Status Codes')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={`200\n429\n5xx`} {...field} />
              </FormControl>
              <FormDescription>{t('One item per line, supports exact code and x-group')}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="minDurationMs"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Min Duration (ms)')}</FormLabel>
              <FormControl>
                <Input type="number" min={0} placeholder={t('Empty means no threshold')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
    </div>
  );
}

function HeadersTab({ t, form }: TabFormProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <FormField
          control={form.control}
          name="includeRequestHeaders"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Include Request Headers')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line, empty means null')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="includeResponseHeaders"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Include Response Headers')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line, empty means null')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="redactRequestHeaders"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Redact Request Headers')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line')} {...field} />
              </FormControl>
              <FormDescription>{t('Sensitive headers that should be masked')}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="redactResponseHeaders"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Redact Response Headers')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
    </div>
  );
}

function BodyTab({ t, form }: TabFormProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <FormField
          control={form.control}
          name="captureRequestBody"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between rounded-md border p-3">
              <div className="space-y-0.5">
                <FormLabel>{t('Capture Request Body')}</FormLabel>
                <FormDescription>{t('Capture request body in text format')}</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="captureResponseBody"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between rounded-md border p-3">
              <div className="space-y-0.5">
                <FormLabel>{t('Capture Response Body')}</FormLabel>
                <FormDescription>{t('Capture response body in text format')}</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="captureRawRequestBody"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between rounded-md border p-3">
              <div className="space-y-0.5">
                <FormLabel>{t('Capture Raw Request Body')}</FormLabel>
                <FormDescription>{t('Capture raw binary request body')}</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="captureRawResponseBody"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between rounded-md border p-3">
              <div className="space-y-0.5">
                <FormLabel>{t('Capture Raw Response Body')}</FormLabel>
                <FormDescription>{t('Capture raw binary response body')}</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="maxTextCharsForTruncate"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Max Text Chars For Truncate')}</FormLabel>
              <FormControl>
                <Input
                  type="number"
                  min={1}
                  placeholder={t('Maximum text characters before truncation (text capture only)')}
                  {...field}
                />
              </FormControl>
              <FormDescription>{t('Only affects text body capture, not raw binary capture')}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="allowedContentTypes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Allowed Content Types')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line, empty means null')} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="redactJsonFields"
          render={({ field }) => (
            <FormItem className="md:col-span-2">
              <FormLabel>{t('Redact JSON Fields')}</FormLabel>
              <FormControl>
                <Textarea rows={4} placeholder={t('One item per line')} {...field} />
              </FormControl>
              <FormDescription>{t('JSON fields to mask in captured body')}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
    </div>
  );
}

// ────────────────────────────── main dialog component ──────────────────────────────

type RequestTraceConfigDialogProps = {
  direction: TraceDirection;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export default function RequestTraceConfigDialog({
  direction,
  open,
  onOpenChange,
}: RequestTraceConfigDialogProps) {
  const { t } = useTranslation();
  const schema = React.useMemo(() => createSchema(t), [t]);
  const configKey = direction === 'inbound' ? 'inboundRequestTrace' : 'outboundRequestTrace';

  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('basic');
  const [initialValues, setInitialValues] = useState<DirectionFormInput>(toFormValues(DEFAULT_DIRECTION_CONFIG));
  const [, setForceUpdateTrigger] = useState({});

  const form = useForm<DirectionFormInput>({
    resolver: zodResolver(schema),
    defaultValues: initialValues,
  });

  // Load config when dialog opens
  useEffect(() => {
    if (!open) return;
    setLoading(true);
    setActiveTab('basic');
    getConfigs()
      .then((configs) => {
        const found = configs.find((c) => c.key === configKey);
        const parsed = parseDirectionConfigFromValue(found?.value);
        const values = toFormValues(parsed);
        form.reset(values);
        setInitialValues(values);
      })
      .catch(() => {
        toast.error(t('Loading failed'));
      })
      .finally(() => setLoading(false));
  }, [open, configKey]);

  // Track changes
  useEffect(() => {
    const sub = form.watch(() => setForceUpdateTrigger({}));
    return () => sub.unsubscribe();
  }, [form]);

  const hasChanges = JSON.stringify(form.getValues()) !== JSON.stringify(initialValues);

  const copyConfig = () => {
    const config = toDirectionConfig(form.getValues());
    navigator.clipboard
      .writeText(JSON.stringify(config, null, 2))
      .then(() => toast.success(t('Copied to clipboard')))
      .catch(() => toast.error(t('Failed to copy')));
  };

  const onSubmit = async (values: DirectionFormInput) => {
    setSaving(true);
    try {
      await putConfigs({
        key: configKey,
        value: JSON.stringify(toDirectionConfig(values)),
        description: `${direction} request trace configuration`,
      });
      setInitialValues(values);
      onOpenChange(false);
    } catch {
      toast.error(t('Failed to save'));
    } finally {
      setSaving(false);
    }
  };

  const directionLabel = direction === 'inbound' ? t('Inbound') : t('Outbound');

  const tabs = [
    { id: 'basic', label: t('Basic Settings'), icon: <IconSettingsCog size={16} /> },
    { id: 'filters', label: t('Filters'), icon: <IconFilter size={16} /> },
    { id: 'headers', label: t('Headers'), icon: <IconCode size={16} /> },
    { id: 'body', label: t('Body'), icon: <IconFileText size={16} /> },
  ];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[85vh] flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <div className="flex items-center gap-2">
            <DialogTitle>
              {directionLabel} - {t('Request Trace Configuration')}
            </DialogTitle>
            <Button variant="outline" size="sm" onClick={copyConfig} className="h-8 w-8 p-0">
              <Copy className="h-4 w-4" />
            </Button>
          </div>
        </DialogHeader>

        {loading ? (
          <div className="flex-1 flex items-center justify-center text-sm text-muted-foreground">{t('Loading...')}</div>
        ) : (
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit(onSubmit)}
              className="flex-1 flex flex-col min-h-0 text-foreground [&_input]:text-foreground [&_textarea]:text-foreground"
            >
              {/* Top tabs */}
              <div className="shrink-0 border-b">
                <div className="flex">
                  {tabs.map((tab) => (
                    <button
                      key={tab.id}
                      type="button"
                      onClick={() => setActiveTab(tab.id)}
                      className={cn(
                        'flex-1 py-2.5 px-2 text-sm font-medium transition-colors text-center',
                        'hover:bg-accent/50',
                        activeTab === tab.id
                          ? 'text-primary border-b-2 border-primary bg-accent/30'
                          : 'text-muted-foreground',
                      )}
                    >
                      <span className="inline-flex items-center gap-1.5">
                        {tab.icon}
                        {tab.label}
                      </span>
                    </button>
                  ))}
                </div>
              </div>

              {/* Content area */}
              <div className="flex-1 overflow-y-auto px-6 relative">
                <div className={cn('py-4', activeTab === 'basic' ? 'block' : 'hidden')}>
                  <BasicSettingsTab t={t} form={form} direction={direction} />
                </div>
                <div className={cn('py-4', activeTab === 'filters' ? 'block' : 'hidden')}>
                  <FiltersTab t={t} form={form} />
                </div>
                <div className={cn('py-4', activeTab === 'headers' ? 'block' : 'hidden')}>
                  <HeadersTab t={t} form={form} />
                </div>
                <div className={cn('py-4', activeTab === 'body' ? 'block' : 'hidden')}>
                  <BodyTab t={t} form={form} />
                </div>
              </div>

              {/* Footer: action buttons */}
              <div className="shrink-0 border-t">
                <div className="flex justify-end gap-2 px-6 py-3">
                  <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                    {t('Cancel')}
                  </Button>
                  <Button type="submit" disabled={!hasChanges || saving} className="min-w-24">
                    {saving ? t('Saving...') : t('Save')}
                  </Button>
                </div>
              </div>
            </form>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  );
}
