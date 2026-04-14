import { Dispatch, SetStateAction, useCallback, useEffect, useMemo, useRef, useState } from 'react';

export const UNIFIED_TABLE_TEXT_FILTER_DEBOUNCE_MS = 900;

const areStringRecordsEqual = <T extends Record<string, string>>(left: T, right: T) => {
  const leftKeys = Object.keys(left) as Array<keyof T>;
  const rightKeys = Object.keys(right) as Array<keyof T>;

  if (leftKeys.length !== rightKeys.length) {
    return false;
  }

  return leftKeys.every((key) => left[key] === right[key]);
};

const buildStringRecordKey = <T extends Record<string, string>>(value: T) =>
  Object.keys(value)
    .sort()
    .map((key) => `${key}:${value[key]}`)
    .join('|');

type UseTextFilterDraftParams<T extends Record<string, string>> = {
  committed: T;
  onCommit: (draft: T) => void;
  debounceMs?: number;
};

type UseTextFilterDraftResult<T extends Record<string, string>> = {
  draft: T;
  setDraft: Dispatch<SetStateAction<T>>;
  flushDraft: (nextDraft?: T) => void;
  hasPendingDraft: boolean;
};

export const useTextFilterDraft = <T extends Record<string, string>>({
  committed,
  onCommit,
  debounceMs = UNIFIED_TABLE_TEXT_FILTER_DEBOUNCE_MS,
}: UseTextFilterDraftParams<T>): UseTextFilterDraftResult<T> => {
  const [draft, setDraft] = useState<T>(committed);
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const committedRef = useRef(committed);
  const previousCommittedKeyRef = useRef(buildStringRecordKey(committed));
  const committedKey = useMemo(() => buildStringRecordKey(committed), [committed]);
  const draftKey = useMemo(() => buildStringRecordKey(draft), [draft]);

  const clearPendingCommit = useCallback(() => {
    if (!timerRef.current) {
      return;
    }

    clearTimeout(timerRef.current);
    timerRef.current = null;
  }, []);

  useEffect(() => {
    committedRef.current = committed;

    if (previousCommittedKeyRef.current === committedKey) {
      return;
    }

    previousCommittedKeyRef.current = committedKey;
    setDraft((prev) => (areStringRecordsEqual(prev, committed) ? prev : committed));
  }, [committed, committedKey]);

  useEffect(() => {
    clearPendingCommit();

    if (draftKey === committedKey) {
      return;
    }

    timerRef.current = setTimeout(() => {
      onCommit(draft);
    }, debounceMs);

    return clearPendingCommit;
  }, [clearPendingCommit, committedKey, debounceMs, draft, draftKey, onCommit]);

  const flushDraft = useCallback(
    (nextDraft?: T) => {
      const resolvedDraft = nextDraft ?? draft;
      clearPendingCommit();

      setDraft((prev) =>
        areStringRecordsEqual(prev, resolvedDraft) ? prev : resolvedDraft,
      );

      if (areStringRecordsEqual(resolvedDraft, committedRef.current)) {
        return;
      }

      onCommit(resolvedDraft);
    },
    [clearPendingCommit, draft, onCommit],
  );

  const hasPendingDraft = useMemo(() => draftKey !== committedKey, [committedKey, draftKey]);

  return {
    draft,
    setDraft,
    flushDraft,
    hasPendingDraft,
  };
};
