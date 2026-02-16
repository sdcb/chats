import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowDownToLine, ArrowUpFromLine, Copy } from 'lucide-react';
import toast from 'react-hot-toast';
import { z } from 'zod';

import { putConfigs } from '@/apis/adminApis';
import useTranslation from '@/hooks/useTranslation';
import { GetConfigsResult } from '@/types/adminApis';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';

interface RequestTraceGlobalConfigProps {
  configs: GetConfigsResult[];
  onConfigsUpdate: () => void;
}

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
  maxBytes: number;
  allowedContentTypes: string[] | null;
  redactJsonFields: string[];
}

interface RequestTraceDirectionConfig {
  enabled: boolean;
  sampleRate: number;
  filters: RequestTraceFilters;
  headers: RequestTraceHeaderConfig;
  body: RequestTraceBodyConfig;
}

interface RequestTraceDirectionFormInput {
  enabled: boolean;
  sampleRate: string;
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
  maxBytes: string;
  allowedContentTypes: string;
  redactJsonFields: string;
}

interface RequestTraceFormData {
  inbound: RequestTraceDirectionFormInput;
  outbound: RequestTraceDirectionFormInput;
}

type TraceDirection = 'inbound' | 'outbound';
type DirectionFieldKey = keyof RequestTraceDirectionFormInput;

const DEFAULT_DIRECTION_CONFIG: RequestTraceDirectionConfig = {
  enabled: false,
  sampleRate: 1,
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
    maxBytes: 5 * 1024 * 1024,
    allowedContentTypes: null,
    redactJsonFields: ['password', 'token', 'secret', 'apiKey', 'access_token', 'refresh_token'],
  },
};

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
  if (!trimmed) {
    return null;
  }

  const parsed = Number.parseInt(trimmed, 10);
  if (!Number.isFinite(parsed) || parsed < 0) {
    return null;
  }

  return parsed;
};

const toDirectionFieldPath = <K extends DirectionFieldKey>(
  direction: TraceDirection,
  key: K,
): `inbound.${K}` | `outbound.${K}` => `${direction}.${key}` as `inbound.${K}` | `outbound.${K}`;

const normalizeDirectionConfig = (
  rawValue: unknown,
): RequestTraceDirectionConfig => {
  if (!rawValue || typeof rawValue !== 'object') {
    return DEFAULT_DIRECTION_CONFIG;
  }

  const data = rawValue as Partial<RequestTraceDirectionConfig>;

  return {
    enabled: data.enabled ?? DEFAULT_DIRECTION_CONFIG.enabled,
    sampleRate:
      typeof data.sampleRate === 'number'
        ? data.sampleRate
        : DEFAULT_DIRECTION_CONFIG.sampleRate,
    filters: {
      sourcePatterns: data.filters?.sourcePatterns ?? DEFAULT_DIRECTION_CONFIG.filters.sourcePatterns,
      includeUrlPatterns:
        data.filters?.includeUrlPatterns ?? DEFAULT_DIRECTION_CONFIG.filters.includeUrlPatterns,
      excludeUrlPatterns:
        data.filters?.excludeUrlPatterns ?? DEFAULT_DIRECTION_CONFIG.filters.excludeUrlPatterns,
      methods: data.filters?.methods ?? DEFAULT_DIRECTION_CONFIG.filters.methods,
      statusCodes: data.filters?.statusCodes ?? DEFAULT_DIRECTION_CONFIG.filters.statusCodes,
      minDurationMs: data.filters?.minDurationMs ?? DEFAULT_DIRECTION_CONFIG.filters.minDurationMs,
    },
    headers: {
      includeRequestHeaders:
        data.headers?.includeRequestHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.includeRequestHeaders,
      includeResponseHeaders:
        data.headers?.includeResponseHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.includeResponseHeaders,
      redactRequestHeaders:
        data.headers?.redactRequestHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.redactRequestHeaders,
      redactResponseHeaders:
        data.headers?.redactResponseHeaders ?? DEFAULT_DIRECTION_CONFIG.headers.redactResponseHeaders,
    },
    body: {
      captureRequestBody:
        data.body?.captureRequestBody ?? DEFAULT_DIRECTION_CONFIG.body.captureRequestBody,
      captureResponseBody:
        data.body?.captureResponseBody ?? DEFAULT_DIRECTION_CONFIG.body.captureResponseBody,
      captureRawRequestBody:
        data.body?.captureRawRequestBody ?? DEFAULT_DIRECTION_CONFIG.body.captureRawRequestBody,
      captureRawResponseBody:
        data.body?.captureRawResponseBody ?? DEFAULT_DIRECTION_CONFIG.body.captureRawResponseBody,
      maxBytes: data.body?.maxBytes ?? DEFAULT_DIRECTION_CONFIG.body.maxBytes,
      allowedContentTypes:
        data.body?.allowedContentTypes ?? DEFAULT_DIRECTION_CONFIG.body.allowedContentTypes,
      redactJsonFields: data.body?.redactJsonFields ?? DEFAULT_DIRECTION_CONFIG.body.redactJsonFields,
    },
  };
};

const parseDirectionConfigFromValue = (
  value?: string,
): RequestTraceDirectionConfig => {
  if (!value) {
    return DEFAULT_DIRECTION_CONFIG;
  }

  try {
    return normalizeDirectionConfig(JSON.parse(value));
  } catch {
    return DEFAULT_DIRECTION_CONFIG;
  }
};

const toFormDirection = (
  config: RequestTraceDirectionConfig,
): RequestTraceDirectionFormInput => ({
  enabled: config.enabled,
  sampleRate: String(config.sampleRate),
  sourcePatterns: toTextareaValue(config.filters.sourcePatterns),
  includeUrlPatterns: toTextareaValue(config.filters.includeUrlPatterns),
  excludeUrlPatterns: toTextareaValue(config.filters.excludeUrlPatterns),
  methods: toTextareaValue(config.filters.methods),
  statusCodes: toTextareaValue(config.filters.statusCodes),
  minDurationMs:
    config.filters.minDurationMs === null || config.filters.minDurationMs === undefined
      ? ''
      : String(config.filters.minDurationMs),
  includeRequestHeaders: toTextareaValue(config.headers.includeRequestHeaders),
  includeResponseHeaders: toTextareaValue(config.headers.includeResponseHeaders),
  redactRequestHeaders: toTextareaValue(config.headers.redactRequestHeaders),
  redactResponseHeaders: toTextareaValue(config.headers.redactResponseHeaders),
  captureRequestBody: config.body.captureRequestBody,
  captureResponseBody: config.body.captureResponseBody,
  captureRawRequestBody: config.body.captureRawRequestBody,
  captureRawResponseBody: config.body.captureRawResponseBody,
  maxBytes: String(config.body.maxBytes),
  allowedContentTypes: toTextareaValue(config.body.allowedContentTypes),
  redactJsonFields: toTextareaValue(config.body.redactJsonFields),
});

const toDirectionConfig = (
  data: RequestTraceDirectionFormInput,
): RequestTraceDirectionConfig => ({
  enabled: data.enabled,
  sampleRate: Number.parseFloat(data.sampleRate),
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
    maxBytes: Number.parseInt(data.maxBytes, 10),
    allowedContentTypes: toNullableArray(data.allowedContentTypes),
    redactJsonFields: toRequiredArray(data.redactJsonFields),
  },
});

const createRequestTraceSchema = (t: (key: string) => string) => {
  const sampleRateSchema = z
    .string()
    .trim()
    .refine((value) => value.length > 0, t('Sample rate is required'))
    .refine((value) => {
      const parsed = Number.parseFloat(value);
      return Number.isFinite(parsed) && parsed >= 0 && parsed <= 1;
    }, t('Sample rate must be between 0 and 1'));

  const maxBytesSchema = z
    .string()
    .trim()
    .refine((value) => value.length > 0, t('Max bytes is required'))
    .refine((value) => {
      const parsed = Number.parseInt(value, 10);
      return Number.isInteger(parsed) && parsed > 0;
    }, t('Max bytes must be a positive integer'));

  const minDurationMsSchema = z
    .string()
    .trim()
    .refine((value) => {
      if (!value) {
        return true;
      }

      const parsed = Number.parseInt(value, 10);
      return Number.isInteger(parsed) && parsed >= 0;
    }, t('Min duration must be an integer greater than or equal to 0'));

  const methodsSchema = z
    .string()
    .trim()
    .refine((value) => {
      if (!value) {
        return true;
      }

      return splitLines(value).every((method) => /^[A-Za-z]+$/.test(method));
    }, t('Methods must contain letters only, one method per line'));

  const statusCodesSchema = z
    .string()
    .trim()
    .refine((value) => {
      if (!value) {
        return true;
      }

      return splitLines(value).every((statusCode) => /^(\d{3}|[1-5]xx)$/i.test(statusCode));
    }, t('Status codes must be like 200, 429 or 2xx'));

  const directionSchema = z.object({
    enabled: z.boolean(),
    sampleRate: sampleRateSchema,
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
    maxBytes: maxBytesSchema,
    allowedContentTypes: z.string(),
    redactJsonFields: z.string(),
  });

  return z.object({
    inbound: directionSchema,
    outbound: directionSchema,
  });
};

function DirectionFields({
  direction,
  t,
  form,
}: {
  direction: TraceDirection;
  t: (key: string) => string;
  form: ReturnType<typeof useForm<RequestTraceFormData>>;
}) {
  return (
    <div className="space-y-6">
      <div className="space-y-4 rounded-md border p-4">
        <div className="text-sm font-medium">{t('Basic Settings')}</div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'enabled')}
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-md border p-3">
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
            name={toDirectionFieldPath(direction, 'sampleRate')}
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
        </div>
      </div>

      <div className="space-y-4 rounded-md border p-4">
        <div className="text-sm font-medium">{t('Filters')}</div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'sourcePatterns')}
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
            name={toDirectionFieldPath(direction, 'methods')}
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('HTTP Methods')}</FormLabel>
                <FormControl>
                  <Textarea
                    rows={4}
                    placeholder={`GET
POST`}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'includeUrlPatterns')}
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
            name={toDirectionFieldPath(direction, 'excludeUrlPatterns')}
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
            name={toDirectionFieldPath(direction, 'statusCodes')}
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('Status Codes')}</FormLabel>
                <FormControl>
                  <Textarea
                    rows={4}
                    placeholder={`200
429
5xx`}
                    {...field}
                  />
                </FormControl>
                <FormDescription>{t('One item per line, supports exact code and x-group')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'minDurationMs')}
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

      <div className="space-y-4 rounded-md border p-4">
        <div className="text-sm font-medium">{t('Headers')}</div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'includeRequestHeaders')}
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
            name={toDirectionFieldPath(direction, 'includeResponseHeaders')}
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
            name={toDirectionFieldPath(direction, 'redactRequestHeaders')}
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
            name={toDirectionFieldPath(direction, 'redactResponseHeaders')}
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

      <div className="space-y-4 rounded-md border p-4">
        <div className="text-sm font-medium">{t('Body')}</div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'captureRequestBody')}
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
            name={toDirectionFieldPath(direction, 'captureResponseBody')}
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
            name={toDirectionFieldPath(direction, 'captureRawRequestBody')}
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
            name={toDirectionFieldPath(direction, 'captureRawResponseBody')}
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
            name={toDirectionFieldPath(direction, 'maxBytes')}
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('Max Bytes')}</FormLabel>
                <FormControl>
                  <Input type="number" min={1} placeholder={t('Maximum bytes for body capture')} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name={toDirectionFieldPath(direction, 'allowedContentTypes')}
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
            name={toDirectionFieldPath(direction, 'redactJsonFields')}
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
    </div>
  );
}

export default function RequestTraceGlobalConfig({ configs, onConfigsUpdate }: RequestTraceGlobalConfigProps) {
  const { t } = useTranslation();
  const requestTraceSchema = React.useMemo(() => createRequestTraceSchema(t), [t]);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState<TraceDirection>('inbound');
  const [initialValues, setInitialValues] = useState<RequestTraceFormData>({
    inbound: toFormDirection(DEFAULT_DIRECTION_CONFIG),
    outbound: toFormDirection(DEFAULT_DIRECTION_CONFIG),
  });
  const [forceUpdateTrigger, setForceUpdateTrigger] = useState({});

  const tabMeta = [
    {
      value: 'inbound' as TraceDirection,
      label: t('Inbound'),
      icon: <ArrowDownToLine size={16} />,
    },
    {
      value: 'outbound' as TraceDirection,
      label: t('Outbound'),
      icon: <ArrowUpFromLine size={16} />,
    },
  ];

  const form = useForm<RequestTraceFormData>({
    resolver: zodResolver(requestTraceSchema),
    defaultValues: initialValues,
  });

  useEffect(() => {
    const inboundConfig = configs.find((item) => item.key === 'inboundRequestTrace');
    const outboundConfig = configs.find((item) => item.key === 'outboundRequestTrace');

    const resolvedValues: RequestTraceFormData = {
      inbound: toFormDirection(parseDirectionConfigFromValue(inboundConfig?.value)),
      outbound: toFormDirection(parseDirectionConfigFromValue(outboundConfig?.value)),
    };

    form.reset(resolvedValues);
    setInitialValues(resolvedValues);
  }, [configs]);

  useEffect(() => {
    const subscription = form.watch(() => {
      setForceUpdateTrigger({});
    });

    return () => subscription.unsubscribe();
  }, [form]);

  const hasChanges =
    JSON.stringify(form.getValues()) !== JSON.stringify(initialValues);

  const copyCurrentTabConfig = () => {
    const currentValues = form.getValues(activeTab);
    const directionConfig = toDirectionConfig(currentValues);
    const jsonString = JSON.stringify(directionConfig, null, 2);

    navigator.clipboard
      .writeText(jsonString)
      .then(() => {
        toast.success(t('Copied to clipboard'));
      })
      .catch(() => {
        toast.error(t('Failed to copy'));
      });
  };

  const onSubmit = async (values: RequestTraceFormData) => {
    setSaving(true);

    try {
      await putConfigs({
        key: 'inboundRequestTrace',
        value: JSON.stringify(toDirectionConfig(values.inbound)),
        description: 'Inbound request trace configuration',
      });

      await putConfigs({
        key: 'outboundRequestTrace',
        value: JSON.stringify(toDirectionConfig(values.outbound)),
        description: 'Outbound request trace configuration',
      });

      toast.success(t('Save successful'));
      setInitialValues(values);
      onConfigsUpdate();
    } catch {
      toast.error(t('Failed to save'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card className="w-full">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-6">
        <div className="flex items-center space-x-3">
          <CardTitle className="text-lg font-medium">
            {t('Request Trace Configuration')}
          </CardTitle>
          <Button
            variant="outline"
            size="sm"
            onClick={copyCurrentTabConfig}
            className="h-8 w-8 p-0"
          >
            <Copy className="h-4 w-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form
            onSubmit={form.handleSubmit(onSubmit)}
            className="space-y-6 text-foreground [&_input]:text-foreground [&_textarea]:text-foreground"
          >
            <Tabs
              value={activeTab}
              onValueChange={(value) => setActiveTab(value as TraceDirection)}
              orientation="horizontal"
              className="flex-col gap-3 border-none p-0 text-foreground"
            >
              <div className="flex w-full justify-center">
                <TabsList className="inline-flex flex-row items-center justify-center rounded-full bg-muted p-1 gap-0 shadow-sm border border-border/60">
                  {tabMeta.map((tab) => (
                    <TabsTrigger
                      key={tab.value}
                      value={tab.value}
                      className="flex items-center gap-2 px-5 py-2 text-sm rounded-full data-[state=active]:bg-background data-[state=active]:text-foreground transition-colors focus-visible:ring-0 focus-visible:ring-offset-0 hover:text-foreground/90 first:rounded-l-full last:rounded-r-full"
                    >
                      {tab.icon}
                      <span>{tab.label}</span>
                    </TabsTrigger>
                  ))}
                </TabsList>
              </div>

              <TabsContent value="inbound" className="ml-0 mt-3 w-full text-foreground">
                <DirectionFields direction="inbound" t={t} form={form} />
              </TabsContent>
              <TabsContent value="outbound" className="ml-0 mt-3 w-full text-foreground">
                <DirectionFields direction="outbound" t={t} form={form} />
              </TabsContent>
            </Tabs>

            <div className="flex justify-end">
              <Button type="submit" disabled={!hasChanges || saving} className="min-w-24">
                {saving ? t('Saving...') : t('Save')}
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
