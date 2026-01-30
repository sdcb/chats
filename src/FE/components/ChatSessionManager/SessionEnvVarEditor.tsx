import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import {
  IconCheck,
  IconLoader,
  IconPlus,
  IconTrash,
} from '@/components/Icons';

import {
  getDockerEnvironmentVariables,
  saveDockerUserEnvironmentVariables,
} from '@/apis/dockerSessionsApi';
import { EnvironmentVariable } from '@/types/dockerSessions';

type Props = {
  chatId: string;
  encryptedSessionId: string;
};

type EditableEnvVar = EnvironmentVariable & { id: string };

let idCounter = 0;
function generateId() {
  return `env-${Date.now()}-${idCounter++}`;
}

export default function SessionEnvVarEditor({
  chatId,
  encryptedSessionId,
}: Props) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [systemVars, setSystemVars] = useState<EnvironmentVariable[]>([]);
  const [userVars, setUserVars] = useState<EditableEnvVar[]>([]);
  const [originalUserVars, setOriginalUserVars] = useState<EnvironmentVariable[]>([]);

  const loadEnvVars = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getDockerEnvironmentVariables(chatId, encryptedSessionId);
      setSystemVars(result.systemVariables);
      const editableUserVars = result.userVariables.map((v) => ({
        ...v,
        id: generateId(),
      }));
      setUserVars(editableUserVars);
      setOriginalUserVars(result.userVariables);
    } catch (e: any) {
      toast.error(e?.message || t('Failed to load environment variables'));
    } finally {
      setLoading(false);
    }
  }, [chatId, encryptedSessionId, t]);

  useEffect(() => {
    loadEnvVars();
  }, [loadEnvVars]);

  const hasChanges = useMemo(() => {
    if (userVars.length !== originalUserVars.length) return true;
    const currentSorted = [...userVars]
      .map((v) => `${v.key}=${v.value}`)
      .sort();
    const originalSorted = [...originalUserVars]
      .map((v) => `${v.key}=${v.value}`)
      .sort();
    return currentSorted.join('\n') !== originalSorted.join('\n');
  }, [userVars, originalUserVars]);

  const handleAddUserVar = useCallback(() => {
    setUserVars((prev) => [...prev, { id: generateId(), key: '', value: '' }]);
  }, []);

  const handleUpdateUserVar = useCallback(
    (id: string, field: 'key' | 'value', newValue: string) => {
      setUserVars((prev) =>
        prev.map((v) => (v.id === id ? { ...v, [field]: newValue } : v)),
      );
    },
    [],
  );

  const handleDeleteUserVar = useCallback((id: string) => {
    setUserVars((prev) => prev.filter((v) => v.id !== id));
  }, []);

  const handleSave = useCallback(async () => {
    // Validate
    const validVars = userVars.filter((v) => v.key.trim() !== '');
    const keys = validVars.map((v) => v.key.trim());
    const uniqueKeys = new Set(keys);
    if (uniqueKeys.size !== keys.length) {
      toast.error(t('Duplicate environment variable keys are not allowed'));
      return;
    }

    setSaving(true);
    try {
      const normalizedVars = validVars.map((v) => ({ key: v.key.trim(), value: v.value }));
      await saveDockerUserEnvironmentVariables(chatId, encryptedSessionId, {
        variables: normalizedVars,
      });
      // Optimistic update: update originalUserVars to match current state
      // This avoids UI jumping from reloading
      setOriginalUserVars(normalizedVars);
      // Also update userVars to remove empty keys and normalize
      setUserVars(validVars.map((v) => ({ ...v, key: v.key.trim() })));
    } catch (e: any) {
      toast.error(e?.message || t('Failed to save environment variables'));
    } finally {
      setSaving(false);
    }
  }, [chatId, encryptedSessionId, t, userVars]);

  if (loading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-6 w-32" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* User variables - 放在上面 */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h4 className="text-xs font-medium text-muted-foreground/70 uppercase tracking-wide">
              {t('User Variables')}
            </h4>
            {hasChanges && (
              <span className="text-xs text-orange-500">
                ({t('Modified')})
              </span>
            )}
          </div>
          <div className="flex items-center gap-1">
            {hasChanges && (
              <Button
                size="sm"
                variant="ghost"
                onClick={handleSave}
                disabled={saving}
                className="h-6 w-6 p-0"
                title={t('Save')}
              >
                {saving ? (
                  <IconLoader size={14} className="animate-spin" />
                ) : (
                  <IconCheck size={14} />
                )}
              </Button>
            )}
            <Button
              size="sm"
              variant="ghost"
              onClick={handleAddUserVar}
              className="h-6 w-6 p-0"
              title={t('Add environment variable')}
            >
              <IconPlus size={14} />
            </Button>
          </div>
        </div>
        {userVars.length === 0 ? (
          <div className="text-xs text-muted-foreground/70 py-2">
            {t('No user environment variables. Click + to add one.')}
          </div>
        ) : (
          <div className="rounded-md border overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-muted/50 border-b">
                  <th className="text-left px-3 py-2 font-medium w-1/3">
                    {t('Key')}
                  </th>
                  <th className="text-left px-3 py-2 font-medium">
                    {t('Value')}
                  </th>
                  <th className="w-10"></th>
                </tr>
              </thead>
              <tbody>
                {userVars.map((v, idx) => (
                  <tr
                    key={v.id}
                    className={cn(
                      'border-b last:border-b-0',
                      idx % 2 === 0 ? 'bg-background' : 'bg-muted/20',
                    )}
                  >
                    <td className="px-2 py-1.5">
                      <Input
                        value={v.key}
                        onChange={(e) =>
                          handleUpdateUserVar(v.id, 'key', e.target.value)
                        }
                        placeholder="KEY"
                        className="h-8 font-mono text-xs"
                      />
                    </td>
                    <td className="px-2 py-1.5">
                      <Input
                        value={v.value}
                        onChange={(e) =>
                          handleUpdateUserVar(v.id, 'value', e.target.value)
                        }
                        placeholder="value"
                        className="h-8 font-mono text-xs"
                      />
                    </td>
                    <td className="px-2 py-1.5">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => handleDeleteUserVar(v.id)}
                        className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
                        title={t('Delete')}
                      >
                        <IconTrash size={14} />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* System variables - 放在下面 */}
      {systemVars.length > 0 && (
        <div className="space-y-2">
          <h4 className="text-xs font-medium text-muted-foreground/70 uppercase tracking-wide">
            {t('System Variables')}
          </h4>
          <div className="rounded-md border overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-muted/50 border-b">
                  <th className="text-left px-3 py-2 font-medium w-1/3">
                    {t('Key')}
                  </th>
                  <th className="text-left px-3 py-2 font-medium">
                    {t('Value')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {systemVars.map((v, idx) => (
                  <tr
                    key={`sys-${idx}`}
                    className={cn(
                      'border-b last:border-b-0',
                      idx % 2 === 0 ? 'bg-background' : 'bg-muted/20',
                    )}
                  >
                    <td className="px-3 py-2 font-mono text-xs break-all">
                      {v.key}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs break-all text-muted-foreground">
                      {v.value}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
