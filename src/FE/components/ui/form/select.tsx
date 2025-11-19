import { FormControl, FormItem, FormLabel, FormMessage } from '../form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../select';
import { FormFieldType, IFormFieldOption } from './type';

const FormSelect = ({
  items,
  label,
  options,
  field,
  hidden,
  disabled,
  className,
}: {
  items: { name: string; value: string }[];
  options?: IFormFieldOption;
  label?: string;
  field: FormFieldType;
  hidden?: boolean;
  disabled?: boolean;
  className?: string;
}) => {
  return (
    <FormItem className={`py-1 ${className}`} hidden={hidden}>
      <FormLabel>{options?.label || label}</FormLabel>
      <Select onValueChange={field.onChange} defaultValue={field.value}>
        <FormControl>
          <SelectTrigger disabled={disabled}>
            <SelectValue placeholder={options?.placeholder} />
          </SelectTrigger>
        </FormControl>
        <SelectContent>
          {items!.map((item) => (
            <SelectItem key={item.value} value={item.value}>
              {item.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <FormMessage />
    </FormItem>
  );
};

export default FormSelect;
