import toast from 'react-hot-toast';

import { WebsiteHostingTestResult } from '@/types/websites';

export const EMPTY_VALUE = '-';

export const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString() : EMPTY_VALUE;

export const showTestResult = (result: WebsiteHostingTestResult) => {
  if (result.success) toast.success(result.message, { duration: 6000 });
  else toast.error(result.message, { duration: 10000 });
};

export const nullIfBlank = (value: string) =>
  value.trim() === '' ? null : value.trim();
