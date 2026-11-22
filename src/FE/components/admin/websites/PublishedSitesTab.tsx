import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  PublishedSiteAdmin,
  PublishedSiteStatus,
  WebsiteEvent,
} from '@/types/websites';

import { IconRefresh } from '@/components/Icons';
import {
  UnifiedTable,
  UnifiedTableColumn,
} from '@/components/table/UnifiedTable';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { LabelSwitch } from '@/components/ui/label-switch';
import WebsiteStatusBadge from '@/components/websites/WebsiteStatusBadge';

import { EMPTY_VALUE, formatDateTime } from './utils';

import {
  getAdminPublishedSiteEvents,
  getAdminPublishedSites,
  reconcileAdminWebsites,
  unpublishAdminPublishedSite,
} from '@/apis/adminWebsitesApi';

export default function PublishedSitesTab() {
  const { t } = useTranslation();
  const [rows, setRows] = useState<PublishedSiteAdmin[]>([]);
  const [includeUnpublished, setIncludeUnpublished] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [reconciling, setReconciling] = useState(false);
  const [events, setEvents] = useState<{
    site: PublishedSiteAdmin;
    items: WebsiteEvent[];
  } | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await getAdminPublishedSites(includeUnpublished));
    } finally {
      setLoading(false);
    }
  }, [includeUnpublished]);

  useEffect(() => {
    refresh().catch(() => null);
  }, [refresh]);

  const unpublish = useCallback(
    async (row: PublishedSiteAdmin, force: boolean) => {
      const message = force
        ? t(
            'Force unpublish marks the website as unpublished even if gateway or DNS cleanup fails. Continue?',
          )
        : t('Unpublish this website?');
      if (!window.confirm(message)) return;
      setBusyId(row.id);
      try {
        await unpublishAdminPublishedSite(row.id, force);
        toast.success(t('Website unpublished'));
      } finally {
        setBusyId(null);
        await refresh().catch(() => null);
      }
    },
    [refresh, t],
  );

  const showEvents = useCallback(async (row: PublishedSiteAdmin) => {
    setEvents({ site: row, items: await getAdminPublishedSiteEvents(row.id) });
  }, []);

  const reconcile = async () => {
    setReconciling(true);
    try {
      const result = await reconcileAdminWebsites();
      const summary = `${t('Routes applied')}: ${result.routesApplied}, ${t(
        'Orphan routes removed',
      )}: ${result.orphanRoutesRemoved}, ${t('Websites unpublished')}: ${
        result.sitesUnpublished
      }`;
      if (result.errors.length > 0)
        toast.error(`${summary}\n${result.errors.join('\n')}`, {
          duration: 10000,
        });
      else toast.success(summary, { duration: 6000 });
      await refresh();
    } finally {
      setReconciling(false);
    }
  };

  const columns = useMemo<UnifiedTableColumn<PublishedSiteAdmin, string>[]>(
    () => [
      { key: 'id', title: t('ID'), cell: (row) => row.id },
      {
        key: 'url',
        title: t('URL'),
        className: 'max-w-72',
        cell: (row) => (
          <a
            href={row.url}
            target="_blank"
            rel="noreferrer"
            className="text-primary underline break-all"
          >
            {row.url}
          </a>
        ),
      },
      { key: 'user', title: t('User'), cell: (row) => row.userName },
      {
        key: 'container',
        title: t('Container'),
        cell: (row) => (
          <span className={row.containerDeleted ? 'line-through' : ''}>
            {row.containerName}:{row.containerPort}
          </span>
        ),
      },
      {
        key: 'upstream',
        title: t('Upstream'),
        cell: (row) => (
          <code className="text-xs">{row.upstreamDial || EMPTY_VALUE}</code>
        ),
      },
      {
        key: 'status',
        title: t('Status'),
        className: 'max-w-72',
        cell: (row) => (
          <div className="space-y-1">
            <WebsiteStatusBadge status={row.status} />
            {row.lastError && (
              <div className="text-xs text-red-600 break-all">
                {row.lastError}
              </div>
            )}
          </div>
        ),
      },
      {
        key: 'createdAt',
        title: t('Created'),
        cell: (row) => formatDateTime(row.createdAt),
      },
      {
        key: 'actions',
        title: t('Actions'),
        cell: (row) => (
          <div className="flex items-center gap-1">
            <Button
              size="sm"
              variant="outline"
              onClick={() => showEvents(row).catch(() => null)}
            >
              {t('Events')}
            </Button>
            {row.status !== PublishedSiteStatus.Unpublished && (
              <>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busyId === row.id}
                  onClick={() => unpublish(row, false).catch(() => null)}
                >
                  {t('Unpublish')}
                </Button>
                <Button
                  size="sm"
                  variant="destructive"
                  disabled={busyId === row.id}
                  onClick={() => unpublish(row, true).catch(() => null)}
                >
                  {t('Force unpublish')}
                </Button>
              </>
            )}
          </div>
        ),
      },
    ],
    [busyId, showEvents, t, unpublish],
  );

  return (
    <>
      <UnifiedTable
        filters={
          <>
            <LabelSwitch
              checked={includeUnpublished}
              onCheckedChange={setIncludeUnpublished}
              label={t('Show unpublished')}
            />
            <Button
              type="button"
              variant="outline"
              size="icon"
              disabled={loading}
              aria-label={t('Refresh')}
              title={t('Refresh')}
              onClick={() => refresh().catch(() => null)}
            >
              <IconRefresh size={18} />
            </Button>
          </>
        }
        actions={[
          {
            key: 'reconcile',
            element: (
              <Button
                variant="outline"
                disabled={reconciling}
                onClick={() => reconcile().catch(() => null)}
              >
                {t('Reconcile now')}
              </Button>
            ),
          },
        ]}
        columns={columns}
        rows={rows}
        loading={loading}
        page={1}
        totalCount={rows.length}
        rowKey={(row) => row.id}
        onPageChange={() => undefined}
        pagination={false}
        emptyText={t('No websites found.')}
      />

      <Dialog
        open={events !== null}
        onOpenChange={(open) => !open && setEvents(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {t('Events')} - {events?.site.hostname}
            </DialogTitle>
          </DialogHeader>
          <div className="max-h-[60vh] space-y-2 overflow-auto text-sm">
            {events?.items.length === 0 && (
              <div className="text-muted-foreground">{t('No events.')}</div>
            )}
            {events?.items.map((e) => (
              <div key={e.id} className="border-b pb-2">
                <div className="flex justify-between gap-2">
                  <span
                    className={
                      e.isSuccess
                        ? 'font-medium text-green-600 dark:text-green-400'
                        : 'font-medium text-red-600 dark:text-red-400'
                    }
                  >
                    {e.action}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {formatDateTime(e.createdAt)}
                  </span>
                </div>
                {e.message && (
                  <div className="break-all text-xs text-muted-foreground">
                    {e.message}
                  </div>
                )}
              </div>
            ))}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
