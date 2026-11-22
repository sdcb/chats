import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  DnsProvider,
  DnsRecordMode,
  HostedDomain,
  UpsertHostedDomainRequest,
  WebGateway,
} from '@/types/websites';

import { IconEdit, IconPlus, IconRefresh } from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import IconActionButton from '@/components/common/IconActionButton';
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

import { formatDateTime, showTestResult } from './utils';

import {
  deleteAdminHostedDomain,
  getAdminDnsProviders,
  getAdminHostedDomains,
  getAdminWebGateways,
  saveAdminHostedDomain,
  testAdminHostedDomain,
} from '@/apis/adminWebsitesApi';

type DomainForm = {
  domain: string;
  dnsZone: string;
  dnsProviderId: string;
  webGatewayId: string;
  recordMode: DnsRecordMode;
  recordTtl: string;
  isProxied: boolean;
  allowAllUsers: boolean;
  isEnabled: boolean;
};

const emptyForm: DomainForm = {
  domain: '',
  dnsZone: '',
  dnsProviderId: '',
  webGatewayId: '',
  recordMode: DnsRecordMode.Address,
  recordTtl: '600',
  isProxied: false,
  allowAllUsers: false,
  isEnabled: true,
};

export default function DomainsTab() {
  const { t } = useTranslation();
  const [rows, setRows] = useState<HostedDomain[]>([]);
  const [gateways, setGateways] = useState<WebGateway[]>([]);
  const [providers, setProviders] = useState<DnsProvider[]>([]);
  const [loading, setLoading] = useState(false);
  const [dialog, setDialog] = useState<number | 'new' | null>(null);
  const [form, setForm] = useState<DomainForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [testingId, setTestingId] = useState<number | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const [d, g, p] = await Promise.all([
        getAdminHostedDomains(),
        getAdminWebGateways(),
        getAdminDnsProviders(),
      ]);
      setRows(d);
      setGateways(g);
      setProviders(p);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh().catch(() => null);
  }, [refresh]);

  const toRequest = (f: DomainForm): UpsertHostedDomainRequest => ({
    domain: f.domain.trim().toLowerCase(),
    dnsZone: (f.dnsZone.trim() || f.domain.trim()).toLowerCase(),
    dnsProviderId: Number(f.dnsProviderId),
    webGatewayId: Number(f.webGatewayId),
    recordMode: f.recordMode,
    recordTtl: Number(f.recordTtl),
    isProxied: f.isProxied,
    allowAllUsers: f.allowAllUsers,
    isEnabled: f.isEnabled,
  });

  const save = async () => {
    if (dialog === null) return;
    setSaving(true);
    try {
      await saveAdminHostedDomain(
        dialog === 'new' ? null : dialog,
        toRequest(form),
      );
      toast.success(t('Save successful'));
      setDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };

  const test = useCallback(async (row: HostedDomain) => {
    setTestingId(row.id);
    try {
      showTestResult(await testAdminHostedDomain(row.id));
    } finally {
      setTestingId(null);
    }
  }, []);

  const remove = useCallback(
    async (row: HostedDomain) => {
      await deleteAdminHostedDomain(row.id);
      toast.success(t('Deleted successful'));
      await refresh();
    },
    [refresh, t],
  );

  const columns = useMemo<UnifiedTableColumn<HostedDomain, string>[]>(
    () => [
      { key: 'domain', title: t('Domain'), cell: (row) => row.domain },
      { key: 'zone', title: t('DNS zone'), cell: (row) => row.dnsZone },
      {
        key: 'provider',
        title: t('DNS provider'),
        cell: (row) => row.dnsProviderName,
      },
      {
        key: 'gateway',
        title: t('Gateway'),
        cell: (row) => row.webGatewayName,
      },
      {
        key: 'mode',
        title: t('Record mode'),
        cell: (row) =>
          `${row.recordMode === DnsRecordMode.Cname ? 'CNAME' : 'A/AAAA'}${
            row.isProxied ? ` (${t('Proxied')})` : ''
          }`,
      },
      {
        key: 'access',
        title: t('Access'),
        cell: (row) =>
          row.allowAllUsers ? t('All users') : t('Administrators only'),
      },
      {
        key: 'sites',
        title: t('Websites'),
        cell: (row) => row.liveSiteCount,
      },
      {
        key: 'status',
        title: t('Status'),
        cell: (row) =>
          row.isEnabled ? (
            <span className="text-green-600 dark:text-green-400">
              {t('Enabled')}
            </span>
          ) : (
            <span className="text-muted-foreground">{t('Disabled')}</span>
          ),
      },
      {
        key: 'updatedAt',
        title: t('Updated'),
        cell: (row) => formatDateTime(row.updatedAt),
      },
      {
        key: 'actions',
        title: t('Actions'),
        cell: (row) => (
          <div className="flex items-center gap-1">
            <Button
              size="sm"
              variant="outline"
              disabled={testingId === row.id}
              onClick={() => test(row).catch(() => null)}
            >
              {t('Test')}
            </Button>
            <IconActionButton
              label={t('Edit')}
              icon={<IconEdit size={15} />}
              className="h-8 w-8"
              onClick={() => {
                setForm({
                  domain: row.domain,
                  dnsZone: row.dnsZone,
                  dnsProviderId: String(row.dnsProviderId),
                  webGatewayId: String(row.webGatewayId),
                  recordMode: row.recordMode,
                  recordTtl: String(row.recordTtl),
                  isProxied: row.isProxied,
                  allowAllUsers: row.allowAllUsers,
                  isEnabled: row.isEnabled,
                });
                setDialog(row.id);
              }}
            />
            <DeletePopover
              onDelete={() => remove(row).catch(() => null)}
              tooltip={t('Delete')}
              className="h-8 w-8"
              iconSize={15}
            />
          </div>
        ),
      },
    ],
    [remove, t, test, testingId],
  );

  return (
    <>
      <UnifiedTable
        filters={
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
        }
        actions={[
          {
            key: 'add',
            element: (
              <IconActionButton
                label={t('Add domain')}
                icon={<IconPlus size={18} />}
                onClick={() => {
                  setForm({
                    ...emptyForm,
                    dnsProviderId: providers[0] ? String(providers[0].id) : '',
                    webGatewayId: gateways[0] ? String(gateways[0].id) : '',
                  });
                  setDialog('new');
                }}
              />
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
        emptyText={t('No domains found.')}
      />

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && setDialog(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new' ? t('Add domain') : t('Edit domain')}
            </DialogTitle>
            <DialogDescription>
              {t(
                'Websites are published as <subdomain>.<domain>. The domain must belong to the DNS zone managed by the provider.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Label>
              {t('Domain')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.domain}
                placeholder="apps.example.com"
                onChange={(e) => setForm({ ...form, domain: e.target.value })}
              />
            </Label>
            <Label>
              {t('DNS zone')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.dnsZone}
                placeholder="example.com"
                onChange={(e) => setForm({ ...form, dnsZone: e.target.value })}
              />
            </Label>
            <Label>
              {t('DNS provider')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.dnsProviderId}
                onChange={(e) =>
                  setForm({ ...form, dnsProviderId: e.target.value })
                }
              >
                {providers.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </Label>
            <Label>
              {t('Gateway')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.webGatewayId}
                onChange={(e) =>
                  setForm({ ...form, webGatewayId: e.target.value })
                }
              >
                {gateways.map((g) => (
                  <option key={g.id} value={g.id}>
                    {g.name}
                  </option>
                ))}
              </select>
            </Label>
            <Label>
              {t('Record mode')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.recordMode}
                onChange={(e) =>
                  setForm({
                    ...form,
                    recordMode: Number(e.target.value) as DnsRecordMode,
                  })
                }
              >
                <option value={DnsRecordMode.Address}>A/AAAA</option>
                <option value={DnsRecordMode.Cname}>CNAME</option>
              </select>
            </Label>
            <Label>
              {t('Record TTL')}
              <Input
                type="number"
                min={1}
                value={form.recordTtl}
                onChange={(e) =>
                  setForm({ ...form, recordTtl: e.target.value })
                }
              />
            </Label>
            <LabelSwitch
              checked={form.isProxied}
              onCheckedChange={(checked) =>
                setForm({ ...form, isProxied: checked })
              }
              label={t('Proxied (Cloudflare only)')}
            />
            <LabelSwitch
              checked={form.allowAllUsers}
              onCheckedChange={(checked) =>
                setForm({ ...form, allowAllUsers: checked })
              }
              label={t('Allow all users')}
            />
            <LabelSwitch
              checked={form.isEnabled}
              onCheckedChange={(checked) =>
                setForm({ ...form, isEnabled: checked })
              }
              label={t('Enabled')}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving || !form.dnsProviderId || !form.webGatewayId}
              onClick={() => save().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
