import { NextRouter } from 'next/router';

export const isMobile = () => {
  const userAgent =
    typeof window.navigator === 'undefined' ? '' : navigator.userAgent;
  const mobileRegex =
    /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini|Mobile|mobile|CriOS/i;
  return mobileRegex.test(userAgent);
};

export function formatNumberAsMoney(amount: number, maximumFractionDigits = 5) {
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits }).format(
    amount,
  );
}

export function termDateString() {
  return new Date(
    new Date().getTime() + 10 * 365 * 24 * 60 * 60 * 1000,
  ).toISOString();
}

export const PhoneRegExp = /^[1][3,4,5,6,7,8,9][0-9]{9}$/;
export const SmsExpirationSeconds = 300;

export const getApiUrl = () => process.env.API_URL || '';

export const getQueryId = (router: NextRouter): string => {
  const { id } = router.query;
  if (id) {
    if (Array.isArray(id)) {
      return id[0];
    } else {
      return id;
    }
  }
  const asPath = router.asPath.split('?')[0];
  const pathSegments = asPath.split('/');
  return pathSegments[pathSegments.length - 1] || '';
};

export function getNextName(existingNames: string[], baseName: string): string {
  const regex = new RegExp(`^${baseName}(?: (\\d+))?$`);

  let maxIndex = 0;

  for (const name of existingNames) {
    const match = name.match(regex);
    if (match) {
      const currentIndex = match[1] ? parseInt(match[1], 10) : 0;
      if (currentIndex > maxIndex) {
        maxIndex = currentIndex;
      }
    }
  }
  return `${baseName} ${maxIndex + 1}`;
}

export function toFixed(value: number, precision: number = 2) {
  return value ? value.toFixed(precision) : '0';
}

/**
 * 检测系统是否为深色主题
 */
export function isSystemDark(): boolean {
  if (typeof window === 'undefined') return false;
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

/**
 * 解析主题值，将 system 转换为实际的 dark/light
 */
export function resolveTheme(theme?: string): 'dark' | 'light' {
  if (theme === 'system') {
    return isSystemDark() ? 'dark' : 'light';
  }
  return theme === 'dark' ? 'dark' : 'light';
}

/**
 * 遮蔽API密钥，显示前缀和后缀，隐藏中间部分
 * @param key API密钥
 * @param prefixLength 前缀长度，默认6
 * @param suffixLength 后缀长度，默认4
 * @returns 遮蔽后的密钥，例如 "sk-abc1***xyz9"
 */
export function maskApiKey(
  key: string,
  prefixLength: number = 6,
  suffixLength: number = 4,
): string {
  if (!key) return '';
  if (key.length <= prefixLength + suffixLength) {
    return key;
  }
  const prefix = key.slice(0, prefixLength);
  const suffix = key.slice(-suffixLength);
  return `${prefix}***${suffix}`;
}
