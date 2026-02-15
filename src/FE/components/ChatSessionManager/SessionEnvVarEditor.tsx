import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import Tips from '@/components/Tips/Tips';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import {
  IconCheck,
  IconClipboard,
  IconEdit,
  IconLoader,
  IconTrash,
  IconX,
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
type EditMode = 'table' | 'raw';

let idCounter = 0;
function generateId() {
  return `env-${Date.now()}-${idCounter++}`;
}

function normalizeRawEnvText(raw: string) {
  return raw
    .replace(/\r\n/g, '\n')
    .split('\n')
    .map((line) => line.trimEnd())
    .filter((line) => line.trim() !== '')
    .join('\n');
}

function serializeEnvVars(vars: EnvironmentVariable[]) {
  return vars
    .filter((v) => v.key.trim() !== '' || v.value.trim() !== '')
    .map((v) => `${v.key}=${v.value}`)
    .join('\n');
}

function parseRawEnvText(raw: string) {
  const lines = raw.replace(/\r\n/g, '\n').split('\n');
  const variables: EnvironmentVariable[] = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.trim() === '') continue;
    const eqIndex = line.indexOf('=');
    if (eqIndex <= 0) {
      return { variables: [], errorLine: i + 1 };
    }
    const key = line.slice(0, eqIndex).trim();
    if (key === '') {
      return { variables: [], errorLine: i + 1 };
    }
    const value = line.slice(eqIndex + 1);
    variables.push({ key, value });
  }

  return { variables, errorLine: null as number | null };
}

export default function SessionEnvVarEditor({
  chatId,
  encryptedSessionId,
}: Props) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editMode, setEditMode] = useState<EditMode>('table');
  const [systemVars, setSystemVars] = useState<EnvironmentVariable[]>([]);
  const [userVars, setUserVars] = useState<EditableEnvVar[]>([]);
  const [originalUserVars, setOriginalUserVars] = useState<EnvironmentVariable[]>([]);
  const [rawText, setRawText] = useState('');
  const [rawError, setRawError] = useState<string | null>(null);
  const [activeBlock, setActiveBlock] = useState<'user' | 'system' | null>(null);
  const [activeRowId, setActiveRowId] = useState<string | null>(null);
  const [copiedId, setCopiedId] = useState<string | null>(null);

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
      setRawText(serializeEnvVars(result.userVariables));
      setRawError(null);
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
    if (isEditing && editMode === 'raw') {
      return normalizeRawEnvText(rawText) !== normalizeRawEnvText(serializeEnvVars(originalUserVars));
    }
    // 过滤掉空行后再比较
    const currentVars = userVars.filter((v) => v.key.trim() !== '' || v.value.trim() !== '');
    if (currentVars.length !== originalUserVars.length) return true;
    const currentSorted = [...currentVars]
      .map((v) => `${v.key}=${v.value}`)
      .sort();
    const originalSorted = [...originalUserVars]
      .map((v) => `${v.key}=${v.value}`)
      .sort();
    return currentSorted.join('\n') !== originalSorted.join('\n');
  }, [editMode, isEditing, originalUserVars, rawText, userVars]);

  const handleEnterEditMode = useCallback(() => {
    setIsEditing(true);
    setEditMode('table');
    setRawText(serializeEnvVars(userVars));
    setRawError(null);
    // 自动添加一个空行
    setUserVars((prev) => {
      const hasEmptyRow = prev.some((v) => v.key.trim() === '' && v.value.trim() === '');
      if (!hasEmptyRow) {
        return [...prev, { id: generateId(), key: '', value: '' }];
      }
      return prev;
    });
  }, [userVars]);

  const handleCancelEdit = useCallback(() => {
    // 恢复原始数据并退出编辑模式
    setUserVars(originalUserVars.map((v) => ({ ...v, id: generateId() })));
    setRawText(serializeEnvVars(originalUserVars));
    setRawError(null);
    setEditMode('table');
    setIsEditing(false);
  }, [originalUserVars]);

  const handleCancelClick = useCallback(() => {
    if (hasChanges) {
      if (confirm(t('You have unsaved changes. Are you sure you want to discard them?'))) {
        handleCancelEdit();
      }
    } else {
      handleCancelEdit();
    }
  }, [hasChanges, t, handleCancelEdit]);

  const handleUpdateUserVar = useCallback(
    (id: string, field: 'key' | 'value', newValue: string) => {
      setUserVars((prev) => {
        const updated = prev.map((v) => (v.id === id ? { ...v, [field]: newValue } : v));
        // 检查是否还有空行，如果没有则添加一个
        const hasEmptyRow = updated.some((v) => v.key.trim() === '' && v.value.trim() === '');
        if (!hasEmptyRow) {
          return [...updated, { id: generateId(), key: '', value: '' }];
        }
        return updated;
      });
    },
    [],
  );

  const handleDeleteUserVar = useCallback((id: string) => {
    setUserVars((prev) => prev.filter((v) => v.id !== id));
  }, []);

  const handleRawTextChange = useCallback(
    (value: string) => {
      setRawText(value);
      const parsed = parseRawEnvText(value);
      if (parsed.errorLine) {
        setRawError(
          t('Invalid environment variable line {{line}}. Use KEY=VALUE', {
            line: parsed.errorLine,
          }),
        );
        return;
      }

      setRawError(null);
      const nextRows = parsed.variables.map((v) => ({ ...v, id: generateId() }));
      const hasEmptyRow = nextRows.some(
        (v) => v.key.trim() === '' && v.value.trim() === '',
      );
      setUserVars(
        hasEmptyRow
          ? nextRows
          : [...nextRows, { id: generateId(), key: '', value: '' }],
      );
    },
    [t],
  );

  const handleSwitchEditMode = useCallback(
    (mode: EditMode) => {
      if (mode === editMode) return;

      if (mode === 'raw') {
        const currentVars = userVars.filter(
          (v) => v.key.trim() !== '' || v.value.trim() !== '',
        );
        setRawText(serializeEnvVars(currentVars));
        setRawError(null);
        setEditMode('raw');
        return;
      }

      const parsed = parseRawEnvText(rawText);
      if (parsed.errorLine) {
        const errorMessage = t(
          'Invalid environment variable line {{line}}. Use KEY=VALUE',
          {
            line: parsed.errorLine,
          },
        );
        setRawError(errorMessage);
        toast.error(errorMessage);
        return;
      }

      setRawError(null);
      const nextRows = parsed.variables.map((v) => ({ ...v, id: generateId() }));
      const hasEmptyRow = nextRows.some(
        (v) => v.key.trim() === '' && v.value.trim() === '',
      );
      setUserVars(
        hasEmptyRow
          ? nextRows
          : [...nextRows, { id: generateId(), key: '', value: '' }],
      );
      setEditMode('table');
    },
    [editMode, rawText, t, userVars],
  );

  const handleSave = useCallback(async () => {
    let validVars: EnvironmentVariable[] = [];

    if (editMode === 'raw') {
      const parsed = parseRawEnvText(rawText);
      if (parsed.errorLine) {
        const errorMessage = t(
          'Invalid environment variable line {{line}}. Use KEY=VALUE',
          {
            line: parsed.errorLine,
          },
        );
        setRawError(errorMessage);
        toast.error(errorMessage);
        return;
      }
      setRawError(null);
      validVars = parsed.variables.filter((v) => v.key.trim() !== '');
    } else {
      validVars = userVars.filter((v) => v.key.trim() !== '');
    }

    // Validate
    const keys = validVars.map((v) => v.key.trim());
    const uniqueKeys = new Set(keys);
    if (uniqueKeys.size !== keys.length) {
      toast.error(t('Duplicate environment variable keys are not allowed'));
      return;
    }

    // Validate key format: cannot start with a digit
    const invalidKey = validVars.find((v) => /^[0-9]/.test(v.key.trim()));
    if (invalidKey) {
      toast.error(t('Environment variable key cannot start with a digit'));
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
      setRawText(serializeEnvVars(normalizedVars));
      setRawError(null);
      // Also update userVars to remove empty keys and normalize
      setUserVars(normalizedVars.map((v) => ({ ...v, id: generateId() })));
      setEditMode('table');
      // Exit edit mode after successful save
      setIsEditing(false);
    } catch (e: any) {
      toast.error(e?.message || t('Failed to save environment variables'));
    } finally {
      setSaving(false);
    }
  }, [chatId, editMode, encryptedSessionId, rawText, t, userVars]);

  const handleCopyRow = useCallback((id: string, key: string, value: string) => {
    navigator.clipboard.writeText(`${key}=${value}`);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  }, []);

  const handleCopyAll = useCallback((blockId: string, vars: EnvironmentVariable[]) => {
    const text = vars.map((v) => `${v.key}=${v.value}`).join('\n');
    navigator.clipboard.writeText(text);
    setCopiedId(blockId);
    setTimeout(() => setCopiedId(null), 2000);
  }, []);

  const handleCopyAllKeys = useCallback(
    (blockId: string, vars: EnvironmentVariable[]) => {
      const keys = vars
        .map((v) => v.key.trim())
        .filter((key) => key !== '')
        .join('\n');
      navigator.clipboard.writeText(keys);
      setCopiedId(blockId);
      setTimeout(() => setCopiedId(null), 2000);
    },
    [],
  );

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
      <div
        className={cn('space-y-2 group/user', activeBlock === 'user' && 'is-active')}
        onClick={() => {
          setActiveBlock('user');
          setActiveRowId(null);
        }}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h4 className="text-xs font-medium text-muted-foreground/70 uppercase tracking-wide">
              {t('User Variables')}
            </h4>
            {isEditing && hasChanges && (
              <span className="text-xs text-orange-500">
                ({t('Modified')})
              </span>
            )}
          </div>
          <div className="flex items-center gap-1">
            {isEditing ? (
              <>
                <Button
                  size="sm"
                  variant={editMode === 'table' ? 'secondary' : 'ghost'}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleSwitchEditMode('table');
                  }}
                  disabled={saving}
                  className="h-7 px-2 text-xs"
                >
                  {t('Table')}
                </Button>
                <Button
                  size="sm"
                  variant={editMode === 'raw' ? 'secondary' : 'ghost'}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleSwitchEditMode('raw');
                  }}
                  disabled={saving}
                  className="h-7 px-2 text-xs"
                >
                  {t('Raw edit')}
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={handleCancelClick}
                  disabled={saving}
                  className="h-7 w-7 p-0"
                  title={t('Cancel')}
                >
                  <IconX size={18} />
                </Button>
                {hasChanges && (
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={handleSave}
                    disabled={saving}
                    className="h-7 w-7 p-0"
                    title={t('Save')}
                  >
                    {saving ? (
                      <IconLoader size={18} className="animate-spin" />
                    ) : (
                      <IconCheck size={18} />
                    )}
                  </Button>
                )}
              </>
            ) : (
              <>
                {userVars.length > 0 && (
                  <Tips
                    content={t('Copy all')}
                    trigger={
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleCopyAll('user-all', userVars);
                        }}
                        className="h-7 w-7 p-0 opacity-0 group-hover/user:opacity-100 group-[.is-active]/user:opacity-100 transition-opacity"
                      >
                        {copiedId === 'user-all' ? (
                          <IconCheck size={18} className="text-green-500" />
                        ) : (
                          <IconClipboard size={18} />
                        )}
                      </Button>
                    }
                  />
                )}
                <Tips
                  content={t('Edit')}
                  trigger={
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleEnterEditMode();
                      }}
                      className="h-7 w-7 p-0"
                    >
                      <IconEdit size={18} />
                    </Button>
                  }
                />
              </>
            )}
          </div>
        </div>
        {isEditing ? (
          editMode === 'raw' ? (
            <>
              <Textarea
                value={rawText}
                onChange={(e) => handleRawTextChange(e.target.value)}
                placeholder="KEY=VALUE"
                spellCheck={false}
                rows={4}
                className="font-mono text-xs"
              />
              {rawError && (
                <div className="text-xs text-destructive">
                  {rawError}
                </div>
              )}
            </>
          ) : (
            <div className="rounded-md border overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-muted/50 border-b">
                    <th className="text-left px-3 py-2 font-medium w-1/3 group">
                      <div className="inline-flex items-center gap-1">
                        <span>{t('Key')}</span>
                        <Tips
                          content={t('Copy all keys')}
                          trigger={
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleCopyAllKeys('user-keys', userVars);
                              }}
                              className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 group-[.is-active]/user:opacity-100 transition-opacity"
                            >
                              {copiedId === 'user-keys' ? (
                                <IconCheck size={18} className="text-green-500" />
                              ) : (
                                <IconClipboard size={18} />
                              )}
                            </Button>
                          }
                        />
                      </div>
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
          )
        ) : userVars.length === 0 ? (
          <div className="text-xs text-muted-foreground/70 py-2">
            {isEditing
              ? t('No user environment variables. Click + to add one.')
              : t('No user environment variables. Click edit to add.')}
          </div>
        ) : (
          <div className="rounded-md border overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-muted/50 border-b">
                  <th className="text-left px-3 py-2 font-medium w-1/3 group">
                    <div className="inline-flex items-center gap-1">
                      <span>{t('Key')}</span>
                      <Tips
                        content={t('Copy all keys')}
                        trigger={
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleCopyAllKeys('user-keys', userVars);
                            }}
                            className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 group-[.is-active]/user:opacity-100 transition-opacity"
                          >
                            {copiedId === 'user-keys' ? (
                              <IconCheck size={18} className="text-green-500" />
                            ) : (
                              <IconClipboard size={18} />
                            )}
                          </Button>
                        }
                      />
                    </div>
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
                      'border-b last:border-b-0 group/row',
                      idx % 2 === 0 ? 'bg-background' : 'bg-muted/20',
                      activeRowId === `user-${v.id}` && 'is-active-row',
                    )}
                    onClick={(e) => {
                      e.stopPropagation();
                      setActiveBlock('user');
                      setActiveRowId(`user-${v.id}`);
                    }}
                  >
                    <td className="px-3 py-2 font-mono text-xs break-all">
                      {v.key}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs break-all text-muted-foreground">
                      {v.value}
                    </td>
                    <td className="px-2 py-1.5">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleCopyRow(`user-${v.id}`, v.key, v.value);
                        }}
                        className="h-7 w-7 p-0 text-muted-foreground opacity-0 group-hover/row:opacity-100 group-[.is-active-row]/row:opacity-100 transition-opacity"
                        title={t('Copy')}
                      >
                        {copiedId === `user-${v.id}` ? (
                          <IconCheck size={14} className="text-green-500" />
                        ) : (
                          <IconClipboard size={14} />
                        )}
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
        <div
          className={cn('space-y-2 group/system', activeBlock === 'system' && 'is-active')}
          onClick={() => {
            setActiveBlock('system');
            setActiveRowId(null);
          }}
        >
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-medium text-muted-foreground/70 uppercase tracking-wide">
              {t('System Variables')}
            </h4>
            <Tips
              content={t('Copy all')}
              trigger={
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleCopyAll('system-all', systemVars);
                  }}
                  className="h-7 w-7 p-0 opacity-0 group-hover/system:opacity-100 group-[.is-active]/system:opacity-100 transition-opacity"
                >
                  {copiedId === 'system-all' ? (
                    <IconCheck size={18} className="text-green-500" />
                  ) : (
                    <IconClipboard size={18} />
                  )}
                </Button>
              }
            />
          </div>
          <div className="rounded-md border overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-muted/50 border-b">
                  <th className="text-left px-3 py-2 font-medium w-1/3 group">
                    <div className="inline-flex items-center gap-1">
                      <span>{t('Key')}</span>
                      <Tips
                        content={t('Copy all keys')}
                        trigger={
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleCopyAllKeys('system-keys', systemVars);
                            }}
                            className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 group-[.is-active]/system:opacity-100 transition-opacity"
                          >
                            {copiedId === 'system-keys' ? (
                              <IconCheck size={18} className="text-green-500" />
                            ) : (
                              <IconClipboard size={18} />
                            )}
                          </Button>
                        }
                      />
                    </div>
                  </th>
                  <th className="text-left px-3 py-2 font-medium">
                    {t('Value')}
                  </th>
                  <th className="w-10"></th>
                </tr>
              </thead>
              <tbody>
                {systemVars.map((v, idx) => (
                  <tr
                    key={`sys-${idx}`}
                    className={cn(
                      'border-b last:border-b-0 group/sysrow',
                      idx % 2 === 0 ? 'bg-background' : 'bg-muted/20',
                      activeRowId === `sys-${idx}` && 'is-active-row',
                    )}
                    onClick={(e) => {
                      e.stopPropagation();
                      setActiveBlock('system');
                      setActiveRowId(`sys-${idx}`);
                    }}
                  >
                    <td className="px-3 py-2 font-mono text-xs break-all">
                      {v.key}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs break-all text-muted-foreground">
                      {v.value}
                    </td>
                    <td className="px-2 py-1.5">
                      <Tips
                        content={t('Copy')}
                        trigger={
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleCopyRow(`sys-${idx}`, v.key, v.value);
                            }}
                            className="h-7 w-7 p-0 text-muted-foreground opacity-0 group-hover/sysrow:opacity-100 group-[.is-active-row]/sysrow:opacity-100 transition-opacity"
                          >
                            {copiedId === `sys-${idx}` ? (
                              <IconCheck size={14} className="text-green-500" />
                            ) : (
                              <IconClipboard size={14} />
                            )}
                          </Button>
                        }
                      />
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
