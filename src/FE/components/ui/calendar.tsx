import * as React from 'react';
import { DayPicker } from 'react-day-picker';
import { zhCN, enUS } from 'react-day-picker/locale';
import { cn } from '@/lib/utils';
import useTranslation from '@/hooks/useTranslation';

export type CalendarProps = React.ComponentProps<typeof DayPicker>;

function Calendar({
  className,
  classNames,
  showOutsideDays = true,
  ...props
}: CalendarProps) {
  const { language } = useTranslation();
  
  // 根据当前语言选择合适的 locale
  const locale = language === 'zh-CN' ? zhCN : enUS;
  
  return (
    <DayPicker
      locale={locale}
      showOutsideDays={showOutsideDays}
      className={cn('p-3', className)}
      classNames={{
        ...classNames,
      }}
      timeZone={Intl.DateTimeFormat().resolvedOptions().timeZone}
      {...props}
    />
  );
}
Calendar.displayName = 'Calendar';

export { Calendar };
