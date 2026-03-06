import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import { getRequestTraceDetails } from '@/apis/adminApis';
import { IconCheck, IconClipboard, IconDownload } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { RequestTraceDetails } from '@/types/adminApis';
import { getApiUrl } from '@/utils/common';
import { getUserSession } from '@/utils/user';

const downloadByUrl = (url: string) => {
  window.open(url, '_blank');
};

const InlineCopyButton = ({ value, className }: { value: string; className?: string }) => {
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

type RequestTraceDetailsDialogProps = {
  traceId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export default function RequestTraceDetailsDialog({
  traceId,
  open,
  onOpenChange,
}: RequestTraceDetailsDialogProps) {
  const { t } = useTranslation();
  const [details, setDetails] = useState<RequestTraceDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [activePayloadTab, setActivePayloadTab] = useState<'requestHeaders' | 'responseHeaders' | 'requestBody' | 'responseBody'>('requestHeaders');
  const [activeCopyArea, setActiveCopyArea] = useState<'url' | 'payload' | null>(null);

  useEffect(() => {
    if (!open || !traceId) {
      return;
    }

    setLoading(true);
    getRequestTraceDetails(traceId)
      .then(setDetails)
      .catch((error) => {
        console.error(error);
        toast.error(t('Operation failed, Please try again later, or contact technical personnel'));
      })
      .finally(() => {
        setLoading(false);
      });
  }, [open, traceId, t]);

  useEffect(() => {
    if (open) {
      setActivePayloadTab('requestHeaders');
      setActiveCopyArea(null);
    }
  }, [open, traceId]);

  const renderPayloadContent = (content: string | null | undefined) => {
    const text = content || '';
    const hasContent = text.trim().length > 0;

    return (
      <div
        className="group relative h-[360px] overflow-hidden"
        onClick={() => setActiveCopyArea('payload')}
      >
        {hasContent && (
          <InlineCopyButton
            value={text}
            className={cn(
              'absolute top-2 right-2 z-10 opacity-0 pointer-events-none transition-opacity',
              'group-hover:opacity-100 group-hover:pointer-events-auto',
              'group-focus-within:opacity-100 group-focus-within:pointer-events-auto',
              activeCopyArea === 'payload' && 'opacity-100 pointer-events-auto',
            )}
          />
        )}

        {hasContent ? (
          <pre className="custom-scrollbar h-full overflow-x-auto overflow-y-auto whitespace-pre-wrap break-all p-3 text-xs font-mono">{text}</pre>
        ) : (
          <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
            {t('No data')}
          </div>
        )}
      </div>
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-hidden sm:max-w-5xl">
        <DialogHeader>
          <DialogTitle>
            {`${t('Request Trace Details')}：${details?.traceId || '-'}`}
          </DialogTitle>
        </DialogHeader>

        {loading || !details ? (
          <div className="space-y-2 py-4">
            {Array.from({ length: 6 }).map((_, idx) => (
              <Skeleton key={idx} className="h-6 w-full" />
            ))}
          </div>
        ) : (
          <div className="custom-scrollbar space-y-4 overflow-x-hidden overflow-y-auto">
            <div
              className="group relative rounded-md border bg-muted/20 p-3 text-sm"
              onClick={() => setActiveCopyArea('url')}
            >
              <div className="break-all pr-8 leading-5 font-mono">{details.method} {details.url}</div>
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

            <div className="rounded-md border">
              <Tabs
                value={activePayloadTab}
                onValueChange={(value) => setActivePayloadTab(value as 'requestHeaders' | 'responseHeaders' | 'requestBody' | 'responseBody')}
                className="flex-col gap-0 border-0 p-0"
              >
                <TabsList className="h-auto flex-row flex-wrap items-center justify-start rounded-none border-b bg-transparent p-1">
                  <TabsTrigger value="requestHeaders">{t('Request Headers')}</TabsTrigger>
                  <TabsTrigger value="responseHeaders">{t('Response Headers')}</TabsTrigger>
                  <TabsTrigger value="requestBody">{t('Request Body')}</TabsTrigger>
                  <TabsTrigger value="responseBody">{t('Response Body')}</TabsTrigger>
                </TabsList>

                <TabsContent value="requestHeaders" className="m-0">
                  {renderPayloadContent(details.requestHeaders)}
                </TabsContent>
                <TabsContent value="responseHeaders" className="m-0">
                  {renderPayloadContent(details.responseHeaders)}
                </TabsContent>
                <TabsContent value="requestBody" className="m-0">
                  {renderPayloadContent(details.requestBody)}
                </TabsContent>
                <TabsContent value="responseBody" className="m-0">
                  {renderPayloadContent(details.responseBody)}
                </TabsContent>
              </Tabs>
            </div>

            <div className="flex flex-wrap justify-end gap-2">
              <Button
                size="sm"
                variant="outline"
                onClick={() =>
                  downloadByUrl(
                    `${getApiUrl()}/api/admin/request-trace/${details.id}/dump?token=${encodeURIComponent(getUserSession() || '')}`,
                  )
                }
              >
                <IconDownload size={14} className="mr-1" />
                {t('Download Dump')}
              </Button>
              {details.hasRequestBodyRaw && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() =>
                    downloadByUrl(
                      `${getApiUrl()}/api/admin/request-trace/${details.id}/raw?part=request&token=${encodeURIComponent(getUserSession() || '')}`,
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
                      `${getApiUrl()}/api/admin/request-trace/${details.id}/raw?part=response&token=${encodeURIComponent(getUserSession() || '')}`,
                    )
                  }
                >
                  <IconDownload size={14} className="mr-1" />
                  {t('Download Response Raw')}
                </Button>
              )}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
