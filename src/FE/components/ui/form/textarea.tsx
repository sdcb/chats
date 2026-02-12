import { FormControl, FormItem, FormLabel, FormMessage } from '../form';
import { Textarea } from '../textarea';
import { FormFieldType, IFormFieldOption } from './type';

const FormTextarea = ({
  label,
  options,
  field,
  hidden,
  rows,
  className,
}: {
  label?: string;
  options?: Partial<IFormFieldOption>;
  field: FormFieldType;
  hidden?: boolean;
  rows?: number;
  className?: string;
}) => {
  return (
    <FormItem className="py-1" hidden={hidden}>
      <FormLabel>{options?.label || label}</FormLabel>
      <FormControl>
        <Textarea
          rows={rows}
          placeholder={options?.placeholder}
          className={className}
          {...field}
        />
      </FormControl>
      <FormMessage />
    </FormItem>
  );
};

export default FormTextarea;
