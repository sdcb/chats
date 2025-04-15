import { Calendar } from 'lucide-react';

import { IconX } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Calendar as CalendarComponent } from '@/components/ui/calendar';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import { cn } from '@/lib/utils';
import { format } from 'date-fns';

interface DateTimePopoverProps {
  value: string;
  className?: string;
  placeholder?: string;
  onSelect: (date: Date) => void;
  onReset?: () => void;
}

const DateTimePopover = ({
  value,
  className,
  placeholder,
  onSelect,
  onReset,
}: DateTimePopoverProps) => {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <div
          className={cn(
            'relative flex h-10 w-[240px] items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-0 focus:ring-offset-0 disabled:cursor-not-allowed disabled:opacity-50',
            className,
          )}
        >
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 opacity-50" />
            <span className={cn(!value && 'text-neutral-400')}>
              {value
                ? format(new Date(value), 'yyyy/M/d')
                : placeholder || 'Pick a date'}
            </span>
          </div>
          {onReset && value && (
            <Button
              variant="ghost"
              size="icon"
              className="h-4 w-4 opacity-50 hover:opacity-100"
              onClick={(e) => {
                e.preventDefault();
                onReset();
              }}
            >
              <IconX size={16} />
            </Button>
          )}
        </div>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="start">
        <CalendarComponent
          mode="single"
          selected={value ? new Date(value) : undefined}
          onSelect={(date) => {
            if (date) {
              onSelect(date);
            }
          }}
          initialFocus
        />
      </PopoverContent>
    </Popover>
  );
};

export default DateTimePopover;
