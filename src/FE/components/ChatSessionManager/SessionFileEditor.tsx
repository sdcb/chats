import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { IconCheck, IconClipboard, IconEdit, IconLoader, IconX } from '@/components/Icons';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { readDockerTextFile, saveDockerTextFile } from '@/apis/dockerSessionsApi';
import { getApiErrorMessage } from '@/utils/apiError';

const TEXTAREA_MIN_HEIGHT = 80;
const TEXTAREA_MAX_HEIGHT = 320;

type Props = {
  chatId: string;
  encryptedSessionId: string;
  path: string;
  onClose: () => void;
  onSaved: () => void;
};

export default function SessionFileEditor({
  chatId,
  encryptedSessionId,
  path,
  onClose,
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

  const resize = useCallback(() => {
    if (!textareaRef.current) return;
    const el = textareaRef.current;
    el.style.height = 'auto';
    const height = Math.min(
      Math.max(el.scrollHeight, TEXTAREA_MIN_HEIGHT),
      TEXTAREA_MAX_HEIGHT,
    );
    el.style.height = `${height}px`;
    el.style.overflow = el.scrollHeight > TEXTAREA_MAX_HEIGHT ? 'auto' : 'hidden';
  }, []);

  useEffect(() => {
    resize();
  }, [text, resize]);

  useEffect(() => {
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
  }, [chatId, path, encryptedSessionId]);

  const dirty = useMemo(() => isText && text !== original, [isText, original, text]);

  const handleCopy = useCallback(() => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [text]);

  if (loading) {
    return (
      <div className="border rounded-md p-4">
        <Skeleton className="h-4 w-40" />
        <div className="mt-3 space-y-2">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-4/5" />
        </div>
      </div>
    );
  }

  if (!isText) {
    return null;
  }

  return (
      <div className="border rounded-md p-4 flex flex-col gap-3">
      <div className="flex items-center gap-2 text-sm font-medium">
        <IconEdit size={16} />
        {t('File editor')}: <span className="font-mono text-xs">{path}</span>
      </div>

      <div className="relative group">
        <textarea
          ref={textareaRef}
          value={text}
          onChange={(e) => setText(e.target.value)}
          className="w-full resize-none rounded-md border bg-background px-3 py-2 font-mono text-sm outline-none"
          style={{ height: TEXTAREA_MIN_HEIGHT, overflow: 'hidden' }}
          spellCheck={false}
        />
        <div className="absolute bottom-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  className="flex items-center rounded p-1 text-muted-foreground hover:text-foreground hover:bg-muted"
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
      </div>

      <div className="flex justify-end gap-2">
        <Button
          variant="secondary"
          onClick={onClose}
          disabled={saving}
          className="gap-2"
        >
          <IconX size={16} />
          {t('Cancel')}
        </Button>

        <Button
          onClick={async () => {
            setSaving(true);
            try {
              await saveDockerTextFile(chatId, encryptedSessionId, { path, text });
              toast.success(t('Save successful'));
              setOriginal(text);
              onSaved();
            } catch (e: any) {
              toast.error(getApiErrorMessage(e, t('Save failed')));
            } finally {
              setSaving(false);
            }
          }}
          disabled={saving || !dirty}
          className="gap-2"
        >
          {saving ? (
            <IconLoader className="animate-spin" size={16} />
          ) : (
            t('Save')
          )}
        </Button>
      </div>
    </div>
  );
}
