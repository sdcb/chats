import { HTMLInputTypeAttribute } from 'react';

import {
  FormControl,
  FormDescription,
  FormItem,
  FormLabel,
  FormMessage,
} from '../form';
import { Input } from '../input';
import { FormFieldType, IFormFieldOption } from './type';

const FormInput = ({
  label,
  options,
  field,
  type,
  hidden,
  disabled,
  autocomplete,
}: {
  label?: string;
  options?: IFormFieldOption;
  field: FormFieldType;
  type?: HTMLInputTypeAttribute;
  hidden?: boolean;
  disabled?: boolean;
  autocomplete?: string;
}) => {
  return (
    <FormItem className="py-1" hidden={hidden}>
      <FormLabel>{options?.label || label}</FormLabel>
      <FormControl>
        <Input
          autoComplete={autocomplete}
          disabled={disabled}
          type={type}
          placeholder={options?.placeholder}
          {...field}
        />
      </FormControl>
      <FormMessage />
      <FormDescription>{options?.description}</FormDescription>
    </FormItem>
  );
};

export default FormInput;
