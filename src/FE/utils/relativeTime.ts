export type RelativeTimeTranslate = (
  key: string,
  params?: Record<string, string | number>,
) => string;

export function formatAbsoluteTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString();
}

export function formatRelativeWithinHour(
  value: string,
  now: Date,
  t: RelativeTimeTranslate,
): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';

  const diffMs = date.getTime() - now.getTime();
  const absMs = Math.abs(diffMs);
  if (absMs > 60 * 60 * 1000) {
    return formatAbsoluteTime(value);
  }

  const mins = Math.floor(absMs / (60 * 1000));
  if (mins < 1) {
    return t('<1 minute');
  }

  if (diffMs >= 0) {
    return t('In {{count}} minutes', { count: mins });
  }

  return t('{{count}} minutes ago', { count: mins });
}
