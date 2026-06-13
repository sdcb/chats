import type { BeforeMount } from '@monaco-editor/react';
import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';

import { useTheme } from 'next-themes';
import dynamic from 'next/dynamic';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';
import { getUserSession } from '@/utils/user';

import { RequestTraceDetails } from '@/types/adminApis';

import {
  IconCheck,
  IconClipboard,
  IconDownload,
  IconInfo,
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import FloatingWindow from '@/components/ui/floating-window/FloatingWindow';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import { getRequestTraceDetails } from '@/apis/adminApis';
import { cn } from '@/lib/utils';

const MonacoEditor = dynamic(() => import('@monaco-editor/react'), {
  ssr: false,
  loading: () => <Skeleton className="h-full w-full" />,
});

const MONACO_EDITOR_OPTIONS = {
  automaticLayout: true,
  contextmenu: true,
  domReadOnly: true,
  fontSize: 12,
  lineNumbers: 'on',
  minimap: { enabled: false },
  readOnly: true,
  renderLineHighlight: 'none',
  scrollBeyondLastLine: false,
  tabSize: 2,
  wordWrap: 'on',
} as const;

const configureMonacoLanguages: BeforeMount = (monaco) => {
  const registeredLanguageIds = monaco.languages
    .getLanguages()
    .map((language: { id: string }) => language.id);

  if (!registeredLanguageIds.includes('jsonl')) {
    monaco.languages.register({ id: 'jsonl' });
    monaco.languages.setMonarchTokensProvider('jsonl', {
      tokenizer: {
        root: [
          [/[{}[\],:]/, 'delimiter'],
          [/"(?:[^"\\]|\\.)*"(?=\s*:)/, 'key'],
          [/"(?:[^"\\]|\\.)*"/, 'string'],
          [/-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/, 'number'],
          [/\b(?:true|false|null)\b/, 'keyword'],
        ],
      },
    });
  }

  if (!registeredLanguageIds.includes('http-headers')) {
    monaco.languages.register({ id: 'http-headers' });
    monaco.languages.setMonarchTokensProvider('http-headers', {
      tokenizer: {
        root: [
          [
            /^(?:HTTP\/\d(?:\.\d)?\s+\d{3}.*|[A-Z]+\s+\S+\s+HTTP\/\d(?:\.\d)?.*)$/,
            'keyword',
          ],
          [/^\s*:.*/, 'comment'],
          [
            /^([!#$%&'*+.^_`|~0-9A-Za-z-]+)(\s*:)/,
            ['type.identifier', 'delimiter'],
          ],
          [/.+/, 'string'],
        ],
      },
    });
  }
};

const getMonacoLanguageFromContentType = (
  contentType: string | null | undefined,
) => {
  const mediaType = (contentType || '').split(';', 1)[0].trim().toLowerCase();

  if (!mediaType) return 'plaintext';
  if (mediaType.includes('jsonl') || mediaType.includes('ndjson'))
    return 'jsonl';
  if (mediaType === 'application/json' || mediaType.endsWith('+json'))
    return 'json';
  if (mediaType === 'text/html' || mediaType === 'application/xhtml+xml')
    return 'html';
  if (mediaType === 'text/css') return 'css';
  if (
    mediaType === 'application/xml' ||
    mediaType === 'text/xml' ||
    mediaType.endsWith('+xml')
  ) {
    return 'xml';
  }
  if (
    mediaType === 'application/javascript' ||
    mediaType === 'text/javascript' ||
    mediaType === 'application/ecmascript' ||
    mediaType === 'text/ecmascript'
  ) {
    return 'javascript';
  }
  if (mediaType === 'text/typescript') return 'typescript';
  if (mediaType === 'text/markdown' || mediaType === 'text/x-markdown') {
    return 'markdown';
  }
  if (mediaType === 'application/yaml' || mediaType === 'text/yaml')
    return 'yaml';
  if (mediaType === 'application/sql') return 'sql';

  return 'plaintext';
};

const downloadByUrl = (url: string) => {
  window.open(url, '_blank');
};

const InlineCopyButton = ({
  value,
  className,
}: {
  value: string;
  className?: string;
}) => {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      return;
    }

    navigator.clipboard.writeText(value).then(() => {
      setCopied(true);
      setTimeout(() => {
        setCopied(false);
      }, 1200);
    });
  };

  return (
    <Button
      variant="ghost"
      size="sm"
      className={cn('h-7 w-7 p-0', className)}
      onClick={handleCopy}
      title={t('Copy')}
    >
      {copied ? <IconCheck size={16} /> : <IconClipboard size={16} />}
    </Button>
  );
};

const TraceMonacoViewer = ({
  value,
  language,
  active,
  onActivate,
}: {
  value: string | null | undefined;
  language: string;
  active: boolean;
  onActivate: () => void;
}) => {
  const { t } = useTranslation();
  const { resolvedTheme } = useTheme();
  const text = value || '';
  const hasContent = text.trim().length > 0;

  return (
    <div
      className="group relative h-full min-h-[240px] overflow-hidden"
      onClick={onActivate}
    >
      {hasContent && (
        <InlineCopyButton
          value={text}
          className={cn(
            'absolute top-2 right-2 z-10 opacity-0 pointer-events-none transition-opacity',
            'group-hover:opacity-100 group-hover:pointer-events-auto',
            'group-focus-within:opacity-100 group-focus-within:pointer-events-auto',
            active && 'opacity-100 pointer-events-auto',
          )}
        />
      )}

      {hasContent ? (
        <MonacoEditor
          beforeMount={configureMonacoLanguages}
          height="100%"
          language={language}
          options={MONACO_EDITOR_OPTIONS}
          theme={resolvedTheme === 'dark' ? 'vs-dark' : 'light'}
          value={text}
          width="100%"
        />
      ) : (
        <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
          {t('No data')}
        </div>
      )}
    </div>
  );
};

type RequestTraceDetailsDialogProps = {
  traceId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

type PayloadTabKey =
  | 'requestHeaders'
  | 'responseHeaders'
  | 'requestBody'
  | 'responseBody'
  | 'errorDetails';

export default function RequestTraceDetailsDialog({
  traceId,
  open,
  onOpenChange,
}: RequestTraceDetailsDialogProps) {
  const { t } = useTranslation();
  const [details, setDetails] = useState<RequestTraceDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [activePayloadTab, setActivePayloadTab] =
    useState<PayloadTabKey>('requestHeaders');
  const [activeCopyArea, setActiveCopyArea] = useState<
    'url' | 'payload' | null
  >(null);

  useEffect(() => {
    if (!open || !traceId) {
      return;
    }

    let cancelled = false;
    setDetails(null);
    setLoading(true);
    getRequestTraceDetails(traceId)
      .then((nextDetails) => {
        if (!cancelled) {
          setDetails(nextDetails);
        }
      })
      .catch((error) => {
        if (cancelled) {
          return;
        }
        console.error(error);
        toast.error(
          t(
            'Operation failed, Please try again later, or contact technical personnel',
          ),
        );
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [open, traceId, t]);

  useEffect(() => {
    if (open) {
      setActivePayloadTab('requestHeaders');
      setActiveCopyArea(null);
    }
  }, [open, traceId]);

  const renderPayloadContent = (
    content: string | null | undefined,
    language: string,
  ) => (
    <TraceMonacoViewer
      active={activeCopyArea === 'payload'}
      language={language}
      onActivate={() => setActiveCopyArea('payload')}
      value={content}
    />
  );

  if (!open || typeof document === 'undefined') {
    return null;
  }

  return createPortal(
    <FloatingWindow
      open={open}
      onOpenChange={onOpenChange}
      title={
        <span className="flex items-center gap-2">
          <IconInfo size={18} />
          <span className="truncate">{`${t('Request Trace Details')}：${
            details?.traceId || '-'
          }`}</span>
        </span>
      }
      defaultSize={{ width: 1120, height: 780 }}
      minSize={{ width: 640, height: 460 }}
      className="w-[min(100vw,1120px)]"
    >
      <div className="flex h-full min-h-0 flex-col">
        {loading || !details ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 6 }).map((_, idx) => (
              <Skeleton key={idx} className="h-6 w-full" />
            ))}
          </div>
        ) : (
          <div className="flex h-full min-h-0 flex-col gap-3 p-3">
            <div
              className="group relative shrink-0 rounded-md border bg-muted/20 p-3 text-sm"
              onClick={() => setActiveCopyArea('url')}
            >
              <div className="break-all pr-8 leading-5 font-mono">
                {details.method} {details.url}
              </div>
              <InlineCopyButton
                value={`${details.method} ${details.url}`}
                className={cn(
                  'absolute top-2 right-2 z-10 h-5 w-5 opacity-0 pointer-events-none transition-opacity',
                  'group-hover:opacity-100 group-hover:pointer-events-auto',
                  'group-focus-within:opacity-100 group-focus-within:pointer-events-auto',
                  activeCopyArea === 'url' && 'opacity-100 pointer-events-auto',
                )}
              />
            </div>

            <div className="min-h-0 flex-1 overflow-hidden rounded-md border">
              <Tabs
                value={activePayloadTab}
                onValueChange={(value) =>
                  setActivePayloadTab(value as PayloadTabKey)
                }
                className="flex h-full min-h-0 flex-col gap-0 border-0 p-0"
              >
                <div className="flex h-auto shrink-0 items-center gap-2 border-b bg-transparent p-1">
                  <TabsList className="min-w-0 flex-1 flex-row flex-wrap items-center justify-start rounded-none bg-transparent p-0">
                    <TabsTrigger value="requestHeaders">
                      {t('Request Headers')}
                    </TabsTrigger>
                    <TabsTrigger value="responseHeaders">
                      {t('Response Headers')}
                    </TabsTrigger>
                    <TabsTrigger value="requestBody">
                      {t('Request Body')}
                    </TabsTrigger>
                    <TabsTrigger value="responseBody">
                      {t('Response Body')}
                    </TabsTrigger>
                    <TabsTrigger value="errorDetails">
                      {t('Error Details')}
                    </TabsTrigger>
                  </TabsList>

                  <Button
                    size="sm"
                    variant="outline"
                    className="h-8 shrink-0"
                    onClick={() =>
                      downloadByUrl(
                        `${getApiUrl()}/api/admin/request-trace/${
                          details.id
                        }/dump?token=${encodeURIComponent(
                          getUserSession() || '',
                        )}`,
                      )
                    }
                  >
                    <IconDownload size={14} className="mr-1" />
                    {t('Download Dump')}
                  </Button>
                </div>

                <TabsContent
                  value="requestHeaders"
                  className="m-0 min-h-0 flex-1 overflow-hidden data-[state=inactive]:hidden"
                >
                  {renderPayloadContent(details.requestHeaders, 'http-headers')}
                </TabsContent>
                <TabsContent
                  value="responseHeaders"
                  className="m-0 min-h-0 flex-1 overflow-hidden data-[state=inactive]:hidden"
                >
                  {renderPayloadContent(
                    details.responseHeaders,
                    'http-headers',
                  )}
                </TabsContent>
                <TabsContent
                  value="requestBody"
                  className="m-0 min-h-0 flex-1 overflow-hidden data-[state=inactive]:hidden"
                >
                  {renderPayloadContent(
                    details.requestBody,
                    getMonacoLanguageFromContentType(
                      details.requestContentType,
                    ),
                  )}
                </TabsContent>
                <TabsContent
                  value="responseBody"
                  className="m-0 min-h-0 flex-1 overflow-hidden data-[state=inactive]:hidden"
                >
                  {renderPayloadContent(
                    details.responseBody,
                    getMonacoLanguageFromContentType(
                      details.responseContentType,
                    ),
                  )}
                </TabsContent>
                <TabsContent
                  value="errorDetails"
                  className="m-0 min-h-0 flex-1 overflow-hidden data-[state=inactive]:hidden"
                >
                  {renderPayloadContent(
                    [
                      details.errorType
                        ? `${t('Error Type')}: ${details.errorType}`
                        : '',
                      details.errorMessage || '',
                    ]
                      .filter(Boolean)
                      .join('\n\n'),
                    'plaintext',
                  )}
                </TabsContent>
              </Tabs>
            </div>

            {(details.hasRequestBodyRaw || details.hasResponseBodyRaw) && (
              <div className="flex shrink-0 flex-wrap justify-end gap-2">
                {details.hasRequestBodyRaw && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      downloadByUrl(
                        `${getApiUrl()}/api/admin/request-trace/${
                          details.id
                        }/raw?part=request&token=${encodeURIComponent(
                          getUserSession() || '',
                        )}`,
                      )
                    }
                  >
                    <IconDownload size={14} className="mr-1" />
                    {t('Download Request Raw')}
                  </Button>
                )}
                {details.hasResponseBodyRaw && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      downloadByUrl(
                        `${getApiUrl()}/api/admin/request-trace/${
                          details.id
                        }/raw?part=response&token=${encodeURIComponent(
                          getUserSession() || '',
                        )}`,
                      )
                    }
                  >
                    <IconDownload size={14} className="mr-1" />
                    {t('Download Response Raw')}
                  </Button>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </FloatingWindow>,
    document.body,
  );
}
