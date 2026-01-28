import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { IconBolt, IconCheck, IconClipboard, IconLoader } from '@/components/Icons';
import { useSendKeyHandler } from '@/components/ui/send-button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { streamRunDockerCommand } from '@/apis/dockerSessionsApi';
import { CommandStreamLine } from '@/types/dockerSessions';

type Props = {
  chatId: string;
  encryptedSessionId: string;
  onFinished?: (ok: boolean) => void;
};

const TEXTAREA_MIN_HEIGHT = 40;
const TEXTAREA_MAX_HEIGHT = 40 * 5;

type OutputLine =
  | { t: 'stdout'; v: string }
  | { t: 'stderr'; v: string }
  | { t: 'exit'; v: { exitCode: number; executionTimeMs: number } }
  | { t: 'error'; v: string };

export default function SessionCommandRunner({
  chatId,
  encryptedSessionId,
  onFinished,
}: Props) {
  const { t } = useTranslation();
  const [command, setCommand] = useState('');
  const [running, setRunning] = useState(false);
  const [output, setOutput] = useState<OutputLine[]>([]);
  const [copied, setCopied] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);

  const canRun = command.trim().length > 0 && !running;

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
  }, [command, resize]);

  const run = useCallback(async () => {
    if (!canRun) return;
    const cmd = command.trim();
    setRunning(true);
    setOutput([]);

    let ok = true;
    try {
      for await (const line of streamRunDockerCommand(chatId, encryptedSessionId, {
        command: cmd,
      })) {
        setOutput((prev) => appendOutput(prev, line));
        if (line.kind === 'error') {
          ok = false;
        }
      }
    } catch (e: any) {
      ok = false;
      toast.error(e?.message || t('Run failed'));
      setOutput((prev) => [
        ...prev,
        { t: 'error', v: e?.message || t('Run failed') },
      ]);
    } finally {
      setRunning(false);
      setCommand('');
      if (textareaRef.current) {
        textareaRef.current.style.height = `${TEXTAREA_MIN_HEIGHT}px`;
        textareaRef.current.style.overflow = 'hidden';
        textareaRef.current.focus();
      }
      onFinished?.(ok);
    }
  }, [canRun, chatId, command, onFinished, encryptedSessionId]);

  const { handleKeyDown } = useSendKeyHandler(run, false, !canRun);

  const outputText = useMemo(() => {
    return output
      .map((l) => {
        if (l.t === 'stdout' || l.t === 'stderr' || l.t === 'error') return l.v;
        return `ExitCode: ${l.v.exitCode} ExecutionTimeMs: ${l.v.executionTimeMs}`;
      })
      .join('\n');
  }, [output]);

  const handleCopy = useCallback(() => {
    if (!outputText) return;
    navigator.clipboard.writeText(outputText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [outputText]);

  const outputView = useMemo(() => {
    if (output.length === 0) {
      return (
        <div className="text-white/70 text-sm">
          {t('No shell output. Enter a command to see results.')}
        </div>
      );
    }

    return (
      <div className="space-y-1 text-sm font-mono whitespace-pre-wrap break-words">
        {output.map((l, idx) => {
          if (l.t === 'stdout') {
            return (
              <div key={idx} className="text-white">
                {l.v}
              </div>
            );
          }
          if (l.t === 'stderr') {
            return (
              <div key={idx} className="text-red-400">
                {l.v}
              </div>
            );
          }
          if (l.t === 'error') {
            return (
              <div key={idx} className="text-red-300">
                {l.v}
              </div>
            );
          }
          return (
            <div key={idx} className="text-green-400">
              ExitCode: <span className="font-bold">{l.v.exitCode}</span>{' '}
              ExecutionTimeMs:{' '}
              <span className="font-bold">{l.v.executionTimeMs}</span>
            </div>
          );
        })}
      </div>
    );
  }, [output]);

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2 text-sm font-medium">
        <IconBolt size={16} /> {t('Run command')}
      </div>

      <div className="relative">
        <textarea
          ref={textareaRef}
          value={command}
          onChange={(e) => setCommand(e.target.value)}
          onKeyDown={handleKeyDown}
          className={cn(
            'w-full resize-none rounded-md border bg-background px-3 py-2 pr-20 leading-6 outline-none',
            'text-sm',
          )}
          style={{ height: TEXTAREA_MIN_HEIGHT }}
          placeholder={t('Enter a shell command...')}
        />
        <div className="absolute right-2 bottom-2">
          <Button
            size="sm"
            variant="secondary"
            onClick={run}
            disabled={!canRun}
            className="gap-2"
          >
            {running ? (
              <IconLoader className="animate-spin" size={16} />
            ) : (
              <IconBolt size={16} />
            )}
            {t('Run')}
          </Button>
        </div>
      </div>

      <div className="relative rounded-md border bg-black min-h-[120px] max-h-[280px] group">
        <div className="p-3 min-h-[120px] max-h-[280px] overflow-auto">
          {outputView}
        </div>
        {output.length > 0 && (
          <div className="absolute bottom-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    className="flex items-center rounded bg-none p-1 text-muted-foreground hover:text-white"
                    onClick={handleCopy}
                  >
                    {copied ? (
                      <IconCheck stroke="currentColor" size={20} />
                    ) : (
                      <IconClipboard stroke="currentColor" size={20} />
                    )}
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  {copied ? t('Copied') : t('Click Copy')}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
        )}
      </div>
    </div>
  );
}

function appendOutput(prev: OutputLine[], line: CommandStreamLine): OutputLine[] {
  if (line.kind === 'stdout') return [...prev, { t: 'stdout', v: line.data }];
  if (line.kind === 'stderr') return [...prev, { t: 'stderr', v: line.data }];
  if (line.kind === 'exit') {
    return [...prev, { t: 'exit', v: { exitCode: line.exitCode, executionTimeMs: line.executionTimeMs } }];
  }
  return [...prev, { t: 'error', v: line.message }];
}
