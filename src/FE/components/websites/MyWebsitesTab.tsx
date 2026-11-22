import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  AvailableDomain,
  MyWebsite,
  PublishedSiteStatus,
  WebsiteContainer,
} from '@/types/websites';

import { IconPlus, IconRefresh } from '@/components/Icons';
import {
  UnifiedTable,
  UnifiedTableColumn,
} from '@/components/table/UnifiedTable';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { LabelSwitch } from '@/components/ui/label-switch';

import WebsiteStatusBadge from './WebsiteStatusBadge';

import {
  getMyWebsites,
  getWebsiteContainers,
  getWebsiteDomains,
  publishWebsite,
  unpublishWebsite,
} from '@/apis/websitesApi';

const SUBDOMAIN_PATTERN = /^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$/;

type PublishForm = {
  containerId: string;
  containerPort: string;
  hostedDomainId: string;
  subdomain: string;
};

const emptyForm: PublishForm = {
  containerId: '',
  containerPort: '80',
  hostedDomainId: '',
  subdomain: '',
};

const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString() : '-';

export const buildPreviewUrl = (domain: AvailableDomain, subdomain: string) => {
  const isDefaultPort =
    (domain.publicScheme === 'https' && domain.publicPort === 443) ||
    (domain.publicScheme === 'http' && domain.publicPort === 80);
  return `${domain.publicScheme}://${subdomain}.${domain.domain}${
    isDefaultPort ? '' : `:${domain.publicPort}`
  }`;
};

export default function MyWebsitesTab() {
  const { t } = useTranslation();
  const [sites, setSites] = useState<MyWebsite[]>([]);
  const [domains, setDomains] = useState<AvailableDomain[]>([]);
  const [containers, setContainers] = useState<WebsiteContainer[]>([]);
  const [includeUnpublished, setIncludeUnpublished] = useState(false);
  const [loading, setLoading] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<PublishForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const [s, d] = await Promise.all([
        getMyWebsites(includeUnpublished),
        getWebsiteDomains(),
      ]);
      setSites(s);
      setDomains(d);
    } finally {
      setLoading(false);
    }
  }, [includeUnpublished]);

  useEffect(() => {
    refresh().catch(() => null);
  }, [refresh]);

  const openPublish = async () => {
    const list = await getWebsiteContainers();
    setContainers(list);
    setForm({
      ...emptyForm,
      containerId: list.find((c) => c.isRunning)?.id ?? '',
      hostedDomainId: domains[0] ? String(domains[0].id) : '',
    });
    setDialogOpen(true);
  };

  const selectedDomain = domains.find(
    (d) => String(d.id) === form.hostedDomainId,
  );
  const subdomainValid = SUBDOMAIN_PATTERN.test(form.subdomain);
  const port = Number(form.containerPort);
  const portValid = Number.isInteger(port) && port >= 1 && port <= 65535;
  const canPublish =
    !!form.containerId && !!selectedDomain && subdomainValid && portValid;

  const submit = async () => {
    if (!canPublish) return;
    setSaving(true);
    try {
      await publishWebsite({
        containerId: form.containerId,
        containerPort: port,
        hostedDomainId: Number(form.hostedDomainId),
        subdomain: form.subdomain,
      });
      toast.success(t('Website published'));
      setDialogOpen(false);
    } finally {
      setSaving(false);
      await refresh().catch(() => null);
    }
  };

  const unpublish = useCallback(
    async (site: MyWebsite) => {
      if (!window.confirm(t('Unpublish this website?'))) return;
      setBusyId(site.id);
      try {
        await unpublishWebsite(site.id);
        toast.success(t('Website unpublished'));
      } finally {
        setBusyId(null);
        await refresh().catch(() => null);
      }
    },
    [refresh, t],
  );

  const columns = useMemo<UnifiedTableColumn<MyWebsite, string>[]>(
    () => [
      {
        key: 'url',
        title: t('URL'),
        cell: (row) =>
          row.status === PublishedSiteStatus.Active ? (
            <a
              href={row.url}
              target="_blank"
              rel="noreferrer"
              className="text-primary underline break-all"
            >
              {row.url}
            </a>
          ) : (
            <span className="break-all">{row.url}</span>
          ),
      },
      {
        key: 'container',
        title: t('Container'),
        cell: (row) => `${row.containerName}:${row.containerPort}`,
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
        cell: (row) =>
          row.status !== PublishedSiteStatus.Unpublished ? (
            <Button
              size="sm"
              variant="outline"
              disabled={busyId === row.id}
              onClick={() => unpublish(row).catch(() => null)}
            >
              {t('Unpublish')}
            </Button>
          ) : (
            formatDateTime(row.unpublishedAt)
          ),
      },
    ],
    [busyId, t, unpublish],
  );

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        {t(
          'Publish a port of your permanent container as a public website. The website stays online while the container is running.',
        )}
      </p>
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
            key: 'publish',
            element: (
              <Button
                disabled={domains.length === 0}
                onClick={() => openPublish().catch(() => null)}
                className="gap-1"
              >
                <IconPlus size={16} />
                {t('Publish website')}
              </Button>
            ),
          },
        ]}
        columns={columns}
        rows={sites}
        loading={loading}
        page={1}
        totalCount={sites.length}
        rowKey={(row) => row.id}
        onPageChange={() => undefined}
        pagination={false}
        emptyText={
          domains.length === 0
            ? t('No domains are available for publishing websites.')
            : t('No websites found.')
        }
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('Publish website')}</DialogTitle>
            <DialogDescription>
              {t(
                'Only running permanent containers can be published. The container may be recreated to expose the port.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <Label>
              {t('Container')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.containerId}
                onChange={(e) =>
                  setForm({ ...form, containerId: e.target.value })
                }
              >
                {containers.length === 0 && (
                  <option value="">{t('No permanent containers')}</option>
                )}
                {containers.map((c) => (
                  <option key={c.id} value={c.id} disabled={!c.isRunning}>
                    {c.name} ({c.image})
                    {c.isRunning ? '' : ` - ${t('Stopped')}`}
                  </option>
                ))}
              </select>
            </Label>
            <Label>
              {t('Container port')}
              <Input
                type="number"
                min={1}
                max={65535}
                value={form.containerPort}
                onChange={(e) =>
                  setForm({ ...form, containerPort: e.target.value })
                }
              />
            </Label>
            <div className="grid grid-cols-2 gap-2">
              <Label>
                {t('Subdomain')}
                <Input
                  autoComplete="off"
                  spellCheck={false}
                  value={form.subdomain}
                  placeholder="my-site"
                  onChange={(e) =>
                    setForm({
                      ...form,
                      subdomain: e.target.value.trim().toLowerCase(),
                    })
                  }
                />
              </Label>
              <Label>
                {t('Domain')}
                <select
                  className="h-10 w-full rounded-md border bg-background px-3"
                  value={form.hostedDomainId}
                  onChange={(e) =>
                    setForm({ ...form, hostedDomainId: e.target.value })
                  }
                >
                  {domains.map((d) => (
                    <option key={d.id} value={d.id}>
                      .{d.domain}
                    </option>
                  ))}
                </select>
              </Label>
            </div>
            {form.subdomain && !subdomainValid && (
              <span className="text-xs text-red-600">
                {t(
                  'Use lowercase letters, digits and hyphens; must not start or end with a hyphen.',
                )}
              </span>
            )}
            {selectedDomain && subdomainValid && (
              <span className="text-xs text-muted-foreground break-all">
                {buildPreviewUrl(selectedDomain, form.subdomain)}
              </span>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={!canPublish || saving}
              onClick={() => submit().catch(() => null)}
            >
              {saving ? t('Publishing...') : t('Publish')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
