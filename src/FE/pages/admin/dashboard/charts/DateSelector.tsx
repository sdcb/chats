import React from 'react';
import useTranslation from '@/hooks/useTranslation';
import { formatDate } from '@/utils/date';
import { Card } from '@/components/ui/card';
import DateTimePopover from '@/pages/home/_components/Popover/DateTimePopover';

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
    <Card className="p-4 mb-4 border-none">
      <div className="flex flex-col sm:flex-row gap-4 items-end">
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
    </Card>
  );
} 