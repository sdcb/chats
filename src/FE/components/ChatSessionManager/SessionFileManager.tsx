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
  IconCheck,
  IconDownload,
  IconFile,
  IconFolder,
  IconFolderPlus,
  IconLoader,
  IconPaperclip,
  IconRefresh,
  IconTrash,
  IconArrowUp,
  IconX,
} from '@/components/Icons';

import {
  deleteDockerFile,
  getDockerFileDownloadUrl,
  listDockerDirectory,
  mkdirDockerDir,
  readDockerTextFile,
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
  onSelectFile: (entry: FileEntry | null) => void;
  onEditFile: (path: string) => void;
};

// 格式化文件大小为人类可读格式
function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

const SessionFileManager = forwardRef<FileManagerHandle, Props>(
  ({ chatId, encryptedSessionId, refreshKey, onSelectFile, onEditFile }, ref) => {
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
    const dragCounterRef = useRef(0);
    const [deletingPath, setDeletingPath] = useState<string | null>(null);
    const [creatingFolder, setCreatingFolder] = useState(false);
    const [newFolderName, setNewFolderName] = useState('');
    const [savingFolder, setSavingFolder] = useState(false);
    const newFolderInputRef = useRef<HTMLInputElement | null>(null);
    // 用于移动端模拟双击
    const lastClickRef = useRef<{ path: string; time: number } | null>(null);

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

    // 初始加载 - 当 session 变化时
    useEffect(() => {
      load(null);
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [chatId, encryptedSessionId]);

    // refreshKey 变化时刷新（跳过初始值 0）
    const prevRefreshKeyRef = useRef(refreshKey);
    useEffect(() => {
      // 只有当 refreshKey 真正变化（不是初始挂载）时才刷新
      if (refreshKey === prevRefreshKeyRef.current) return;
      prevRefreshKeyRef.current = refreshKey;
      if (refreshKey == null) return;
      load(currentDir);
    }, [currentDir, load, refreshKey]);

    const entries = data?.entries ?? [];

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

    const handleDownload = useCallback(
      (e: React.MouseEvent, entry: FileEntry) => {
        e.stopPropagation();
        if (entry.isDirectory) return;
        const url = getDockerFileDownloadUrl(chatId, encryptedSessionId, entry.path);
        window.open(url, '_blank');
      },
      [chatId, encryptedSessionId],
    );

    const handleDelete = useCallback(
      async (e: React.MouseEvent, entry: FileEntry) => {
        e.stopPropagation();
        const ok = window.confirm(
          t('Are you sure you want to delete {{name}}?', { name: entry.name }),
        );
        if (!ok) return;
        setDeletingPath(entry.path);
        try {
          await deleteDockerFile(chatId, encryptedSessionId, entry.path);
          toast.success(t('Deleted successful'));
          await load(currentDir);
        } catch (e: any) {
          toast.error(getApiErrorMessage(e, t('Delete failed')));
        } finally {
          setDeletingPath(null);
        }
      },
      [chatId, currentDir, encryptedSessionId, load, t],
    );

    const onDrop = useCallback(
      async (e: React.DragEvent) => {
        e.preventDefault();
        e.stopPropagation();
        dragCounterRef.current = 0;
        setDragOver(false);
        if (uploading || loading) return;
        const files = Array.from(e.dataTransfer.files ?? []);
        await doUpload(files);
      },
      [doUpload, loading, uploading],
    );

    const onDragEnter = useCallback((e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      dragCounterRef.current++;
      if (dragCounterRef.current === 1) {
        setDragOver(true);
      }
    }, []);

    const onDragOver = useCallback((e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
    }, []);

    const onDragLeave = useCallback((e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      dragCounterRef.current--;
      if (dragCounterRef.current === 0) {
        setDragOver(false);
      }
    }, []);

    const handleDoubleClickFile = useCallback(
      async (entry: FileEntry) => {
        if (entry.isDirectory) {
          load(entry.path);
          return;
        }
        // 调用API判断是否为文本文件
        try {
          const res = await readDockerTextFile(chatId, encryptedSessionId, entry.path);
          if (res.isText) {
            onEditFile(entry.path);
          } else {
            // 非文本文件，下载
            const url = getDockerFileDownloadUrl(chatId, encryptedSessionId, entry.path);
            window.open(url, '_blank');
          }
        } catch {
          // 出错时默认下载
          const url = getDockerFileDownloadUrl(chatId, encryptedSessionId, entry.path);
          window.open(url, '_blank');
        }
      },
      [chatId, encryptedSessionId, load, onEditFile],
    );

    const handleStartCreateFolder = useCallback(() => {
      setCreatingFolder(true);
      setNewFolderName('');
      setTimeout(() => {
        newFolderInputRef.current?.focus();
      }, 0);
    }, []);

    const handleCancelCreateFolder = useCallback(() => {
      setCreatingFolder(false);
      setNewFolderName('');
    }, []);

    const handleSaveNewFolder = useCallback(async () => {
      if (!newFolderName.trim()) {
        toast.error(t('Please enter a folder name'));
        return;
      }
      const folderName = newFolderName.trim();
      const path = `${currentDir.replace(/\/+$/, '')}/${folderName}`;
      setSavingFolder(true);
      try {
        await mkdirDockerDir(chatId, encryptedSessionId, path);
        setCreatingFolder(false);
        setNewFolderName('');
        // 重新加载并选中新建的文件夹
        const res = await listDockerDirectory(chatId, encryptedSessionId, currentDir);
        setData(res);
        setCurrentDir(res.path);
        setInputDir(res.path);
        setDirError(null);
        // 找到新建的文件夹并选中
        const newFolder = res.entries.find(e => e.name === folderName && e.isDirectory);
        if (newFolder) {
          setSelected(newFolder);
          onSelectFile(newFolder);
        } else {
          setSelected(null);
        }
      } catch (e: any) {
        toast.error(getApiErrorMessage(e, t('Create failed')));
      } finally {
        setSavingFolder(false);
      }
    }, [chatId, currentDir, encryptedSessionId, newFolderName, onSelectFile, t]);

    const newFolderRow = useMemo(() => {
      if (!creatingFolder) return null;
      return (
        <div
          className={cn(
            'flex items-center gap-3 px-3 py-2 transition-colors bg-accent/60',
          )}
        >
          {/* 图标 */}
          <div className="shrink-0 text-muted-foreground">
            <IconFolder size={16} />
          </div>
          
          {/* 输入框 */}
          <div className="flex-1 min-w-0">
            <Input
              ref={newFolderInputRef}
              value={newFolderName}
              onChange={(e) => setNewFolderName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  handleSaveNewFolder();
                } else if (e.key === 'Escape') {
                  handleCancelCreateFolder();
                }
              }}
              placeholder={t('Enter folder name')}
              className="h-7 text-sm"
              disabled={savingFolder}
            />
          </div>
          
          {/* 操作按钮 */}
          <div className="shrink-0 flex items-center gap-1">
            <button
              className="p-1.5 rounded hover:bg-primary/10 hover:text-primary transition-colors"
              onClick={handleSaveNewFolder}
              disabled={savingFolder}
              title={t('Save')}
            >
              {savingFolder ? (
                <IconLoader className="animate-spin" size={14} />
              ) : (
                <IconCheck size={14} />
              )}
            </button>
            <button
              className="p-1.5 rounded hover:bg-destructive/10 hover:text-destructive transition-colors"
              onClick={handleCancelCreateFolder}
              disabled={savingFolder}
              title={t('Cancel')}
            >
              <IconX size={14} />
            </button>
          </div>
        </div>
      );
    }, [creatingFolder, handleCancelCreateFolder, handleSaveNewFolder, newFolderName, savingFolder, t]);

    const listView = useMemo(() => {
      if (loading) {
        return (
          <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground h-full">
            <IconLoader className="animate-spin" size={16} /> {t('Loading...')}
          </div>
        );
      }

      if (entries.length === 0 && !creatingFolder) {
        return (
          <div className="flex items-center justify-center text-sm text-muted-foreground h-full">
            {t('This directory is empty')}
          </div>
        );
      }

      return (
        <div className="flex flex-col">
          {newFolderRow}
          {entries.map((e) => (
            <div
              key={e.path}
              className={cn(
                'flex items-center gap-3 px-3 py-2 cursor-pointer transition-colors',
                'hover:bg-accent/40',
                selected?.path === e.path && 'bg-accent',
              )}
              onClick={() => {
                const now = Date.now();
                const last = lastClickRef.current;
                // 检测移动端双击：300ms内点击同一文件
                if (last && last.path === e.path && now - last.time < 300) {
                  lastClickRef.current = null;
                  handleDoubleClickFile(e);
                  return;
                }
                lastClickRef.current = { path: e.path, time: now };
                setSelected(e);
                onSelectFile(e);
              }}
              onDoubleClick={() => handleDoubleClickFile(e)}
              title={e.path}
            >
              {/* 图标 */}
              <div className="shrink-0 text-muted-foreground">
                {e.isDirectory ? <IconFolder size={16} /> : <IconFile size={16} />}
              </div>
              
              {/* 文件名 */}
              <div className="flex-1 min-w-0">
                <span className="text-sm truncate block">{e.name}</span>
              </div>
              
              {/* 文件大小 */}
              <div className="shrink-0 text-xs text-muted-foreground w-20 text-right">
                {e.isDirectory ? '-' : formatFileSize(e.size)}
              </div>
              
              {/* 操作按钮 */}
              <div className="shrink-0 flex items-center gap-1">
                {!e.isDirectory && (
                  <button
                    className="p-1.5 rounded hover:bg-primary/10 hover:text-primary transition-colors"
                    onClick={(ev) => handleDownload(ev, e)}
                    title={t('Download')}
                  >
                    <IconDownload size={14} />
                  </button>
                )}
                {!e.isDirectory && (
                  <button
                    className="p-1.5 rounded hover:bg-destructive/10 hover:text-destructive transition-colors"
                    onClick={(ev) => handleDelete(ev, e)}
                    disabled={deletingPath === e.path}
                    title={t('Delete')}
                  >
                    {deletingPath === e.path ? (
                      <IconLoader className="animate-spin" size={14} />
                    ) : (
                      <IconTrash size={14} />
                    )}
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      );
    }, [creatingFolder, entries, deletingPath, handleDelete, handleDoubleClickFile, handleDownload, loading, newFolderRow, onSelectFile, selected?.path, t]);

    return (
      <div className="h-full flex flex-col gap-3">
        {/* 路径栏 */}
        <div className="flex flex-col sm:flex-row gap-2 items-stretch sm:items-center shrink-0">
          <div className="flex-1 flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              className="h-9 w-9 p-0"
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
              className="flex-1 font-mono text-sm h-9"
            />
          </div>

          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              className="h-9 w-9 p-0"
              onClick={() => load(inputDir)}
              disabled={loading || uploading}
              title={t('Refresh')}
            >
              <IconRefresh size={16} />
            </Button>

            <Button
              variant="ghost"
              size="sm"
              className="h-9 w-9 p-0"
              onClick={handleStartCreateFolder}
              disabled={loading || uploading || creatingFolder}
              title={t('Create folder')}
            >
              <IconFolderPlus size={16} />
            </Button>

            <Button
              variant="ghost"
              size="sm"
              className="h-9 w-9 p-0"
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

        {/* 文件列表区域 */}
        <div
          className={cn(
            'relative border rounded-lg flex-1 min-h-0 overflow-auto flex flex-col',
            (loading || uploading) && 'opacity-80 pointer-events-none',
            dragOver && 'ring-2 ring-primary',
          )}
          onDragEnter={onDragEnter}
          onDragOver={onDragOver}
          onDragLeave={onDragLeave}
          onDrop={onDrop}
        >
          {dragOver && (
            <div className="absolute inset-0 bg-primary/10 flex items-center justify-center text-sm z-10">
              {t('Drop files here to upload')}
            </div>
          )}
          {dirError ? (
            <div className="flex flex-col h-full">
              {/* 错误信息区域 - 类似命令执行的样式 */}
              <div className="p-3">
                <div className="text-sm font-mono whitespace-pre-wrap break-words text-red-300">
                  {dirError}
                </div>
              </div>
              {/* 空白占位区域 */}
              <div className="flex-1 p-3" />
            </div>
          ) : (
            <div className="p-3 flex-1">
              {listView}
            </div>
          )}
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
