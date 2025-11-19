import { FormControl, FormItem, FormLabel, FormMessage } from '../form';
import { Switch } from '../switch';
import { FormFieldType, IFormFieldOption } from './type';

const FormSwitch = ({
  label,
  options,
  field,
}: {
  label?: string;
  options?: IFormFieldOption;
  field: FormFieldType;
}) => {
  return (
    <FormItem className="py-1">
      <FormLabel>{options?.label || label}</FormLabel>
      <FormControl className="flex">
        <Switch checked={field.value} onCheckedChange={field.onChange} />
      </FormControl>
      <FormMessage />
    </FormItem>
  );
};

export default FormSwitch;
