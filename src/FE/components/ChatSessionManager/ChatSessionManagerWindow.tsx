import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  ContainerSessionDto,
  CreateContainerSessionRequest,
  FileEntry,
  ImageListResponse,
  MemoryLimitResponse,
  NetworkModesResponse,
  ResourceLimitResponse,
} from '@/types/containers';

import {
  IconBolt,
  IconCheck,
  IconDocker,
  IconEdit,
  IconFolder,
  IconInfo,
  IconLoader,
  IconPlus,
  IconSettings,
  IconTrash,
  IconX,
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import FloatingWindow from '@/components/ui/floating-window/FloatingWindow';
import { Skeleton } from '@/components/ui/skeleton';

import CreateSessionPane from './CreateSessionPane';
import SessionCommandRunner from './SessionCommandRunner';
import SessionEnvVarEditor from './SessionEnvVarEditor';
import SessionFileEditor from './SessionFileEditor';
import SessionFileManager, { FileManagerHandle } from './SessionFileManager';
import SessionInfoCard from './SessionInfoCard';

import {
  createChatContainer,
  deleteChatContainer,
  grantContainerToChat,
  listChatContainers,
  revokeContainerFromChat,
  startContainer,
  stopContainer,
} from '@/apis/containersApi';
import { listContainerTemplates } from '@/apis/containersApi';
import { cn } from '@/lib/utils';

type Mode = 'view' | 'create';
type TabType = 'info' | 'env' | 'command' | 'files' | 'editor';

type Props = {
  chatId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export default function ChatSessionManagerWindow({
  chatId,
  open,
  onOpenChange,
}: Props) {
  const { t } = useTranslation();
  const [loadingSessions, setLoadingSessions] = useState(false);
  const [sessions, setSessions] = useState<ContainerSessionDto[]>([]);
  const [selectedLabel, setSelectedLabel] = useState<string | null>(null);
  const [mode, setMode] = useState<Mode>('view');
  const [deletingLabel, setDeletingLabel] = useState<string | null>(null);
  const [confirmDeleteLabel, setConfirmDeleteLabel] = useState<string | null>(
    null,
  );

  const [defaultImage, setDefaultImage] = useState<string>('');
  const [images, setImages] = useState<ImageListResponse>({ images: [] });
  const [cpuLimits, setCpuLimits] = useState<ResourceLimitResponse | null>(
    null,
  );
  const [memoryLimits, setMemoryLimits] = useState<MemoryLimitResponse | null>(
    null,
  );
  const [networkModes, setNetworkModes] = useState<NetworkModesResponse | null>(
    null,
  );
  const [createDefaultsLoaded, setCreateDefaultsLoaded] = useState(false);
  const [sharing, setSharing] = useState(false);

  const [activeFilePath, setActiveFilePath] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<FileEntry | null>(null);
  const [refreshFilesKey, setRefreshFilesKey] = useState(0);
  const fileManagerRef = useRef<FileManagerHandle | null>(null);

  // Tab state
  const [activeTab, setActiveTab] = useState<TabType>('info');

  const selectedSession = useMemo(
    () => sessions.find((s) => s.name === selectedLabel) ?? null,
    [selectedLabel, sessions],
  );

  // 编辑 tab 只有在有文件被选中时才启用
  const isEditorTabEnabled = !!selectedFile && !selectedFile.isDirectory;

  const handleTabChange = useCallback(
    (newTab: TabType) => {
      // 如果编辑 tab 未启用，不允许切换
      if (newTab === 'editor' && !isEditorTabEnabled) return;
      setActiveTab(newTab);
    },
    [isEditorTabEnabled],
  );

  const loadSessions = useCallback(async () => {
    setLoadingSessions(true);
    try {
      const s = await listChatContainers(chatId);
      setSessions(s);
      setSelectedLabel((prev) => {
        if (prev && s.some((x) => x.name === prev)) return prev;
        return s.length > 0 ? s[0].name : null;
      });
    } finally {
      setLoadingSessions(false);
    }
  }, [chatId]);

  const loadCreateDefaults = useCallback(async () => {
    const templates = await listContainerTemplates();
    const first = templates[0];
    setDefaultImage(first?.image ?? 'code-interpreter:latest');
    setImages({ images: templates.map((x) => x.image) });
    setCpuLimits(
      first ? { defaultValue: first.cpuCores, maxValue: first.cpuCores } : null,
    );
    setMemoryLimits(
      first
        ? { defaultBytes: first.memoryBytes, maxBytes: first.memoryBytes }
        : null,
    );
    setNetworkModes({
      defaultNetworkMode: first?.backendNetworkName ?? 'bridge',
      maxAllowedNetworkMode: '*',
      allowedNetworkModes: ['none', 'bridge', 'host'],
    });
    setCreateDefaultsLoaded(true);
  }, []);

  useEffect(() => {
    if (!open) return;
    loadSessions();
  }, [loadCreateDefaults, loadSessions, open]);

  useEffect(() => {
    if (!open) return;
    setMode('view');
    setActiveFilePath(null);
    setSelectedFile(null);
    setActiveTab('info');
    setCreateDefaultsLoaded(false);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    if (mode !== 'create') return;
    if (createDefaultsLoaded) return;
    loadCreateDefaults().catch(() => null);
  }, [createDefaultsLoaded, loadCreateDefaults, mode, open]);

  const handleCreate = useCallback(
    async (req: CreateContainerSessionRequest) => {
      const created = await createChatContainer(chatId, req);
      setSessions((prev) => [...prev, created]);
      setSelectedLabel(created.name);
      setMode('view');
    },
    [chatId],
  );

  const handleDelete = useCallback(
    async (label: string) => {
      const session = sessions.find((s) => s.name === label);
      if (!session) return;
      setDeletingLabel(label);
      try {
        await deleteChatContainer(chatId, session.encryptedId);
        // 请求成功，直接更新前端列表
        setSessions((prev) => prev.filter((s) => s.name !== label));
        if (selectedLabel === label) {
          setSelectedLabel((prev) => {
            const remaining = sessions.filter((s) => s.name !== label);
            return remaining.length > 0 ? remaining[0].name : null;
          });
        }
      } catch {
        // 请求失败，重新获取列表
        await loadSessions();
        toast.error(t('Delete failed'));
      } finally {
        setDeletingLabel(null);
        setConfirmDeleteLabel(null);
      }
    },
    [chatId, loadSessions, selectedLabel, sessions, t],
  );

  const showEmpty = !loadingSessions && sessions.length === 0;
  const toggleSharing = useCallback(async () => {
    if (!selectedSession || !selectedSession.isPermanent || sharing) return;
    setSharing(true);
    try {
      if (selectedSession.grantedChatIds.includes(chatId))
        await revokeContainerFromChat(selectedSession.encryptedId, chatId);
      else await grantContainerToChat(selectedSession.encryptedId, chatId);
      await loadSessions();
    } finally {
      setSharing(false);
    }
  }, [chatId, loadSessions, selectedSession, sharing]);

  const toggleRunning = useCallback(async () => {
    if (!selectedSession || sharing) return;
    setSharing(true);
    try {
      if (selectedSession.isStopped)
        await startContainer(selectedSession.encryptedId);
      else await stopContainer(selectedSession.encryptedId);
      await loadSessions();
    } finally {
      setSharing(false);
    }
  }, [loadSessions, selectedSession, sharing]);

  const tabs = useMemo(
    () => [
      {
        id: 'info' as TabType,
        label: t('Basic Info'),
        icon: <IconInfo size={18} />,
        disabled: false,
      },
      {
        id: 'env' as TabType,
        label: t('Environment Variables'),
        icon: <IconSettings size={18} />,
        disabled: false,
      },
      {
        id: 'command' as TabType,
        label: t('Run command'),
        icon: <IconBolt size={18} />,
        disabled: false,
      },
      {
        id: 'files' as TabType,
        label: t('File manager'),
        icon: <IconFolder size={18} />,
        disabled: false,
      },
      {
        id: 'editor' as TabType,
        label: t('File editor'),
        icon: <IconEdit size={18} />,
        disabled: !isEditorTabEnabled,
      },
    ],
    [isEditorTabEnabled, t],
  );

  return (
    <FloatingWindow
      open={open}
      onOpenChange={onOpenChange}
      title={
        <span className="flex items-center gap-2">
          <IconDocker size={18} />
          {t('Sandbox Manager')}
        </span>
      }
      className="w-[min(100vw,920px)]"
    >
      <div className="flex flex-col h-full">
        {/* Session selector */}
        <div className="p-3 flex items-center gap-2 overflow-x-auto border-b shrink-0">
          {loadingSessions ? (
            <>
              <Skeleton className="h-8 w-28" />
              <Skeleton className="h-8 w-28" />
              <Skeleton className="h-8 w-28" />
            </>
          ) : (
            <>
              {sessions.map((s) => (
                <div
                  key={s.encryptedId}
                  className={cn(
                    'shrink-0 h-8 rounded-md border text-sm flex items-center',
                    selectedLabel === s.name
                      ? 'bg-accent'
                      : 'bg-background hover:bg-accent/60',
                  )}
                >
                  {confirmDeleteLabel === s.name ? (
                    <div className="flex items-center px-2 gap-1">
                      <span className="text-xs mr-1">{s.name}</span>
                      <button
                        className="p-1 hover:bg-accent rounded"
                        onClick={() => handleDelete(s.name)}
                        disabled={deletingLabel === s.name}
                        title={t('Confirm')}
                      >
                        {deletingLabel === s.name ? (
                          <IconLoader size={14} />
                        ) : (
                          <IconCheck size={14} />
                        )}
                      </button>
                      <button
                        className="p-1 hover:bg-accent rounded"
                        onClick={() => setConfirmDeleteLabel(null)}
                        disabled={deletingLabel === s.name}
                        title={t('Cancel')}
                      >
                        <IconX size={14} />
                      </button>
                    </div>
                  ) : (
                    <>
                      <button
                        className="h-full px-3"
                        onClick={() => {
                          setSelectedLabel(s.name);
                          setMode('view');
                          setActiveFilePath(null);
                          setActiveTab('info');
                        }}
                        title={s.image}
                      >
                        {s.name}
                      </button>
                      <button
                        className="pr-2 pl-1 h-full hover:text-destructive"
                        onClick={(e) => {
                          e.stopPropagation();
                          setConfirmDeleteLabel(s.name);
                        }}
                        title={t('Delete')}
                      >
                        <IconTrash size={14} />
                      </button>
                    </>
                  )}
                </div>
              ))}
              <Button
                variant="ghost"
                className="shrink-0 h-8 px-2"
                onClick={() => {
                  setMode('create');
                  setActiveFilePath(null);
                }}
                title={t('Create session')}
              >
                <IconPlus size={16} />
              </Button>
            </>
          )}
        </div>

        {/* Main content area */}
        <div className="flex-1 overflow-hidden">
          {mode === 'create' ? (
            <div className="p-3 h-full overflow-auto">
              <CreateSessionPane
                defaultImage={defaultImage}
                images={images.images}
                cpuLimits={cpuLimits}
                memoryLimits={memoryLimits}
                networkModes={networkModes}
                onCancel={() => {
                  setMode('view');
                  setSelectedLabel((prev) => prev ?? sessions[0]?.name ?? null);
                }}
                onCreate={handleCreate}
              />
            </div>
          ) : showEmpty ? (
            <div className="flex flex-col items-center justify-center h-full text-sm text-muted-foreground">
              {t('No Docker sessions. Click + to create one.')}
            </div>
          ) : selectedSession ? (
            <div className="h-full flex flex-col">
              <div className="flex items-center justify-between gap-2 border-b px-3 py-2 text-sm">
                <span className="text-muted-foreground">
                  {selectedSession.isPermanent
                    ? t('Permanent Docker')
                    : t('Temporary Docker')}{' '}
                  · {selectedSession.isStopped ? t('Stopped') : t('Running')}
                </span>
                <div className="flex gap-2">
                  {selectedSession.isPermanent && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={sharing}
                      onClick={() => toggleSharing().catch(() => null)}
                    >
                      {selectedSession.grantedChatIds.includes(chatId)
                        ? t('Revoke access')
                        : t('Allow this chat')}
                    </Button>
                  )}
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={sharing}
                    onClick={() => toggleRunning().catch(() => null)}
                  >
                    {selectedSession.isStopped ? t('Start') : t('Stop')}
                  </Button>
                </div>
              </div>
              {selectedSession.isStopped && (
                <div className="px-3 py-2 text-xs text-amber-600 border-b">
                  {t('Start this Docker to use commands and files.')}
                </div>
              )}
              {/* Tab content with animation */}
              <div className="flex-1 overflow-hidden relative">
                {/* Info tab */}
                <div
                  className={cn(
                    'h-full overflow-auto p-3 absolute inset-0',
                    activeTab === 'info'
                      ? 'visible'
                      : 'invisible pointer-events-none',
                  )}
                >
                  <SessionInfoCard
                    chatId={chatId}
                    session={selectedSession}
                    onRefreshTimes={loadSessions}
                  />
                </div>

                {/* Environment variables tab */}
                <div
                  className={cn(
                    'h-full overflow-auto p-3 absolute inset-0',
                    activeTab === 'env'
                      ? 'visible'
                      : 'invisible pointer-events-none',
                  )}
                >
                  <SessionEnvVarEditor
                    chatId={chatId}
                    encryptedId={selectedSession.encryptedId}
                  />
                </div>

                {/* Command tab - 保持挂载以保留状态 */}
                <div
                  className={cn(
                    'h-full overflow-auto p-3 absolute inset-0',
                    activeTab === 'command'
                      ? 'visible'
                      : 'invisible pointer-events-none',
                  )}
                >
                  <SessionCommandRunner
                    chatId={chatId}
                    encryptedId={selectedSession.encryptedId}
                    onFinished={(ok) => {
                      if (ok) {
                        setRefreshFilesKey((k) => k + 1);
                        fileManagerRef.current?.refresh();
                      }
                    }}
                  />
                </div>

                {/* Files tab - 保持挂载以保留状态 */}
                <div
                  className={cn(
                    'h-full overflow-auto p-3 absolute inset-0',
                    activeTab === 'files'
                      ? 'visible'
                      : 'invisible pointer-events-none',
                  )}
                >
                  <SessionFileManager
                    ref={fileManagerRef}
                    chatId={chatId}
                    encryptedId={selectedSession.encryptedId}
                    refreshKey={refreshFilesKey}
                    onSelectFile={(entry) => {
                      setSelectedFile(entry);
                      if (!entry || entry.isDirectory) {
                        setActiveFilePath(null);
                      }
                    }}
                    onEditFile={(path) => {
                      setActiveFilePath(path);
                      handleTabChange('editor');
                    }}
                  />
                </div>

                {/* Editor tab */}
                <div
                  className={cn(
                    'h-full overflow-auto p-3 absolute inset-0',
                    activeTab === 'editor'
                      ? 'visible'
                      : 'invisible pointer-events-none',
                  )}
                >
                  {activeFilePath ? (
                    <SessionFileEditor
                      chatId={chatId}
                      encryptedId={selectedSession.encryptedId}
                      path={activeFilePath}
                      onSaved={() => {
                        setRefreshFilesKey((k) => k + 1);
                        fileManagerRef.current?.refresh();
                      }}
                    />
                  ) : (
                    <div className="flex flex-col items-center justify-center h-full text-sm text-muted-foreground">
                      {t('Select a file from File Manager to edit')}
                    </div>
                  )}
                </div>
              </div>

              {/* Bottom tabs */}
              <div className="border-t shrink-0">
                <div className="flex">
                  {tabs.map((tab) => (
                    <button
                      key={tab.id}
                      onClick={() => handleTabChange(tab.id)}
                      disabled={tab.disabled}
                      className={cn(
                        'flex-1 flex flex-col items-center gap-1 py-2.5 px-2 transition-colors',
                        tab.disabled
                          ? 'text-muted-foreground/50 cursor-not-allowed'
                          : 'hover:bg-accent/50',
                        activeTab === tab.id && !tab.disabled
                          ? 'text-primary border-t-2 border-primary bg-accent/30'
                          : '',
                      )}
                    >
                      {tab.icon}
                      <span className="text-xs font-medium">{tab.label}</span>
                    </button>
                  ))}
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </FloatingWindow>
  );
}
