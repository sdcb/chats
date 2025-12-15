import { IStepGenerateInfo } from '@/types/chatMessage';
import { getSharedTurnGenerateInfo, getTurnGenerateInfo, getStepGenerateInfo, getSharedStepGenerateInfo } from '@/apis/clientApis';

export interface GenerateInfoCacheKey {
  turnId: string;
  chatId?: string;
  chatShareId?: string;
}

export interface StepGenerateInfoCacheKey {
  stepId: string;
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
  inputOverallTokens: number;
  inputFreshTokens: number;
  inputCachedTokens: number;
  outputTokens: number;
  inputPrice: number;
  inputFreshPrice: number;
  inputCachedPrice: number;
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
      const cachedTokens = info.inputCachedTokens ?? 0;
      const overallTokens =
        info.inputOverallTokens ?? cachedTokens;
      const freshTokens = Math.max(0, overallTokens - cachedTokens);
      const cachedPrice = info.inputCachedPrice ?? 0;
      const freshPrice =
        info.inputFreshPrice ?? Math.max(0, (info.inputPrice ?? 0) - cachedPrice);

      acc.inputFreshTokens += freshTokens;
      acc.inputCachedTokens += cachedTokens;
      acc.inputOverallTokens += overallTokens;
      acc.outputTokens += info.outputTokens ?? 0;

      acc.inputFreshPrice += freshPrice;
      acc.inputCachedPrice += cachedPrice;
      acc.inputPrice += info.inputPrice ?? freshPrice + cachedPrice;
      acc.outputPrice += info.outputPrice ?? 0;

      acc.reasoningTokens += info.reasoningTokens ?? 0;
      acc.duration += info.duration ?? 0;
      acc.reasoningDuration += info.reasoningDuration ?? 0;
      return acc;
    },
    {
      inputOverallTokens: 0,
      inputFreshTokens: 0,
      inputCachedTokens: 0,
      outputTokens: 0,
      inputPrice: 0,
      inputFreshPrice: 0,
      inputCachedPrice: 0,
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

// Step-level cache
const stepResolvedCache = new Map<string, IStepGenerateInfo | null>();
const stepInFlightCache = new Map<string, Promise<IStepGenerateInfo | null>>();

type StepCacheListener = (data: IStepGenerateInfo | null) => void;
const stepListeners = new Map<string, Set<StepCacheListener>>();

const toStepCacheKey = ({ stepId, chatId, chatShareId }: StepGenerateInfoCacheKey): string => {
  if (chatShareId) return `share:${chatShareId}:step:${stepId}`;
  if (chatId) return `chat:${chatId}:step:${stepId}`;
  return `step:${stepId}`;
};

const emitStepCacheUpdate = (cacheKey: string, data: IStepGenerateInfo | null): void => {
  const subs = stepListeners.get(cacheKey);
  if (!subs || subs.size === 0) return;

  subs.forEach((listener) => {
    try {
      listener(data);
    } catch (error) {
      console.error('stepGenerateInfoCache listener failed:', error);
    }
  });
};

export const getCachedStepGenerateInfo = (key: StepGenerateInfoCacheKey): IStepGenerateInfo | null | undefined => {
  const cacheKey = toStepCacheKey(key);
  if (stepResolvedCache.has(cacheKey)) {
    return stepResolvedCache.get(cacheKey);
  }
  return undefined;
};

export const fetchStepGenerateInfoCached = async (
  key: StepGenerateInfoCacheKey,
  fetcher: () => Promise<IStepGenerateInfo | null>,
): Promise<IStepGenerateInfo | null> => {
  const cacheKey = toStepCacheKey(key);

  if (stepResolvedCache.has(cacheKey)) {
    return stepResolvedCache.get(cacheKey) ?? null;
  }

  const inFlight = stepInFlightCache.get(cacheKey);
  if (inFlight) {
    return inFlight;
  }

  const promise = fetcher()
    .then((data) => {
      stepResolvedCache.set(cacheKey, data);
      emitStepCacheUpdate(cacheKey, data);
      stepInFlightCache.delete(cacheKey);
      return data;
    })
    .catch((error) => {
      stepInFlightCache.delete(cacheKey);
      throw error;
    });

  stepInFlightCache.set(cacheKey, promise);
  return promise;
};

export const setStepGenerateInfoCache = (
  key: StepGenerateInfoCacheKey,
  data: IStepGenerateInfo | null,
): void => {
  const cacheKey = toStepCacheKey(key);
  stepResolvedCache.set(cacheKey, data);
  emitStepCacheUpdate(cacheKey, data);
};

export const subscribeStepGenerateInfoCache = (
  key: StepGenerateInfoCacheKey,
  listener: StepCacheListener,
): (() => void) => {
  const cacheKey = toStepCacheKey(key);
  const existing = stepListeners.get(cacheKey);
  if (existing) {
    existing.add(listener);
  } else {
    stepListeners.set(cacheKey, new Set([listener]));
  }

  return () => {
    const subs = stepListeners.get(cacheKey);
    if (!subs) return;
    subs.delete(listener);
    if (subs.size === 0) {
      stepListeners.delete(cacheKey);
    }
  };
};

export const requestStepGenerateInfo = async (
  key: StepGenerateInfoCacheKey,
): Promise<IStepGenerateInfo | null> => {
  return fetchStepGenerateInfoCached(key, () => {
    const { chatId, chatShareId, stepId } = key;
    if (chatShareId) return getSharedStepGenerateInfo(chatShareId, stepId);
    if (chatId) return getStepGenerateInfo(chatId, stepId);
    return Promise.resolve(null);
  });
};

export const getOrLoadStepGenerateInfo = async (
  key: StepGenerateInfoCacheKey,
): Promise<IStepGenerateInfo | null> => {
  const cached = getCachedStepGenerateInfo(key);
  if (cached !== undefined) return cached;
  return requestStepGenerateInfo(key);
};
