import { RequestTraceListItem } from '@/types/adminApis';

export type ColumnKey =
  | 'id'
  | 'startedAt'
  | 'requestBodyAt'
  | 'responseHeaderAt'
  | 'responseBodyAt'
  | 'direction'
  | 'method'
  | 'url'
  | 'statusCode'
  | 'durationMs'
  | 'userId'
  | 'traceId'
  | 'userName'
  | 'source'
  | 'requestContentType'
  | 'responseContentType'
  | 'errorType'
  | 'rawRequestBodyBytes'
  | 'rawResponseBodyBytes'
  | 'requestBodyLength'
  | 'responseBodyLength'
  | 'hasPayload'
  | 'hasRequestBodyRaw'
  | 'hasResponseBodyRaw';

export type RequestTraceColumnDef = {
  key: ColumnKey;
  title: string;
};

export const ALL_COLUMNS: RequestTraceColumnDef[] = [
  { key: 'id', title: 'Id' },
  { key: 'traceId', title: 'Trace Id' },
  { key: 'startedAt', title: 'Started At' },
  { key: 'requestBodyAt', title: 'Request Body At' },
  { key: 'responseHeaderAt', title: 'Response Header At' },
  { key: 'responseBodyAt', title: 'Response Body At' },
  { key: 'direction', title: 'Direction' },
  { key: 'method', title: 'Method' },
  { key: 'url', title: 'Url' },
  { key: 'statusCode', title: 'Status Code' },
  { key: 'durationMs', title: 'Duration (ms)' },
  { key: 'userId', title: 'User Id' },
  { key: 'userName', title: 'User Name' },
  { key: 'source', title: 'Source' },
  { key: 'requestContentType', title: 'Request Content Type' },
  { key: 'responseContentType', title: 'Response Content Type' },
  { key: 'errorType', title: 'Error Type' },
  { key: 'rawRequestBodyBytes', title: 'Raw Request Body Bytes' },
  { key: 'rawResponseBodyBytes', title: 'Raw Response Body Bytes' },
  { key: 'requestBodyLength', title: 'Request Body Length' },
  { key: 'responseBodyLength', title: 'Response Body Length' },
  { key: 'hasPayload', title: 'Has Payload' },
  { key: 'hasRequestBodyRaw', title: 'Has Request Body Raw' },
  { key: 'hasResponseBodyRaw', title: 'Has Response Body Raw' },
];

export const DEFAULT_COLUMNS: ColumnKey[] = [
  'traceId',
  'startedAt',
  'direction',
  'method',
  'url',
  'statusCode',
  'userName',
];

export const getDurationMs = (row: RequestTraceListItem): number | null => {
  if (!row.responseHeaderAt) {
    return null;
  }

  const start = Date.parse(row.startedAt);
  const responseHeader = Date.parse(row.responseHeaderAt);
  if (Number.isNaN(start) || Number.isNaN(responseHeader)) {
    return null;
  }

  return Math.max(0, responseHeader - start);
};

export const getDirectionLabel = (value: number, t: (key: string) => string) => {
  if (value === 0) return t('Inbound');
  if (value === 1) return t('Outbound');
  return String(value);
};