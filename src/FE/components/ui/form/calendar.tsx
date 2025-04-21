import { formatDate } from '@/utils/date';

import { IconSquareRoundedX } from '@/components/Icons/index';

import { Button } from '../button';
import { Calendar } from '../calendar';
import { FormControl, FormItem, FormLabel, FormMessage } from '../form';
import { Popover, PopoverContent, PopoverTrigger } from '../popover';
import { FormFieldType, IFormFieldOption } from './type';

import { cn } from '@/lib/utils';

const FormCalendar = ({
  options,
  field,
}: {
  options: IFormFieldOption;
  field: FormFieldType;
}) => {
  return (
    <FormItem className="py-2">
      <FormLabel>{options.label}</FormLabel>
      <Popover>
        <PopoverTrigger asChild>
          <FormControl className="flex">
            <Button
              variant={'outline'}
              className={cn(
                'w-full pl-3 text-left font-normal',
                !field.value && 'text-muted-foreground',
              )}
            >
              {field.value ? (
                field.value === '-' ? null : (
                  formatDate(field.value)
                )
              ) : (
                <span></span>
              )}
              <IconSquareRoundedX
                onClick={(e) => {
                  field.onChange(null);
                  e.preventDefault();
                }}
                className="z-10 ml-auto h-5 w-5 opacity-50"
              />
            </Button>
          </FormControl>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="start">
          <Calendar
            mode="single"
            selected={field.value}
            onSelect={field.onChange}
            initialFocus
          />
        </PopoverContent>
      </Popover>
      <FormMessage />
    </FormItem>
  );
};

export default FormCalendar;
