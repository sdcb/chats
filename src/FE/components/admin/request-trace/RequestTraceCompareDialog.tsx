import React, { useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { getRequestTraceDetails } from '@/apis/adminApis';
import { IconCheck, IconClipboard } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { RequestTraceDetails } from '@/types/adminApis';
import { formatDateTime } from '@/utils/date';

import {
  ALL_COLUMNS,
  ColumnKey,
  getDirectionLabel,
  getDurationMs,
} from './requestTraceColumns';

type CompareExtraFieldKey =
  | 'requestHeaders'
  | 'responseHeaders'
  | 'requestBody'
  | 'responseBody'
  | 'errorMessage';

const COMPARE_EXTRA_FIELDS: { key: CompareExtraFieldKey; title: string }[] = [
  { key: 'requestHeaders', title: 'Request Headers' },
  { key: 'responseHeaders', title: 'Response Headers' },
  { key: 'requestBody', title: 'Request Body' },
  { key: 'responseBody', title: 'Response Body' },
  { key: 'errorMessage', title: 'Error Message' },
];

const formatCompareValue = (value: string | number | boolean | null | undefined) => {
  if (value == null || value === '') {
    return '';
  }

  return String(value);
};

const getCompareColumnValue = (
  row: RequestTraceDetails,
  key: ColumnKey,
  t: (key: string) => string,
) => {
  switch (key) {
    case 'id':
      return row.id;
    case 'startedAt':
      return formatDateTime(row.startedAt);
    case 'requestBodyAt':
      return row.requestBodyAt ? formatDateTime(row.requestBodyAt) : '';
    case 'responseHeaderAt':
      return row.responseHeaderAt ? formatDateTime(row.responseHeaderAt) : '';
    case 'responseBodyAt':
      return row.responseBodyAt ? formatDateTime(row.responseBodyAt) : '';
    case 'direction':
      return getDirectionLabel(row.direction, t);
    case 'method':
      return row.method;
    case 'url':
      return row.url;
    case 'statusCode':
      return formatCompareValue(row.statusCode);
    case 'durationMs':
      return formatCompareValue(getDurationMs(row));
    case 'userId':
      return formatCompareValue(row.userId);
    case 'traceId':
      return formatCompareValue(row.traceId);
    case 'userName':
      return formatCompareValue(row.userName);
    case 'source':
      return formatCompareValue(row.source);
    case 'requestContentType':
      return formatCompareValue(row.requestContentType);
    case 'responseContentType':
      return formatCompareValue(row.responseContentType);
    case 'errorType':
      return formatCompareValue(row.errorType);
    case 'rawRequestBodyBytes':
      return formatCompareValue(row.rawRequestBodyBytes);
    case 'rawResponseBodyBytes':
      return formatCompareValue(row.rawResponseBodyBytes);
    case 'requestBodyLength':
      return formatCompareValue(row.requestBodyLength);
    case 'responseBodyLength':
      return formatCompareValue(row.responseBodyLength);
    case 'hasPayload':
      return row.hasPayload ? t('Yes') : t('No');
    case 'hasRequestBodyRaw':
      return row.hasRequestBodyRaw ? t('Yes') : t('No');
    case 'hasResponseBodyRaw':
      return row.hasResponseBodyRaw ? t('Yes') : t('No');
    default:
      return '';
  }
};

const getCompareExtraValue = (row: RequestTraceDetails, key: CompareExtraFieldKey) => {
  switch (key) {
    case 'requestHeaders':
      return formatCompareValue(row.requestHeaders);
    case 'responseHeaders':
      return formatCompareValue(row.responseHeaders);
    case 'requestBody':
      return formatCompareValue(row.requestBody);
    case 'responseBody':
      return formatCompareValue(row.responseBody);
    case 'errorMessage':
      return formatCompareValue(row.errorMessage);
    default:
      return '';
  }
};

const CompareCopyButton = ({ value, className }: { value: string; className?: string }) => {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const timeoutRef = useRef<number | null>(null);

  useEffect(() => {
    return () => {
      if (timeoutRef.current !== null) {
        window.clearTimeout(timeoutRef.current);
      }
    };
  }, []);

  const handleCopy = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();

    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      return;
    }

    navigator.clipboard.writeText(value).then(() => {
      setCopied(true);

      if (timeoutRef.current !== null) {
        window.clearTimeout(timeoutRef.current);
      }

      timeoutRef.current = window.setTimeout(() => {
        setCopied(false);
      }, 1200);
    });
  };

  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      className={cn('h-7 w-7 p-0', copied && 'opacity-100 pointer-events-auto', className)}
      onClick={handleCopy}
      title={t('Copy')}
      aria-label={t('Copy')}
    >
      {copied ? <IconCheck size={16} /> : <IconClipboard size={16} />}
    </Button>
  );
};

type RequestTraceCompareDialogProps = {
  ids: string[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export default function RequestTraceCompareDialog({
  ids,
  open,
  onOpenChange,
}: RequestTraceCompareDialogProps) {
  const { t } = useTranslation();
  const [left, setLeft] = useState<RequestTraceDetails | null>(null);
  const [right, setRight] = useState<RequestTraceDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [hideSame, setHideSame] = useState(false);

  useEffect(() => {
    if (!open || ids.length !== 2) {
      return;
    }

    setLoading(true);
    Promise.all([getRequestTraceDetails(ids[0]), getRequestTraceDetails(ids[1])])
      .then(([a, b]) => {
        setLeft(a);
        setRight(b);
      })
      .catch((error) => {
        console.error(error);
        toast.error(t('Operation failed, Please try again later, or contact technical personnel'));
      })
      .finally(() => {
        setLoading(false);
      });
  }, [open, ids, t]);

  const rows = useMemo(() => {
    if (!left || !right) return [] as { label: string; a: string; b: string }[];

    const base: { label: string; a: string; b: string }[] = [
      ...ALL_COLUMNS.map((column) => ({
        label: t(column.title),
        a: getCompareColumnValue(left, column.key, t),
        b: getCompareColumnValue(right, column.key, t),
      })),
      ...COMPARE_EXTRA_FIELDS.map((field) => ({
        label: t(field.title),
        a: getCompareExtraValue(left, field.key),
        b: getCompareExtraValue(right, field.key),
      })),
    ];

    if (!hideSame) return base;
    return base.filter((x) => x.a !== x.b);
  }, [hideSame, left, right, t]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-6xl h-[85vh] max-h-[85vh] grid-rows-[auto_auto_minmax(0,1fr)] overflow-hidden">
        <DialogHeader>
          <DialogTitle>{t('Request Trace Compare')}</DialogTitle>
        </DialogHeader>

        <div className="flex items-center justify-end gap-2 text-sm">
          <span>{t('Hide same values')}</span>
          <Switch checked={hideSame} onCheckedChange={setHideSame} />
        </div>

        {loading ? (
          <div className="custom-scrollbar min-h-0 h-full overflow-y-auto pr-1">
            <div className="space-y-2 py-4">
              {Array.from({ length: 6 }).map((_, idx) => (
                <Skeleton key={idx} className="h-8 w-full" />
              ))}
            </div>
          </div>
        ) : (
          <div className="custom-scrollbar min-h-0 h-full overflow-x-auto overflow-y-scroll pr-1">
            <div className="space-y-2">
              {rows.map((row) => {
                const different = row.a !== row.b;
                return (
                  <div key={row.label} className="grid grid-cols-[220px_1fr_1fr] items-start gap-2">
                    <div className="px-2 py-2 text-xs text-muted-foreground">{row.label}</div>
                    <Card
                      tabIndex={different ? 0 : -1}
                      className={cn(
                        'p-2 text-xs whitespace-pre-wrap break-all focus:outline-none',
                        different ? 'group relative' : 'border-0 bg-transparent shadow-none',
                      )}
                    >
                      {different && (
                        <CompareCopyButton
                          value={row.a || '-'}
                          className="absolute right-1 top-1 z-10 opacity-0 pointer-events-none transition-opacity group-hover:opacity-100 group-hover:pointer-events-auto group-focus-within:opacity-100 group-focus-within:pointer-events-auto"
                        />
                      )}
                      <div className={cn(different && 'pr-8')}>{row.a || '-'}</div>
                    </Card>
                    <Card
                      tabIndex={different ? 0 : -1}
                      className={cn(
                        'p-2 text-xs whitespace-pre-wrap break-all focus:outline-none',
                        different ? 'group relative' : 'border-0 bg-transparent shadow-none',
                      )}
                    >
                      {different && (
                        <CompareCopyButton
                          value={row.b || '-'}
                          className="absolute right-1 top-1 z-10 opacity-0 pointer-events-none transition-opacity group-hover:opacity-100 group-hover:pointer-events-auto group-focus-within:opacity-100 group-focus-within:pointer-events-auto"
                        />
                      )}
                      <div className={cn(different && 'pr-8')}>{row.b || '-'}</div>
                    </Card>
                  </div>
                );
              })}
              {!loading && rows.length === 0 && (
                <div className="text-center py-8 text-sm text-muted-foreground">{t('No difference')}</div>
              )}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}