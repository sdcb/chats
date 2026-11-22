import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  UpsertWebGatewayRequest,
  WebGateway,
  WebGatewayType,
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

import { EMPTY_VALUE, formatDateTime, nullIfBlank, showTestResult } from './utils';

import {
  deleteAdminWebGateway,
  getAdminWebGateways,
  saveAdminWebGateway,
  testAdminWebGateway,
} from '@/apis/adminWebsitesApi';

type GatewayForm = {
  name: string;
  adminEndpoint: string;
  credential: string;
  clearCredential: boolean;
  serverNames: string;
  publicIpv4: string;
  publicIpv6: string;
  cnameTarget: string;
  publicScheme: string;
  publicPort: string;
  isEnabled: boolean;
};

const emptyForm: GatewayForm = {
  name: '',
  adminEndpoint: 'http://127.0.0.1:2019',
  credential: '',
  clearCredential: false,
  serverNames: 'srv0',
  publicIpv4: '',
  publicIpv6: '',
  cnameTarget: '',
  publicScheme: 'https',
  publicPort: '443',
  isEnabled: true,
};

export default function GatewaysTab() {
  const { t } = useTranslation();
  const [rows, setRows] = useState<WebGateway[]>([]);
  const [loading, setLoading] = useState(false);
  const [dialog, setDialog] = useState<number | 'new' | null>(null);
  const [form, setForm] = useState<GatewayForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [testingId, setTestingId] = useState<number | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await getAdminWebGateways());
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh().catch(() => null);
  }, [refresh]);

  const edit = useCallback((row: WebGateway) => {
    setForm({
      name: row.name,
      adminEndpoint: row.adminEndpoint,
      credential: '',
      clearCredential: false,
      serverNames: row.serverNames,
      publicIpv4: row.publicIpv4 ?? '',
      publicIpv6: row.publicIpv6 ?? '',
      cnameTarget: row.cnameTarget ?? '',
      publicScheme: row.publicScheme,
      publicPort: String(row.publicPort),
      isEnabled: row.isEnabled,
    });
    setDialog(row.id);
  }, []);

  const toRequest = (f: GatewayForm): UpsertWebGatewayRequest => ({
    name: f.name.trim(),
    gatewayType: WebGatewayType.Caddy2,
    adminEndpoint: f.adminEndpoint.trim(),
    credential: nullIfBlank(f.credential),
    clearCredential: f.clearCredential,
    serverNames: f.serverNames.trim(),
    publicIpv4: nullIfBlank(f.publicIpv4),
    publicIpv6: nullIfBlank(f.publicIpv6),
    cnameTarget: nullIfBlank(f.cnameTarget),
    publicScheme: f.publicScheme,
    publicPort: Number(f.publicPort),
    isEnabled: f.isEnabled,
  });

  const save = async () => {
    if (dialog === null) return;
    setSaving(true);
    try {
      await saveAdminWebGateway(dialog === 'new' ? null : dialog, toRequest(form));
      toast.success(t('Save successful'));
      setDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };

  const test = useCallback(async (row: WebGateway) => {
    setTestingId(row.id);
    try {
      showTestResult(await testAdminWebGateway(row.id));
    } finally {
      setTestingId(null);
    }
  }, []);

  const remove = useCallback(
    async (row: WebGateway) => {
      await deleteAdminWebGateway(row.id);
      toast.success(t('Deleted successful'));
      await refresh();
    },
    [refresh, t],
  );

  const columns = useMemo<UnifiedTableColumn<WebGateway, string>[]>(
    () => [
      { key: 'name', title: t('Name'), cell: (row) => row.name },
      {
        key: 'endpoint',
        title: t('Admin endpoint'),
        className: 'max-w-64',
        cell: (row) => (
          <code className="break-all text-xs">{row.adminEndpoint}</code>
        ),
      },
      {
        key: 'servers',
        title: t('Servers'),
        cell: (row) => row.serverNames,
      },
      {
        key: 'target',
        title: t('DNS target'),
        cell: (row) =>
          [row.publicIpv4, row.publicIpv6, row.cnameTarget]
            .filter(Boolean)
            .join(', ') || EMPTY_VALUE,
      },
      {
        key: 'public',
        title: t('Public endpoint'),
        cell: (row) => `${row.publicScheme}:${row.publicPort}`,
      },
      {
        key: 'credential',
        title: t('Credential'),
        cell: (row) => (row.hasCredential ? t('Configured') : EMPTY_VALUE),
      },
      { key: 'domains', title: t('Domains'), cell: (row) => row.domainCount },
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
              onClick={() => edit(row)}
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
    [edit, remove, t, test, testingId],
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
                label={t('Add gateway')}
                icon={<IconPlus size={18} />}
                onClick={() => {
                  setForm(emptyForm);
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
        emptyText={t('No gateways found.')}
      />

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && setDialog(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new' ? t('Add gateway') : t('Edit gateway')}
            </DialogTitle>
            <DialogDescription>
              {t(
                'Caddy 2 gateway managed through its Admin API. Routes are added to the listed HTTP servers.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Label>
              {t('Name')}
              <Input
                autoComplete="off"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </Label>
            <Label>
              {t('Admin endpoint')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.adminEndpoint}
                placeholder="http://127.0.0.1:2019"
                onChange={(e) =>
                  setForm({ ...form, adminEndpoint: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Servers')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.serverNames}
                placeholder="srv0"
                onChange={(e) =>
                  setForm({ ...form, serverNames: e.target.value })
                }
              />
              <span className="text-xs text-muted-foreground">
                {t('Comma-separated Caddy HTTP server names, e.g. srv0.')}
              </span>
            </Label>
            <div className="grid grid-cols-2 gap-2">
              <Label>
                {t('Public scheme')}
                <select
                  className="h-10 w-full rounded-md border bg-background px-3"
                  value={form.publicScheme}
                  onChange={(e) =>
                    setForm({ ...form, publicScheme: e.target.value })
                  }
                >
                  <option value="https">https</option>
                  <option value="http">http</option>
                </select>
              </Label>
              <Label>
                {t('Public port')}
                <Input
                  type="number"
                  min={1}
                  max={65535}
                  value={form.publicPort}
                  onChange={(e) =>
                    setForm({ ...form, publicPort: e.target.value })
                  }
                />
              </Label>
            </div>
            <Label>
              {t('Public IPv4')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.publicIpv4}
                onChange={(e) =>
                  setForm({ ...form, publicIpv4: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Public IPv6')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.publicIpv6}
                onChange={(e) =>
                  setForm({ ...form, publicIpv6: e.target.value })
                }
              />
            </Label>
            <Label className="sm:col-span-2">
              {t('CNAME target')}
              <Input
                autoComplete="off"
                spellCheck={false}
                value={form.cnameTarget}
                placeholder="gateway.example.com"
                onChange={(e) =>
                  setForm({ ...form, cnameTarget: e.target.value })
                }
              />
              <span className="text-xs text-muted-foreground">
                {t(
                  'Address mode domains use the public IPs; CNAME mode domains use the CNAME target.',
                )}
              </span>
            </Label>
            <Label className="sm:col-span-2">
              {t('Credential')}
              <Input
                type="password"
                autoComplete="new-password"
                value={form.credential}
                placeholder='{"username":"","password":""}'
                onChange={(e) =>
                  setForm({ ...form, credential: e.target.value })
                }
              />
              <span className="text-xs text-muted-foreground">
                {t(
                  'Optional JSON: {"username","password"} or {"bearerToken"}. Leave blank to keep the current credential.',
                )}
              </span>
            </Label>
            {dialog !== 'new' && (
              <LabelSwitch
                checked={form.clearCredential}
                onCheckedChange={(checked) =>
                  setForm({ ...form, clearCredential: checked })
                }
                label={t('Clear credential')}
              />
            )}
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
            <Button disabled={saving} onClick={() => save().catch(() => null)}>
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
