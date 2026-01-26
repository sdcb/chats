import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { IconEdit, IconLoader, IconX } from '@/components/Icons';

import { readDockerTextFile, saveDockerTextFile } from '@/apis/dockerSessionsApi';
import { getApiErrorMessage } from '@/utils/apiError';

type Props = {
  chatId: string;
  sessionId: string;
  path: string;
  onClose: () => void;
  onSaved: () => void;
};

export default function SessionFileEditor({
  chatId,
  sessionId,
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

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setIsText(false);
    setText('');
    setOriginal('');

    readDockerTextFile(chatId, sessionId, path)
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
  }, [chatId, path, sessionId]);

  const dirty = useMemo(() => isText && text !== original, [isText, original, text]);

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

      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        className="w-full min-h-[180px] max-h-[420px] resize-y rounded-md border bg-background px-3 py-2 font-mono text-sm outline-none"
        spellCheck={false}
      />

      <div className="flex justify-end gap-2">
        <Button
          variant="secondary"
          onClick={() => {
            if (dirty) {
              const ok = window.confirm(t('You have unsaved changes. Discard them?'));
              if (!ok) return;
            }
            onClose();
          }}
          disabled={saving}
          className="gap-2"
        >
          <IconX size={16} />
          {t('Close')}
        </Button>

        <Button
          onClick={async () => {
            setSaving(true);
            try {
              await saveDockerTextFile(chatId, sessionId, { path, text });
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
