import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { Skeleton } from '@/components/ui/skeleton';
import { IconCheck, IconClipboard, IconEdit, IconLoader } from '@/components/Icons';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { readDockerTextFile, saveDockerTextFile } from '@/apis/dockerSessionsApi';
import { getApiErrorMessage } from '@/utils/apiError';

type Props = {
  chatId: string;
  encryptedSessionId: string;
  path: string;
  onSaved: () => void;
};

export default function SessionFileEditor({
  chatId,
  encryptedSessionId,
  path,
  onSaved,
}: Props) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [isText, setIsText] = useState<boolean>(false);
  const [text, setText] = useState<string>('');
  const [original, setOriginal] = useState<string>('');
  const [copied, setCopied] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);

  const loadFile = useCallback(() => {
    let cancelled = false;
    setLoading(true);
    setIsText(false);
    setText('');
    setOriginal('');

    readDockerTextFile(chatId, encryptedSessionId, path)
      .then((res) => {
        if (cancelled) return;
        if (!res.isText || res.text == null) {
          setIsText(false);
          return;
        }
        setIsText(true);
        setText(res.text);
        setOriginal(res.text);
      })
      .catch(() => {
        if (cancelled) return;
        setIsText(false);
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [chatId, encryptedSessionId, path]);

  useEffect(() => {
    return loadFile();
  }, [loadFile]);

  const dirty = useMemo(() => isText && text !== original, [isText, original, text]);

  const handleCopy = useCallback(() => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [text]);

  const handleSave = useCallback(async () => {
    setSaving(true);
    try {
      await saveDockerTextFile(chatId, encryptedSessionId, { path, text });
      setSaving(false);
      setOriginal(text);
      onSaved();
    } catch (e: any) {
      toast.error(getApiErrorMessage(e, t('Save failed')));
      setSaving(false);
    }
  }, [chatId, encryptedSessionId, onSaved, path, t, text]);

  if (loading) {
    return (
      <div className="h-full flex flex-col p-4">
        <Skeleton className="h-5 w-48 mb-4" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-4/5" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-3/4" />
        </div>
      </div>
    );
  }

  if (!isText) {
    return (
      <div className="h-full flex flex-col items-center justify-center text-sm text-muted-foreground">
        {t('This file cannot be edited as text')}
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col gap-3">
      {/* 标题栏 */}
      <div className="flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2 text-sm font-medium min-w-0">
          <IconEdit size={16} className="shrink-0" />
          <span className="truncate font-mono text-xs" title={path}>
            {path}
          </span>
          {dirty && (
            <span className="shrink-0 text-xs text-orange-500 font-normal">
              ({t('Modified')})
            </span>
          )}
        </div>
        {/* 复制按钮 */}
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                className="flex items-center rounded p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                onClick={handleCopy}
              >
                {copied ? (
                  <IconCheck stroke="currentColor" size={16} />
                ) : (
                  <IconClipboard stroke="currentColor" size={16} />
                )}
              </button>
            </TooltipTrigger>
            <TooltipContent>
              {copied ? t('Copied') : t('Click Copy')}
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>

      {/* 编辑区域 */}
      <div className="flex-1 min-h-0 relative">
        <textarea
          ref={textareaRef}
          value={text}
          onChange={(e) => setText(e.target.value)}
          className="w-full h-full resize-none rounded-lg border bg-background px-4 py-3 font-mono text-sm outline-none focus:ring-2 focus:ring-primary/20 transition-shadow"
          spellCheck={false}
        />
        {/* 右下角保存按钮 */}
        {(dirty || saving) && (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  className="absolute right-3 bottom-3 flex items-center rounded p-2 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors disabled:opacity-50"
                  onClick={handleSave}
                  disabled={saving}
                >
                  {saving ? (
                    <IconLoader className="animate-spin" stroke="currentColor" size={20} />
                  ) : (
                    <IconCheck stroke="currentColor" size={20} />
                  )}
                </button>
              </TooltipTrigger>
              <TooltipContent>
                {t('Save')}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        )}
      </div>
    </div>
  );
}
