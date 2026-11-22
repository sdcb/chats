import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { DnsProvider, DnsProviderType } from '@/types/websites';

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

import { EMPTY_VALUE, formatDateTime, showTestResult } from './utils';

import {
  deleteAdminDnsProvider,
  getAdminDnsProviders,
  saveAdminDnsProvider,
  testAdminDnsProvider,
} from '@/apis/adminWebsitesApi';

type ProviderForm = {
  name: string;
  providerType: DnsProviderType;
  apiToken: string;
  accessKeyId: string;
  accessKeySecret: string;
  isEnabled: boolean;
};

const emptyForm: ProviderForm = {
  name: '',
  providerType: DnsProviderType.Cloudflare,
  apiToken: '',
  accessKeyId: '',
  accessKeySecret: '',
  isEnabled: true,
};

const buildCredential = (form: ProviderForm): string | null => {
  if (form.providerType === DnsProviderType.Cloudflare) {
    return form.apiToken.trim()
      ? JSON.stringify({ apiToken: form.apiToken.trim() })
      : null;
  }
  if (!form.accessKeyId.trim() && !form.accessKeySecret.trim()) return null;
  return JSON.stringify({
    accessKeyId: form.accessKeyId.trim(),
    accessKeySecret: form.accessKeySecret.trim(),
  });
};

export default function DnsProvidersTab() {
  const { t } = useTranslation();
  const [rows, setRows] = useState<DnsProvider[]>([]);
  const [loading, setLoading] = useState(false);
  const [dialog, setDialog] = useState<number | 'new' | null>(null);
  const [form, setForm] = useState<ProviderForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [testingId, setTestingId] = useState<number | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await getAdminDnsProviders());
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh().catch(() => null);
  }, [refresh]);

  const providerName = useCallback(
    (type: DnsProviderType) =>
      type === DnsProviderType.Cloudflare
        ? t('Cloudflare')
        : type === DnsProviderType.AliyunDns
        ? t('Aliyun DNS')
        : String(type),
    [t],
  );

  const save = async () => {
    if (dialog === null) return;
    const credential = buildCredential(form);
    if (dialog === 'new' && !credential) {
      toast.error(t('Credential is required'));
      return;
    }
    setSaving(true);
    try {
      await saveAdminDnsProvider(dialog === 'new' ? null : dialog, {
        name: form.name.trim(),
        providerType: form.providerType,
        credential,
        isEnabled: form.isEnabled,
      });
      toast.success(t('Save successful'));
      setDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };

  const test = useCallback(async (row: DnsProvider) => {
    setTestingId(row.id);
    try {
      showTestResult(await testAdminDnsProvider(row.id));
    } finally {
      setTestingId(null);
    }
  }, []);

  const remove = useCallback(
    async (row: DnsProvider) => {
      await deleteAdminDnsProvider(row.id);
      toast.success(t('Deleted successful'));
      await refresh();
    },
    [refresh, t],
  );

  const columns = useMemo<UnifiedTableColumn<DnsProvider, string>[]>(
    () => [
      { key: 'name', title: t('Name'), cell: (row) => row.name },
      {
        key: 'type',
        title: t('Type'),
        cell: (row) => providerName(row.providerType),
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
              onClick={() => {
                setForm({
                  ...emptyForm,
                  name: row.name,
                  providerType: row.providerType,
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
    [providerName, remove, t, test, testingId],
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
                label={t('Add DNS provider')}
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
        emptyText={t('No DNS providers found.')}
      />

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && setDialog(null)}
      >
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new' ? t('Add DNS provider') : t('Edit DNS provider')}
            </DialogTitle>
            <DialogDescription>
              {t(
                'Credentials are stored encrypted and are never shown again. Leave them blank to keep the current credential.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <Label>
              {t('Name')}
              <Input
                autoComplete="off"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </Label>
            <Label>
              {t('Type')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.providerType}
                onChange={(e) =>
                  setForm({
                    ...form,
                    providerType: Number(e.target.value) as DnsProviderType,
                  })
                }
              >
                <option value={DnsProviderType.Cloudflare}>
                  {t('Cloudflare')}
                </option>
                <option value={DnsProviderType.AliyunDns}>
                  {t('Aliyun DNS')}
                </option>
              </select>
            </Label>
            {form.providerType === DnsProviderType.Cloudflare ? (
              <Label>
                {t('API token')}
                <Input
                  type="password"
                  autoComplete="new-password"
                  value={form.apiToken}
                  onChange={(e) =>
                    setForm({ ...form, apiToken: e.target.value })
                  }
                />
                <span className="text-xs text-muted-foreground">
                  {t('Requires Zone:Read and DNS:Edit permissions.')}
                </span>
              </Label>
            ) : (
              <>
                <Label>
                  {t('AccessKey ID')}
                  <Input
                    type="password"
                    autoComplete="new-password"
                    value={form.accessKeyId}
                    onChange={(e) =>
                      setForm({ ...form, accessKeyId: e.target.value })
                    }
                  />
                </Label>
                <Label>
                  {t('AccessKey secret')}
                  <Input
                    type="password"
                    autoComplete="new-password"
                    value={form.accessKeySecret}
                    onChange={(e) =>
                      setForm({ ...form, accessKeySecret: e.target.value })
                    }
                  />
                </Label>
              </>
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
