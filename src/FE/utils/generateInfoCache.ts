import { IStepGenerateInfo } from '@/types/chatMessage';
import { getSharedTurnGenerateInfo, getTurnGenerateInfo } from '@/apis/clientApis';

export interface GenerateInfoCacheKey {
  turnId: string;
  chatId?: string;
  chatShareId?: string;
}

const resolvedCache = new Map<string, IStepGenerateInfo[]>();
const inFlightCache = new Map<string, Promise<IStepGenerateInfo[]>>();

type CacheListener = (data: IStepGenerateInfo[]) => void;
const listeners = new Map<string, Set<CacheListener>>();

const toCacheKey = ({ turnId, chatId, chatShareId }: GenerateInfoCacheKey): string => {
  if (chatShareId) return `share:${chatShareId}:${turnId}`;
  if (chatId) return `chat:${chatId}:${turnId}`;
  return `turn:${turnId}`;
};

const emitCacheUpdate = (cacheKey: string, data: IStepGenerateInfo[]): void => {
  const subs = listeners.get(cacheKey);
  if (!subs || subs.size === 0) return;

  // Clone payload to avoid external mutation side effects
  const payload = [...data];
  subs.forEach((listener) => {
    try {
      listener(payload);
    } catch (error) {
      console.error('generateInfoCache listener failed:', error);
    }
  });
};

export const getCachedGenerateInfo = (key: GenerateInfoCacheKey): IStepGenerateInfo[] | undefined => {
  return resolvedCache.get(toCacheKey(key));
};

export const fetchGenerateInfoCached = async (
  key: GenerateInfoCacheKey,
  fetcher: () => Promise<IStepGenerateInfo[]>,
): Promise<IStepGenerateInfo[]> => {
  const cacheKey = toCacheKey(key);

  const resolved = resolvedCache.get(cacheKey);
  if (resolved) {
    return resolved;
  }

  const inFlight = inFlightCache.get(cacheKey);
  if (inFlight) {
    return inFlight;
  }

  const promise = fetcher()
    .then((data) => {
      const result = data ?? [];
      resolvedCache.set(cacheKey, result);
      emitCacheUpdate(cacheKey, result);
      inFlightCache.delete(cacheKey);
      return result;
    })
    .catch((error) => {
      inFlightCache.delete(cacheKey);
      throw error;
    });

  inFlightCache.set(cacheKey, promise);
  return promise;
};

export const setGenerateInfoCache = (
  key: GenerateInfoCacheKey,
  data: IStepGenerateInfo[],
): void => {
  const cacheKey = toCacheKey(key);
  const value = data ?? [];
  resolvedCache.set(cacheKey, value);
  emitCacheUpdate(cacheKey, value);
};

export const subscribeGenerateInfoCache = (
  key: GenerateInfoCacheKey,
  listener: CacheListener,
): (() => void) => {
  const cacheKey = toCacheKey(key);
  const existing = listeners.get(cacheKey);
  if (existing) {
    existing.add(listener);
  } else {
    listeners.set(cacheKey, new Set([listener]));
  }

  return () => {
    const subs = listeners.get(cacheKey);
    if (!subs) return;
    subs.delete(listener);
    if (subs.size === 0) {
      listeners.delete(cacheKey);
    }
  };
};

export interface AggregatedGenerateInfo {
  inputTokens: number;
  outputTokens: number;
  inputPrice: number;
  outputPrice: number;
  reasoningTokens: number;
  duration: number;
  reasoningDuration: number;
  firstTokenLatency: number;
}

export const aggregateStepGenerateInfo = (
  stepInfos: IStepGenerateInfo[] | null | undefined,
): AggregatedGenerateInfo | null => {
  if (!stepInfos || stepInfos.length === 0) {
    return null;
  }

  const aggregated = stepInfos.reduce<AggregatedGenerateInfo>(
    (acc, info) => {
      acc.inputTokens += info.inputTokens ?? 0;
      acc.outputTokens += info.outputTokens ?? 0;
      acc.inputPrice += info.inputPrice ?? 0;
      acc.outputPrice += info.outputPrice ?? 0;
      acc.reasoningTokens += info.reasoningTokens ?? 0;
      acc.duration += info.duration ?? 0;
      acc.reasoningDuration += info.reasoningDuration ?? 0;
      return acc;
    },
    {
      inputTokens: 0,
      outputTokens: 0,
      inputPrice: 0,
      outputPrice: 0,
      reasoningTokens: 0,
      duration: 0,
      reasoningDuration: 0,
      firstTokenLatency: 0,
    },
  );

  aggregated.firstTokenLatency = stepInfos[0]?.firstTokenLatency ?? 0;

  return aggregated;
};

// Unified loader entry for components: choose API based on key and apply cache/in-flight de-dupe
export const requestGenerateInfo = async (
  key: GenerateInfoCacheKey,
): Promise<IStepGenerateInfo[]> => {
  return fetchGenerateInfoCached(key, () => {
    const { chatId, chatShareId, turnId } = key;
    if (chatShareId) return getSharedTurnGenerateInfo(chatShareId, turnId);
    if (chatId) return getTurnGenerateInfo(chatId, turnId);
    // Without identifiers we cannot fetch from server; resolve as empty
    return Promise.resolve([]);
  });
};

export const getOrLoadGenerateInfo = async (
  key: GenerateInfoCacheKey,
): Promise<IStepGenerateInfo[]> => {
  const cached = getCachedGenerateInfo(key);
  if (cached) return cached;
  return requestGenerateInfo(key);
};
