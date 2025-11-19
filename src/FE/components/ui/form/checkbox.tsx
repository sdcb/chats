import { Checkbox } from '../checkbox';
import { FormControl, FormItem, FormLabel, FormMessage } from '../form';
import { FormFieldType, IFormFieldOption } from './type';

const FormCheckbox = ({
  label,
  disabled,
  options,
  field,
}: {
  label?: string;
  disabled?: boolean;
  options?: IFormFieldOption;
  field: FormFieldType;
}) => {
  return (
    <FormItem className="py-1 flex items-center gap-2">
      <FormControl className="flex">
        <Checkbox
          checked={field.value}
          disabled={disabled}
          onCheckedChange={field.onChange}
        />
      </FormControl>
      <FormLabel style={{ marginTop: 0 }}>{label}</FormLabel>
      <FormMessage />
    </FormItem>
  );
};

export default FormCheckbox;
