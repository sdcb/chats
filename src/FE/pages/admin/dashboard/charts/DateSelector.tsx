import React from 'react';
import { useRouter } from 'next/router';
import useTranslation from '@/hooks/useTranslation';
import { formatDate } from '@/utils/date';
import { Card } from '@/components/ui/card';
import DateTimePopover from '@/pages/home/_components/Popover/DateTimePopover';

interface DateSelectorProps {
  startDate: string;
  endDate: string;
  setStartDate: (date: string) => void;
  setEndDate: (date: string) => void;
}

export default function DateSelector({
  startDate,
  endDate,
  setStartDate,
  setEndDate,
}: DateSelectorProps) {
  const { t } = useTranslation();
  const router = useRouter();

  return (
    <Card className="p-4 mb-4 border-none">
      <div className="flex flex-col sm:flex-row gap-4 items-end">
        <div className="flex items-center gap-2">
          <DateTimePopover
            className="w-48"
            placeholder={t('Start date')}
            value={startDate}
            onSelect={(date: Date) => {
              const formattedDate = formatDate(date.toLocaleDateString());
              setStartDate(formattedDate);
              const query: Record<string, string> = {
                ...(router.query as Record<string, string>),
                start: formattedDate,
              };
              router.push(
                {
                  pathname: router.pathname,
                  query,
                },
                undefined,
                { shallow: true },
              );
            }}
            onReset={() => {
              setStartDate('');
              const query: Record<string, string> = {
                ...(router.query as Record<string, string>),
              };
              delete query.start;
              router.push(
                {
                  pathname: router.pathname,
                  query,
                },
                undefined,
                { shallow: true },
              );
            }}
          />
        </div>

        <div className="flex items-center gap-2">
          <DateTimePopover
            className="w-48"
            placeholder={t('End date')}
            value={endDate}
            onSelect={(date: Date) => {
              const formattedDate = formatDate(date.toLocaleDateString());
              setEndDate(formattedDate);
              const query: Record<string, string> = {
                ...(router.query as Record<string, string>),
                end: formattedDate,
              };
              router.push(
                {
                  pathname: router.pathname,
                  query,
                },
                undefined,
                { shallow: true },
              );
            }}
            onReset={() => {
              setEndDate('');
              const query: Record<string, string> = {
                ...(router.query as Record<string, string>),
              };
              delete query.end;
              router.push(
                {
                  pathname: router.pathname,
                  query,
                },
                undefined,
                { shallow: true },
              );
            }}
          />
        </div>
      </div>
    </Card>
  );
} 