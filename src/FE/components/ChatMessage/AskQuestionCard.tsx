import { useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ToolCallContent, ToolResponseContent } from '@/types/chat';
import { AskQuestionAnswer, AskQuestionItem } from '@/types/chatMessage';

import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';

import { cn } from '@/lib/utils';

export const ASK_QUESTION_TOOL_NAME = 'ask_question';

/** Mirrors AskQuestionTool.TryParse on the server so question ids match. */
export function parseAskQuestions(parameters: string | undefined): AskQuestionItem[] | null {
  let root: any;
  try {
    root = JSON.parse(parameters && parameters.trim() ? parameters : '{}');
  } catch {
    return null;
  }
  let questions = root?.questions;
  if (typeof questions === 'string') {
    try {
      questions = JSON.parse(questions);
    } catch {
      return null;
    }
  }
  if (!Array.isArray(questions) || questions.length === 0 || questions.length > 3) return null;

  const ids = new Set<string>();
  const result: AskQuestionItem[] = [];
  for (let i = 0; i < questions.length; i++) {
    const q = questions[i];
    if (!q || typeof q !== 'object' || Array.isArray(q)) return null;
    const asString = (v: unknown) =>
      v === undefined || v === null ? undefined : typeof v === 'string' ? v : JSON.stringify(v);
    const question = asString(q.question)?.trim();
    if (!question) return null;
    const type = (asString(q.type) ?? 'text').trim().toLowerCase();
    if (type !== 'single' && type !== 'multi' && type !== 'text') return null;
    const options: string[] = Array.isArray(q.options)
      ? Array.from(
          new Set(
            (q.options as unknown[])
              .map((x) => asString(x)?.trim())
              .filter((x): x is string => !!x),
          ),
        )
      : [];
    if (type !== 'text' && options.length === 0) return null;
    let id = asString(q.id)?.trim() ?? '';
    if (id.length === 0 || ids.has(id)) {
      id = `q${i + 1}`;
      while (ids.has(id)) id += '_';
    }
    ids.add(id);
    result.push({
      id,
      question,
      type,
      options: type === 'text' ? [] : options,
      default: asString(q.default) ?? null,
    });
  }
  return result;
}

type AnsweredResult =
  | { status: 'answered'; answers: { id: string; answer: string | string[] | null }[] }
  | { status: 'skipped'; message?: string };

function parseAnswered(response: string | undefined): AnsweredResult | null {
  if (!response) return null;
  try {
    const obj = JSON.parse(response);
    if (obj?.status === 'answered' || obj?.status === 'skipped') return obj as AnsweredResult;
  } catch {
    // not a structured answer
  }
  return null;
}

interface Props {
  toolCall: ToolCallContent;
  toolResponse?: ToolResponseContent;
  /** When provided (and there is no response yet), the card is interactive. */
  onAnswer?: (toolCallId: string, answers: AskQuestionAnswer[], skipped: boolean) => void;
}

const AskQuestionCard = ({ toolCall, toolResponse, onAnswer }: Props) => {
  const { t } = useTranslation();
  const questions = useMemo(() => parseAskQuestions(toolCall.p), [toolCall.p]);
  const answered = useMemo(() => parseAnswered(toolResponse?.r), [toolResponse?.r]);
  const [values, setValues] = useState<Record<string, string[]>>(() => {
    const init: Record<string, string[]> = {};
    (questions ?? []).forEach((q) => {
      if (q.default) {
        if (q.type === 'text') init[q.id] = [q.default];
        else if (q.options.includes(q.default)) init[q.id] = [q.default];
      }
    });
    return init;
  });
  const [submitted, setSubmitted] = useState(false);

  if (!questions) {
    // Still streaming arguments, or invalid arguments (the server answered with an error)
    return (
      <div className="my-2 rounded-md border border-border/60 px-3 py-2 text-sm text-muted-foreground">
        {toolResponse ? toolResponse.r : t('Preparing questions...')}
      </div>
    );
  }

  const interactive = !toolResponse && !!onAnswer && !submitted;
  const answerMap = new Map<string, string[]>();
  if (answered?.status === 'answered') {
    answered.answers.forEach((a) => {
      const v = a.answer;
      answerMap.set(a.id, v === null ? [] : Array.isArray(v) ? v : [v]);
    });
  }

  const toggle = (q: AskQuestionItem, option: string) => {
    setValues((prev) => {
      const current = prev[q.id] ?? [];
      if (q.type === 'single') return { ...prev, [q.id]: [option] };
      return {
        ...prev,
        [q.id]: current.includes(option) ? current.filter((x) => x !== option) : [...current, option],
      };
    });
  };

  const submit = (skipped: boolean) => {
    if (!onAnswer) return;
    setSubmitted(true);
    onAnswer(
      toolCall.u,
      skipped ? [] : questions.map((q) => ({ id: q.id, values: (values[q.id] ?? []).filter((x) => x.trim()) })),
      skipped,
    );
  };

  const hasAnyAnswer = questions.some((q) => (values[q.id] ?? []).some((x) => x.trim()));

  return (
    <div className="my-2 rounded-lg border border-primary/30 bg-muted/30 p-3 text-sm">
      <div className="mb-2 flex items-center justify-between font-medium">
        <span>{t('Questions from AI')}</span>
        {!interactive && (
          <span className="text-xs text-muted-foreground">
            {answered?.status === 'skipped'
              ? t('Skipped')
              : toolResponse
                ? t('Answered')
                : submitted
                  ? t('Submitting...')
                  : t('Waiting for answer')}
          </span>
        )}
      </div>
      <div className="flex flex-col gap-3">
        {questions.map((q, index) => {
          const selected = interactive ? values[q.id] ?? [] : answerMap.get(q.id) ?? [];
          return (
            <div key={q.id} className="flex flex-col gap-1.5">
              <div className="whitespace-pre-wrap">
                {questions.length > 1 && `${index + 1}. `}
                {q.question}
                {q.type === 'multi' && (
                  <span className="ml-1 text-xs text-muted-foreground">({t('Multiple choice')})</span>
                )}
              </div>
              {q.type === 'text' ? (
                interactive ? (
                  <Textarea
                    className="min-h-[60px] bg-background"
                    value={(values[q.id] ?? [''])[0]}
                    placeholder={q.default ?? ''}
                    onChange={(e) => setValues((prev) => ({ ...prev, [q.id]: [e.target.value] }))}
                  />
                ) : (
                  selected.length > 0 && (
                    <div className="whitespace-pre-wrap rounded-md bg-background px-2 py-1">{selected.join('\n')}</div>
                  )
                )
              ) : (
                <div className="flex flex-wrap gap-1.5">
                  {q.options.map((option) => {
                    const isSelected = selected.includes(option);
                    return (
                      <button
                        key={option}
                        type="button"
                        disabled={!interactive}
                        onClick={() => toggle(q, option)}
                        className={cn(
                          'rounded-md border px-2 py-1 text-left transition-colors',
                          isSelected
                            ? 'border-primary bg-primary text-primary-foreground'
                            : 'border-border bg-background',
                          interactive ? 'hover:border-primary' : 'cursor-default',
                          !interactive && !isSelected && 'opacity-60',
                        )}
                      >
                        {option}
                        {q.default === option && interactive && (
                          <span className="ml-1 text-xs opacity-70">({t('Recommended')})</span>
                        )}
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>
      {interactive && (
        <div className="mt-3 flex justify-end gap-2">
          <Button size="sm" variant="outline" onClick={() => submit(true)}>
            {t('Skip')}
          </Button>
          <Button size="sm" disabled={!hasAnyAnswer} onClick={() => submit(false)}>
            {t('Submit')}
          </Button>
        </div>
      )}
    </div>
  );
};

export default AskQuestionCard;
