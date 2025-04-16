import { format } from 'date-fns';

export const currentISODateString = () => new Date().toISOString();

Date.prototype.addYear = function (years: number, date?: Date | string): Date {
  const newDate = new Date(date || this);
  newDate.setFullYear(newDate.getFullYear() + years);
  return newDate;
};

export const formatDate = (value: string) =>
  value ? format(new Date(value), 'yyyy-M-d') : value;

export const formatDateTime = (value: string) =>
  value ? format(new Date(value), 'yyyy-M-d HH:mm:ss') : value;
