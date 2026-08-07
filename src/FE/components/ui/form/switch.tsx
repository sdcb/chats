import { FormControl, FormItem, FormLabel, FormMessage } from '../form';
import { Switch } from '../switch';
import { FormFieldType, IFormFieldOption } from './type';

const FormSwitch = ({
  label,
  options,
  field,
  disabled,
}: {
  label?: string;
  options?: Partial<IFormFieldOption>;
  field: FormFieldType;
  disabled?: boolean;
}) => {
  return (
    <FormItem className="py-1">
      <FormLabel>{options?.label || label}</FormLabel>
      <FormControl className="flex">
        <Switch
          checked={field.value}
          onCheckedChange={field.onChange}
          disabled={disabled}
        />
      </FormControl>
      <FormMessage />
    </FormItem>
  );
};

export default FormSwitch;
