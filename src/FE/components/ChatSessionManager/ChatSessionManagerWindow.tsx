import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import FloatingWindow from '@/components/ui/floating-window/FloatingWindow';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { IconCheck, IconDocker, IconLoader, IconPlus, IconTrash, IconX } from '@/components/Icons';

import {
  createChatDockerSession,
  deleteChatDockerSession,
  getChatDockerSessions,
  getDockerCpuLimits,
  getDockerDefaultImage,
  getDockerImages,
  getDockerMemoryLimits,
  getDockerNetworkModes,
} from '@/apis/dockerSessionsApi';

import {
  CreateDockerSessionRequest,
  DockerSessionDto,
  ImageListResponse,
  MemoryLimitResponse,
  NetworkModesResponse,
  ResourceLimitResponse,
} from '@/types/dockerSessions';

import CreateSessionPane from './CreateSessionPane';
import SessionCommandRunner from './SessionCommandRunner';
import SessionFileManager, { FileManagerHandle } from './SessionFileManager';
import SessionFileEditor from './SessionFileEditor';
import SessionInfoCard from './SessionInfoCard';
import { cn } from '@/lib/utils';

type Mode = 'view' | 'create';

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
  const [sessions, setSessions] = useState<DockerSessionDto[]>([]);
  const [selectedLabel, setSelectedLabel] = useState<string | null>(null);
  const [mode, setMode] = useState<Mode>('view');
  const [deletingLabel, setDeletingLabel] = useState<string | null>(null);
  const [confirmDeleteLabel, setConfirmDeleteLabel] = useState<string | null>(null);

  const [defaultImage, setDefaultImage] = useState<string>('');
  const [images, setImages] = useState<ImageListResponse>({ images: [] });
  const [cpuLimits, setCpuLimits] = useState<ResourceLimitResponse | null>(null);
  const [memoryLimits, setMemoryLimits] = useState<MemoryLimitResponse | null>(
    null,
  );
  const [networkModes, setNetworkModes] = useState<NetworkModesResponse | null>(
    null,
  );
  const [createDefaultsLoaded, setCreateDefaultsLoaded] = useState(false);

  const [activeFilePath, setActiveFilePath] = useState<string | null>(null);
  const [refreshFilesKey, setRefreshFilesKey] = useState(0);
  const fileManagerRef = useRef<FileManagerHandle | null>(null);

  const selectedSession = useMemo(
    () => sessions.find((s) => s.label === selectedLabel) ?? null,
    [selectedLabel, sessions],
  );

  const loadSessions = useCallback(async () => {
    setLoadingSessions(true);
    try {
      const s = await getChatDockerSessions(chatId);
      setSessions(s);
      setSelectedLabel((prev) => {
        if (prev && s.some((x) => x.label === prev)) return prev;
        return s.length > 0 ? s[0].label : null;
      });
    } finally {
      setLoadingSessions(false);
    }
  }, [chatId]);

  const loadCreateDefaults = useCallback(async () => {
    const [img, list, cpu, mem, net] = await Promise.all([
      getDockerDefaultImage(),
      getDockerImages(),
      getDockerCpuLimits(),
      getDockerMemoryLimits(),
      getDockerNetworkModes(),
    ]);
    setDefaultImage(img.defaultImage);
    setImages(list);
    setCpuLimits(cpu);
    setMemoryLimits(mem);
    setNetworkModes(net);
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
    setCreateDefaultsLoaded(false);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    if (mode !== 'create') return;
    if (createDefaultsLoaded) return;
    loadCreateDefaults().catch(() => null);
  }, [createDefaultsLoaded, loadCreateDefaults, mode, open]);

  const handleCreate = useCallback(
    async (req: CreateDockerSessionRequest) => {
      const created = await createChatDockerSession(chatId, req);
      setSessions((prev) => [...prev, created]);
      setSelectedLabel(created.label);
      setMode('view');
      toast.success(t('Created successful'));
    },
    [chatId, t],
  );

  const handleDelete = useCallback(
    async (label: string) => {
      const session = sessions.find((s) => s.label === label);
      if (!session) return;
      setDeletingLabel(label);
      try {
        await deleteChatDockerSession(chatId, session.encryptedSessionId);
        // 请求成功，直接更新前端列表
        setSessions((prev) => prev.filter((s) => s.label !== label));
        if (selectedLabel === label) {
          setSelectedLabel((prev) => {
            const remaining = sessions.filter((s) => s.label !== label);
            return remaining.length > 0 ? remaining[0].label : null;
          });
        }
        toast.success(t('Deleted successful'));
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
      <div className="p-3 flex flex-col gap-3">
        <div className="flex items-center gap-2 overflow-x-auto border-b pb-2">
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
                  key={s.encryptedSessionId}
                  className={cn(
                    'shrink-0 h-8 rounded-md border text-sm flex items-center',
                    selectedLabel === s.label
                      ? 'bg-accent'
                      : 'bg-background hover:bg-accent/60',
                  )}
                >
                  {confirmDeleteLabel === s.label ? (
                    <div className="flex items-center px-2 gap-1">
                      <span className="text-xs mr-1">{s.label}</span>
                      <button
                        className="p-1 hover:bg-accent rounded"
                        onClick={() => handleDelete(s.label)}
                        disabled={deletingLabel === s.label}
                        title={t('Confirm')}
                      >
                        {deletingLabel === s.label ? (
                          <IconLoader size={14} className="animate-spin" />
                        ) : (
                          <IconCheck size={14} />
                        )}
                      </button>
                      <button
                        className="p-1 hover:bg-accent rounded"
                        onClick={() => setConfirmDeleteLabel(null)}
                        disabled={deletingLabel === s.label}
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
                          setSelectedLabel(s.label);
                          setMode('view');
                          setActiveFilePath(null);
                        }}
                        title={s.image}
                      >
                        {s.label}
                      </button>
                      <button
                        className="pr-2 pl-1 h-full hover:text-destructive"
                        onClick={(e) => {
                          e.stopPropagation();
                          setConfirmDeleteLabel(s.label);
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

        {mode === 'create' ? (
          <CreateSessionPane
            defaultImage={defaultImage}
            images={images.images}
            cpuLimits={cpuLimits}
            memoryLimits={memoryLimits}
            networkModes={networkModes}
            onCancel={() => {
              setMode('view');
              setSelectedLabel((prev) => prev ?? sessions[0]?.label ?? null);
            }}
            onCreate={handleCreate}
          />
        ) : showEmpty ? (
          <div className="flex flex-col items-center justify-center py-20 text-sm text-muted-foreground">
            {t('No Docker sessions. Click + to create one.')}
          </div>
        ) : selectedSession ? (
          <div className="flex flex-col gap-4">
            <SessionInfoCard session={selectedSession} />

            <SessionCommandRunner
              chatId={chatId}
              encryptedSessionId={selectedSession.encryptedSessionId}
              onFinished={(ok) => {
                if (ok) {
                  setRefreshFilesKey((k) => k + 1);
                  fileManagerRef.current?.refresh();
                }
              }}
            />

            <SessionFileManager
              ref={fileManagerRef}
              chatId={chatId}
              encryptedSessionId={selectedSession.encryptedSessionId}
              refreshKey={refreshFilesKey}
              onOpenTextFile={(path) => setActiveFilePath(path)}
            />

            {activeFilePath && (
              <SessionFileEditor
                chatId={chatId}
                encryptedSessionId={selectedSession.encryptedSessionId}
                path={activeFilePath}
                onClose={() => setActiveFilePath(null)}
                onSaved={() => {
                  setRefreshFilesKey((k) => k + 1);
                  fileManagerRef.current?.refresh();
                }}
              />
            )}
          </div>
        ) : null}
      </div>
    </FloatingWindow>
  );
}
