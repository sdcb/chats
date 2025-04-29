import { useState } from 'react';
import { IconEye, IconEyeOff } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  FormControl,
  FormDescription,
  FormItem,
  FormLabel,
  FormMessage,
} from '../form';
import { Input } from '../input';
import { FormFieldType, IFormFieldOption } from './type';

const FormPasswordInput = ({
  label,
  options,
  field,
  hidden,
  disabled,
  autocomplete,
}: {
  label?: string;
  options?: IFormFieldOption;
  field: FormFieldType;
  hidden?: boolean;
  disabled?: boolean;
  autocomplete?: string;
}) => {
  const [showPassword, setShowPassword] = useState(false);

  return (
    <FormItem className="py-2" hidden={hidden}>
      <FormLabel>{options?.label || label}</FormLabel>
      <FormControl>
        <div className="relative">
          <Input
            autoComplete={autocomplete}
            disabled={disabled}
            type={showPassword ? 'text' : 'password'}
            placeholder={options?.placeholder}
            {...field}
          />
          <Button
            type="button"
            variant="ghost"
            className="absolute right-0 top-0 h-full px-3"
            onClick={() => setShowPassword(!showPassword)}
            tabIndex={-1}
          >
            {showPassword ? <IconEye /> : <IconEyeOff />}
          </Button>
        </div>
      </FormControl>
      <FormMessage />
      <FormDescription>{options?.description}</FormDescription>
    </FormItem>
  );
};

export default FormPasswordInput; 