import { GetUserChatGroupWithMessagesResult } from '@/types/clientApis';
import { getUserInfo } from '@/utils/user';

const CHAT_CACHE_KEY_PREFIX = 'chat_groups_cache';
const CACHE_EXPIRY_TIME = 24 * 60 * 60 * 1000; // 24小时过期

interface CachedChatData {
  data: GetUserChatGroupWithMessagesResult[];
  timestamp: number;
  query: string;
  username: string; // 添加用户名字段，确保缓存与用户绑定
}

/**
 * 获取当前用户的缓存键
 * @returns 包含用户名的缓存键，如果未登录则返回 null
 */
const getCacheKey = (): string | null => {
  const userInfo = getUserInfo();
  if (!userInfo?.username) return null;
  return `${CHAT_CACHE_KEY_PREFIX}_${userInfo.username}`;
};

/**
 * 从 localStorage 读取聊天列表缓存
 * @param query 搜索关键词
 * @returns 缓存的聊天数据，如果不存在或已过期则返回 null
 */
export const getChatCache = (query: string = ''): GetUserChatGroupWithMessagesResult[] | null => {
  if (typeof localStorage === 'undefined') return null;
  
  const cacheKey = getCacheKey();
  if (!cacheKey) return null; // 未登录，不使用缓存
  
  try {
    const cacheJson = localStorage.getItem(cacheKey);
    if (!cacheJson) return null;

    const cache: CachedChatData = JSON.parse(cacheJson);
    
    // 验证缓存是否属于当前用户
    const currentUser = getUserInfo();
    if (cache.username !== currentUser?.username) {
      // 缓存不属于当前用户，清除并返回 null
      localStorage.removeItem(cacheKey);
      return null;
    }
    
    // 检查缓存是否过期
    const now = Date.now();
    if (now - cache.timestamp > CACHE_EXPIRY_TIME) {
      // 缓存已过期，清除
      localStorage.removeItem(cacheKey);
      return null;
    }

    // 检查查询条件是否匹配（目前主要缓存空查询的情况）
    if (cache.query !== query) {
      return null;
    }

    return cache.data;
  } catch (error) {
    console.error('Failed to read chat cache:', error);
    return null;
  }
};

/**
 * 将聊天列表数据保存到 localStorage
 * @param data 聊天组数据
 * @param query 搜索关键词
 */
export const setChatCache = (
  data: GetUserChatGroupWithMessagesResult[],
  query: string = ''
): void => {
  if (typeof localStorage === 'undefined') return;
  
  const cacheKey = getCacheKey();
  if (!cacheKey) return; // 未登录，不缓存
  
  const currentUser = getUserInfo();
  if (!currentUser?.username) return;
  
  try {
    const cache: CachedChatData = {
      data,
      timestamp: Date.now(),
      query,
      username: currentUser.username, // 记录用户名
    };
    localStorage.setItem(cacheKey, JSON.stringify(cache));
  } catch (error) {
    console.error('Failed to save chat cache:', error);
    // 如果存储失败（比如超出配额），尝试清除缓存后重试
    try {
      localStorage.removeItem(cacheKey);
      const cache: CachedChatData = {
        data,
        timestamp: Date.now(),
        query,
        username: currentUser.username,
      };
      localStorage.setItem(cacheKey, JSON.stringify(cache));
    } catch (retryError) {
      console.error('Failed to save chat cache even after clearing:', retryError);
    }
  }
};

/**
 * 清除聊天列表缓存
 */
export const clearChatCache = (): void => {
  if (typeof localStorage === 'undefined') return;
  
  const cacheKey = getCacheKey();
  if (!cacheKey) return;
  
  localStorage.removeItem(cacheKey);
};
