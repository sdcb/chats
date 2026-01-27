import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import FloatingWindow from '@/components/ui/floating-window/FloatingWindow';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { IconPlus } from '@/components/Icons';

import {
  createChatDockerSession,
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
      setSessions((prev) => [...prev, created].sort((a, b) => a.id - b.id));
      setSelectedLabel(created.label);
      setMode('view');
      toast.success(t('Created successful'));
    },
    [chatId, t],
  );

  const showEmpty = !loadingSessions && sessions.length === 0;

  return (
    <FloatingWindow
      open={open}
      onOpenChange={onOpenChange}
      title={t('Session Manager')}
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
                <button
                  key={s.id}
                  className={cn(
                    'shrink-0 h-8 px-3 rounded-md border text-sm',
                    selectedLabel === s.label
                      ? 'bg-accent'
                      : 'bg-background hover:bg-accent/60',
                  )}
                  onClick={() => {
                    setSelectedLabel(s.label);
                    setMode('view');
                    setActiveFilePath(null);
                  }}
                  title={s.image}
                >
                  {s.label}
                </button>
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
            <SessionCommandRunner
              chatId={chatId}
              sessionId={selectedSession.label}
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
              sessionId={selectedSession.label}
              refreshKey={refreshFilesKey}
              onOpenTextFile={(path) => setActiveFilePath(path)}
            />

            {activeFilePath && (
              <SessionFileEditor
                chatId={chatId}
                sessionId={selectedSession.label}
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
