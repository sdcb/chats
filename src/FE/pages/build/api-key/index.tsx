import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { IconPencil } from '@/components/Icons';

import { getApiUrl } from '@/utils/common';
import { formatDate } from '@/utils/date';

import { GetUserApiKeyResult } from '@/types/clientApis';

import DeletePopover from '@/components/Popover/DeletePopover';
import Tips from '@/components/Tips/Tips';
import ApiKeyDialog from './ApiKeyDialog';

import CopyButton from '@/components/Button/CopyButton';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import {
  deleteUserApiKey,
  getUserApiKey,
  postUserApiKey,
  putUserApiKey,
} from '@/apis/clientApis';

export default function BuildApiKeyPage() {
  const { t } = useTranslation();
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [apiKeys, setApiKeys] = useState<GetUserApiKeyResult[]>([]);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createExpires, setCreateExpires] = useState('');
  const [creating, setCreating] = useState(false);
  const [createdKey, setCreatedKey] = useState<string | null>(null);

  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<GetUserApiKeyResult | null>(null);
  const [editComment, setEditComment] = useState('');
  const [editExpires, setEditExpires] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);

  const getDefaultName = () => {
    const now = new Date();
    const yyyy = now.getFullYear();
    const mm = `${now.getMonth() + 1}`.padStart(2, '0');
    const dd = `${now.getDate()}`.padStart(2, '0');
    return `${yyyy}${mm}${dd}`;
  };

  const getDefaultExpires = () => {
    const date = new Date();
    date.setFullYear(date.getFullYear() + 1);
    return date.toISOString();
  };

  const initData = () => {
    getUserApiKey()
      .then((data) => {
        setApiKeys(data);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  useEffect(() => {
    initData();
  }, []);

  const openCreateDialog = () => {
    setCreateName(getDefaultName());
    setCreateExpires(getDefaultExpires());
    setCreatedKey(null);
    setCreateDialogOpen(true);
  };

  const submitCreate = () => {
    const name = createName.trim();
    if (!name) {
      toast.error(t('Please enter a name'));
      return;
    }

    setCreating(true);
    postUserApiKey({
      comment: name,
      expires: createExpires,
    })
      .then((result) => {
        setCreatedKey(result.key);
        initData();
      })
      .finally(() => {
        setCreating(false);
      });
  };

  const copyCreatedKey = async () => {
    if (!createdKey) return;
    await navigator.clipboard.writeText(createdKey);
    toast.success(t('Copied to clipboard'));
  };

  const openEditDialog = (apiKey: GetUserApiKeyResult) => {
    setEditTarget(apiKey);
    setEditComment(apiKey.comment || '');
    setEditExpires(apiKey.expires);
    setEditDialogOpen(true);
  };

  const submitEdit = () => {
    if (!editTarget) return;
    setSavingEdit(true);
    putUserApiKey(editTarget.id, {
      comment: editComment,
      expires: editExpires,
    })
      .then(() => {
        toast.success(t('Save successful'));
        setEditDialogOpen(false);
        setEditTarget(null);
        initData();
      })
      .finally(() => {
        setSavingEdit(false);
      });
  };

  const removeApiKey = (id: string) => {
    deleteUserApiKey(id).then(() => {
      setApiKeys((prev) =>
        prev.filter((x) => {
          return x.id !== id;
        }),
      );
    });
  };

  const viewApiKeyUsage = (id: string) => {
    router.push(`/build/usage?kid=${id}&page=1`);
  };

  const apiUrl = (getApiUrl() || location.origin) + '/v1';

  return (
    <div className="w-full">
      <div className="flex justify-between items-center gap-4 px-2 bg-card rounded-md mb-4">
        <div className="relative w-[70%]">
          <div className="p-3 text-xs sm:text-[12.5px]">
            <div className="flex items-center overflow-hidden text-ellipsis whitespace-nowrap">
              API URL:&nbsp;<span className="font-mono">{apiUrl}</span>
              <CopyButton value={apiUrl} />
            </div>
            <div className="text-muted-foreground mt-1">
              {t('API keys are credentials for accessing Chats API with full account permissions. Please keep them secure.')}
            </div>
          </div>
        </div>
        <div>
          <Button variant="default" onClick={openCreateDialog} size="sm">
            {t('New Key')}
          </Button>
        </div>
      </div>

      <div className="block sm:hidden">
        {loading ? (
          <div className="flex justify-center py-4">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-gray-900 dark:border-white"></div>
          </div>
        ) : apiKeys.length === 0 ? (
          <div className="text-center py-4 text-sm text-gray-500">
            {t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {apiKeys.map((x) => (
              <Card key={x.id} className="p-2 border-none shadow-sm">
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('Comment')}</div>
                  <button
                    type="button"
                    className="max-w-[180px] truncate cursor-pointer text-blue-600 hover:underline"
                    onClick={() => viewApiKeyUsage(x.id)}
                  >
                    {x.comment || '-'}
                  </button>
                </div>
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('Key')}</div>
                  <div className="w-3/4 truncate text-right font-mono">{x.key}</div>
                </div>
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('Expires')}</div>
                  <div className="h-9 flex items-center">{formatDate(x.expires)}</div>
                </div>
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('LastUsedAt')}</div>
                  <div className="h-9 flex items-center">
                    {x.lastUsedAt ? formatDate(x.lastUsedAt) : '-'}
                  </div>
                </div>
                <div className="flex justify-end items-center gap-2 mt-1">
                  <Tips
                    content={t('Edit')}
                    trigger={
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-9 w-9"
                        onClick={() => openEditDialog(x)}
                        aria-label={t('Edit') || 'Edit'}
                      >
                        <IconPencil size={18} />
                      </Button>
                    }
                  />
                  <DeletePopover
                    onDelete={() => {
                      removeApiKey(x.id);
                    }}
                  />
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>

      <div className="hidden sm:block">
        <Card className="overflow-x-auto w-full rounded-md border-none">
          <Table>
            <TableHeader>
              <TableRow className="pointer-events-none">
                <TableHead>{t('Comment')}</TableHead>
                <TableHead>{t('Key')}</TableHead>
                <TableHead>{t('Expires')}</TableHead>
                <TableHead>{t('LastUsedAt')}</TableHead>
                <TableHead>{t('Actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody isEmpty={apiKeys.length === 0} isLoading={loading}>
              {apiKeys.map((x) => {
                return (
                  <TableRow key={x.id} className="cursor-pointer">
                    <TableCell className="py-2 min-w-[128px] max-w-[200px]">
                      <button
                        type="button"
                        className="overflow-hidden text-ellipsis whitespace-nowrap text-blue-600 hover:underline"
                        onClick={() => viewApiKeyUsage(x.id)}
                      >
                        {x.comment || '-'}
                      </button>
                    </TableCell>
                    <TableCell className="py-2 min-w-[140px] max-w-[260px] font-mono overflow-hidden text-ellipsis whitespace-nowrap">
                      {x.key}
                    </TableCell>
                    <TableCell className="py-2">{formatDate(x.expires)}</TableCell>
                    <TableCell className="py-2 min-w-[128px] max-w-[150px]">
                      {x.lastUsedAt ? formatDate(x.lastUsedAt) : '-'}
                    </TableCell>
                    <TableCell className="py-2 max-w-[140px]">
                      <div className="flex items-center gap-2">
                        <Tips
                          content={t('Edit')}
                          trigger={
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-9 w-9"
                              onClick={() => openEditDialog(x)}
                              aria-label={t('Edit') || 'Edit'}
                            >
                              <IconPencil size={18} />
                            </Button>
                          }
                        />
                        <DeletePopover
                          onDelete={() => {
                            removeApiKey(x.id);
                          }}
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Card>
      </div>

      <ApiKeyDialog
        open={createDialogOpen}
        mode="create"
        title={t('New Key') || 'New Key'}
        name={createName}
        onNameChange={setCreateName}
        expires={createExpires}
        onExpiresChange={setCreateExpires}
        submitting={creating}
        submitText={creating ? t('Creating...') || 'Creating...' : t('Create') || 'Create'}
        onSubmit={submitCreate}
        onCancel={() => setCreateDialogOpen(false)}
        createdKey={createdKey}
        onCopyCreatedKey={copyCreatedKey}
        onCloseCreatedState={() => {
          setCreateDialogOpen(false);
          setCreatedKey(null);
        }}
        onOpenChange={(open) => {
          if (!creating) {
            setCreateDialogOpen(open);
            if (!open) {
              setCreatedKey(null);
            }
          }
        }}
      />

      <ApiKeyDialog
        open={editDialogOpen}
        mode="edit"
        title={t('Edit') || 'Edit'}
        name={editComment}
        onNameChange={setEditComment}
        expires={editExpires}
        onExpiresChange={setEditExpires}
        submitting={savingEdit}
        submitText={savingEdit ? t('Saving...') || 'Saving...' : t('Save') || 'Save'}
        onSubmit={submitEdit}
        onCancel={() => {
          setEditDialogOpen(false);
          setEditTarget(null);
        }}
        onOpenChange={(open) => {
          if (!savingEdit) {
            setEditDialogOpen(open);
            if (!open) {
              setEditTarget(null);
            }
          }
        }}
      />
    </div>
  );
}
