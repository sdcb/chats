import React, { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { getLoginConfigs } from '@/utils/login';
import { mergeConfigs } from '@/utils/model';

import {
  GetLoginServicesResult,
  PostLoginServicesParams,
} from '@/types/adminApis';
import { LoginType } from '@/types/user';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form, FormField } from '@/components/ui/form';
import FormSelect from '@/components/ui/form/select';
import FormSwitch from '@/components/ui/form/switch';
import FormTextarea from '@/components/ui/form/textarea';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';

import { postLoginService, putLoginService } from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  selected: GetLoginServicesResult | null;
  types: LoginType[];
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

const LoginServiceModal = (props: IProps) => {
  const { t } = useTranslation();
  const { selected, types, isOpen, onClose, onSuccessful } = props;
  const formFields: IFormFieldOption[] = [
    {
      name: 'type',
      label: t('Login Service Type'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormSelect
          disabled={!!selected}
          items={Object.keys(LoginType)
            .filter((x) => selected || !types.find((t) => t.toString() === x))
            .map((key) => ({
              name: LoginType[key as keyof typeof LoginType],
              value: LoginType[key as keyof typeof LoginType],
            }))}
          options={options}
          field={field}
        />
      ),
    },
    {
      name: 'enabled',
      label: t('Is it enabled'),
      defaultValue: true,
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormSwitch options={options} field={field} />
      ),
    },
    {
      name: 'configs',
      label: t('Service Configs'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormTextarea rows={5} options={options} field={field} />
      ),
    },
  ];

  const formSchema = z.object({
    type: z.string().min(1, `${t('This field is require')}`),
    enabled: z.boolean().optional(),
    configs: z.string().min(1, `${t('This field is require')}`),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: formFields.reduce((obj: any, field) => {
      obj[field.name] = field.defaultValue;
      return obj;
    }, {}),
  });

  function onSubmit(values: z.infer<typeof formSchema>) {
    if (!form.formState.isValid) return;
    let p = null;
    if (selected) {
      p = putLoginService(selected.id, values as PostLoginServicesParams);
    } else {
      p = postLoginService(values as PostLoginServicesParams);
    }
    p.then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    });
  }

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
      if (selected) {
        form.setValue('type', selected.type);
        form.setValue('enabled', selected.enabled);
        form.setValue(
          'configs',
          mergeConfigs(getLoginConfigs(selected.type), selected.configs),
        );
      }
    }
  }, [isOpen]);

  useEffect(() => {
    const subscription = form.watch((value, { name, type }) => {
      if (name === 'type' && type === 'change') {
        const type = value.type as LoginType;
        form.setValue(
          'configs',
          JSON.stringify(getLoginConfigs(type) || {}, null, 2),
        );
      }
    });
    return () => subscription.unsubscribe();
  }, [form.watch]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {selected ? t('Edit Login Service') : t('Add Login Service')}
          </DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)}>
            {formFields.map((item) => (
              <FormField
                key={item.name}
                control={form.control}
                name={item.name as never}
                render={({ field }) => item.render(item, field)}
              />
            ))}
            <DialogFooter className="pt-4">
              <Button type="submit">{t('Save')}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};
export default LoginServiceModal;
