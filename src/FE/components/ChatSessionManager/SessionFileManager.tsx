import React, {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import {
  IconDownload,
  IconFile,
  IconFolder,
  IconFolderPlus,
  IconLoader,
  IconPaperclip,
  IconRefresh,
  IconTrash,
  IconArrowUp,
} from '@/components/Icons';

import {
  deleteDockerFile,
  downloadDockerFile,
  listDockerDirectory,
  mkdirDockerDir,
  uploadDockerFiles,
} from '@/apis/dockerSessionsApi';
import { DirectoryListResponse, FileEntry } from '@/types/dockerSessions';
import { getApiErrorMessage } from '@/utils/apiError';

export type FileManagerHandle = {
  refresh: () => void;
};

type Props = {
  chatId: string;
  encryptedSessionId: string;
  refreshKey?: number;
  onOpenTextFile: (path: string) => void;
};

const SessionFileManager = forwardRef<FileManagerHandle, Props>(
  ({ chatId, encryptedSessionId, refreshKey, onOpenTextFile }, ref) => {
    const { t } = useTranslation();
    const [loading, setLoading] = useState(false);
    const [uploading, setUploading] = useState(false);
    const [currentDir, setCurrentDir] = useState('/app');
    const [inputDir, setInputDir] = useState('/app');
    const [data, setData] = useState<DirectoryListResponse | null>(null);
    const [dirError, setDirError] = useState<string | null>(null);
    const [selected, setSelected] = useState<FileEntry | null>(null);
    const fileInputRef = useRef<HTMLInputElement | null>(null);
    const [dragOver, setDragOver] = useState(false);

    const load = useCallback(
      async (path?: string | null) => {
        setLoading(true);
        try {
          const target = path ?? currentDir;
          const res = await listDockerDirectory(chatId, encryptedSessionId, target);
          setData(res);
          setCurrentDir(res.path);
          setInputDir(res.path);
          setDirError(null);
          setSelected(null);
        } catch (e: any) {
          setData(null);
          setSelected(null);
          setDirError(getApiErrorMessage(e, t('Directory does not exist')));
        } finally {
          setLoading(false);
        }
      },
      [chatId, currentDir, encryptedSessionId, t],
    );

    useImperativeHandle(ref, () => ({ refresh: () => load(currentDir) }), [currentDir, load]);

    useEffect(() => {
      load(null);
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [chatId, encryptedSessionId]);

    useEffect(() => {
      if (refreshKey == null) return;
      load(currentDir);
    }, [currentDir, load, refreshKey]);

    const entries = data?.entries ?? [];

    const canDownload = selected && !selected.isDirectory;
    const canDelete = !!selected;

    const doUpload = useCallback(
      async (files: File[]) => {
        if (files.length === 0) return;
        setUploading(true);
        try {
          await uploadDockerFiles(chatId, encryptedSessionId, currentDir, files);
          toast.success(t('Upload successful'));
          await load(currentDir);
        } catch (e: any) {
          toast.error(getApiErrorMessage(e, t('Upload failed')));
        } finally {
          setUploading(false);
        }
      },
      [chatId, currentDir, load, encryptedSessionId, t],
    );

    const onDrop = useCallback(
      async (e: React.DragEvent) => {
        e.preventDefault();
        setDragOver(false);
        if (uploading || loading) return;
        const files = Array.from(e.dataTransfer.files ?? []);
        await doUpload(files);
      },
      [doUpload, loading, uploading],
    );

    const selectedPath = selected?.path ?? '';
    const selectedName = selected?.name ?? '';

    const listView = useMemo(() => {
      if (loading) {
        return (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <IconLoader className="animate-spin" size={16} /> {t('Loading...')}
          </div>
        );
      }

      if (entries.length === 0) {
        return (
          <div className="text-sm text-muted-foreground">
            {t('This directory is empty')}
          </div>
        );
      }

      return (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
          {entries.map((e) => (
            <div
              key={e.path}
              className={cn(
                'border rounded-md p-2 flex items-center gap-2 cursor-pointer hover:bg-accent/40',
                selected?.path === e.path && 'bg-accent',
              )}
              onClick={() => {
                setSelected(e);
                if (!e.isDirectory) {
                  onOpenTextFile(e.path);
                }
              }}
              onDoubleClick={() => {
                if (e.isDirectory) {
                  load(e.path);
                }
              }}
              title={e.path}
            >
              <div className="shrink-0 text-muted-foreground">
                {e.isDirectory ? <IconFolder size={18} /> : <IconFile size={18} />}
              </div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm">{e.name}</div>
                <div className="truncate text-xs text-muted-foreground">
                  {e.isDirectory ? t('Directory') : `${e.size} bytes`}
                </div>
              </div>
            </div>
          ))}
        </div>
      );
    }, [entries, load, loading, onOpenTextFile, selected?.path, t]);

    return (
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2 text-sm font-medium">
          <IconFolder size={16} /> {t('File manager')}
        </div>

        <div className="flex flex-col sm:flex-row gap-2 items-stretch sm:items-center">
          <div className="flex-1 flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              className="h-8 w-8 p-0"
              onClick={() => {
                const parent = getParentDir(currentDir);
                setInputDir(parent);
                load(parent);
              }}
              disabled={loading || uploading}
              title={t('Up')}
            >
              <IconArrowUp size={16} />
            </Button>
            <Input
              value={inputDir}
              onChange={(e) => setInputDir(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  load(inputDir);
                }
              }}
              className="flex-1 font-mono text-sm"
            />
          </div>

          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              className="h-8 w-8 p-0"
              onClick={() => load(inputDir)}
              disabled={loading || uploading}
              title={t('Refresh')}
            >
              <IconRefresh size={16} />
            </Button>

            <Button
              variant="ghost"
              size="sm"
              className="h-8 w-8 p-0"
              onClick={async () => {
                const name = window.prompt(t('New folder name'));
                if (!name) return;
                const path = `${currentDir.replace(/\/+$/, '')}/${name}`;
                try {
                  await mkdirDockerDir(chatId, encryptedSessionId, path);
                  toast.success(t('Created successful'));
                  await load(currentDir);
                } catch (e: any) {
                  toast.error(getApiErrorMessage(e, t('Create failed')));
                }
              }}
              disabled={loading || uploading}
              title={t('Create folder')}
            >
              <IconFolderPlus size={16} />
            </Button>

            <Button
              variant="ghost"
              size="sm"
              className="h-8 w-8 p-0"
              onClick={() => fileInputRef.current?.click()}
              disabled={loading || uploading}
              title={t('Upload files')}
            >
              {uploading ? (
                <IconLoader className="animate-spin" size={16} />
              ) : (
                <IconPaperclip size={16} />
              )}
            </Button>

            <input
              ref={fileInputRef}
              type="file"
              multiple
              className="hidden"
              onChange={async (e) => {
                const files = Array.from(e.target.files ?? []);
                e.target.value = '';
                await doUpload(files);
              }}
            />
          </div>
        </div>

        <div
          className={cn(
            'relative border rounded-md p-3 min-h-[160px]',
            (loading || uploading) && 'opacity-80 pointer-events-none',
            dragOver && 'ring-2 ring-primary',
          )}
          onDragEnter={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={(e) => {
            e.preventDefault();
            setDragOver(false);
          }}
          onDrop={onDrop}
        >
          {dragOver && (
            <div className="absolute inset-0 bg-primary/10 flex items-center justify-center text-sm">
              {t('Drop files here to upload')}
            </div>
          )}
          {dirError ? (
            <div className="text-sm text-muted-foreground">{dirError}</div>
          ) : (
            listView
          )}
        </div>

        <div className="flex items-center justify-between gap-2">
          <div className="text-xs text-muted-foreground truncate">
            {selected
              ? `${selectedName} (${selectedPath})`
              : t('No file selected')}
          </div>
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              className="h-8 px-2 gap-1"
              disabled={!canDownload || loading || uploading}
              onClick={async () => {
                if (!selected || selected.isDirectory) return;
                try {
                  const blob = await downloadDockerFile(chatId, encryptedSessionId, selected.path);
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = selected.name;
                  a.click();
                  URL.revokeObjectURL(url);
                } catch (e: any) {
                  toast.error(getApiErrorMessage(e, t('Download failed')));
                }
              }}
              title={t('Download')}
            >
              <IconDownload size={16} />
              {t('Download')}
            </Button>

            <Button
              variant="ghost"
              size="sm"
              className="h-8 px-2 gap-1"
              disabled={!canDelete || loading || uploading}
              onClick={async () => {
                if (!selected) return;
                const ok = window.confirm(
                  t('Are you sure you want to delete {{name}}?', { name: selected.name }),
                );
                if (!ok) return;
                try {
                  await deleteDockerFile(chatId, encryptedSessionId, selected.path);
                  toast.success(t('Deleted successful'));
                  await load(currentDir);
                } catch (e: any) {
                  toast.error(getApiErrorMessage(e, t('Delete failed')));
                }
              }}
              title={t('Delete')}
            >
              <IconTrash size={16} />
              {t('Delete')}
            </Button>
          </div>
        </div>
      </div>
    );
  },
);

SessionFileManager.displayName = 'SessionFileManager';
export default SessionFileManager;

function getParentDir(path: string): string {
  const p = (path || '').replace(/\/+$/, '');
  if (!p || p === '/') return '/';
  const parts = p.split('/').filter(Boolean);
  if (parts.length <= 1) return '/';
  return '/' + parts.slice(0, -1).join('/');
}
