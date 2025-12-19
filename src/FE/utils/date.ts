import { format, isThisYear, isToday } from 'date-fns';

export const currentISODateString = () => new Date().toISOString();

Date.prototype.addYear = function (years: number, date?: Date | string): Date {
  const newDate = new Date(date || this);
  newDate.setFullYear(newDate.getFullYear() + years);
  return newDate;
};

export const formatDate = (value: string | Date) =>
  value ? format(new Date(value), 'yyyy-MM-dd') : value;

export const formatDateTime = (value: string | Date) =>
  value ? format(new Date(value), 'yyyy-M-d HH:mm:ss') : value;

export const getTz = () => new Date().getTimezoneOffset();

export const formatRelativeTime = (value: string | Date) => {
  if (isToday(value)) return format(value, 'HH:mm:ss');
  if (isThisYear(value)) return format(value, 'MM-dd HH:mm:ss');
  return format(value, 'yyyy-MM-dd HH:mm:ss');
};
