import React from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatDate } from '@/utils/date';

import DateTimePopover from '@/pages/home/_components/Popover/DateTimePopover';

import { Card } from '@/components/ui/card';

interface DateSelectorProps {
  startDate: string;
  endDate: string;
  setStartDate: (date: string) => void;
  setEndDate: (date: string) => void;
  onDateChange?: (startDate: string, endDate: string) => void;
}

export default function DateSelector({
  startDate,
  endDate,
  setStartDate,
  setEndDate,
  onDateChange,
}: DateSelectorProps) {
  const { t } = useTranslation();

  const handleStartDateChange = (date: Date | null) => {
    const formattedDate = date ? formatDate(date.toLocaleDateString()) : '';
    setStartDate(formattedDate);

    if (onDateChange) {
      onDateChange(formattedDate, endDate);
    }
  };

  const handleEndDateChange = (date: Date | null) => {
    const formattedDate = date ? formatDate(date.toLocaleDateString()) : '';
    setEndDate(formattedDate);

    if (onDateChange) {
      onDateChange(startDate, formattedDate);
    }
  };

  return (
    <div className="flex flex-col mb-4 sm:flex-row gap-4">
      <div className="flex items-center gap-2">
        <DateTimePopover
          className="w-48"
          placeholder={t('Start date')}
          value={startDate}
          onSelect={(date: Date) => handleStartDateChange(date)}
          onReset={() => handleStartDateChange(null)}
        />
      </div>

      <div className="flex items-center gap-2">
        <DateTimePopover
          className="w-48"
          placeholder={t('End date')}
          value={endDate}
          onSelect={(date: Date) => handleEndDateChange(date)}
          onReset={() => handleEndDateChange(null)}
        />
      </div>
    </div>
  );
}
