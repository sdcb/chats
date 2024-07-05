import React, { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import { useTranslation } from 'next-i18next';

import { getFileConfigs } from '@/utils/file';
import { mergeConfigs } from '@/utils/model';

import { GetFileServicesResult } from '@/types/admin';
import {
  FileServicesType,
  PostFileServicesParams,
  PutFileServicesParams,
} from '@/types/file';

import FormSelect from '@/components/ui/form/select';

import { Button } from '../../ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../ui/dialog';
import { Form, FormField } from '../../ui/form';
import FormInput from '../../ui/form/input';
import FormSwitch from '../../ui/form/switch';
import FormTextarea from '../../ui/form/textarea';
import { FormFieldType, IFormFieldOption } from '../../ui/form/type';

import { postFileService, putFileService } from '@/apis/adminService';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  selected: GetFileServicesResult | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

export const FileServiceModal = (props: IProps) => {
  const { t } = useTranslation('admin');
  const { selected, isOpen, onClose, onSuccessful } = props;
  const formFields: IFormFieldOption[] = [
    {
      name: 'type',
      label: t('File Service Type'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormSelect
          items={Object.keys(FileServicesType).map((key) => ({
            name: FileServicesType[key as keyof typeof FileServicesType],
            value: FileServicesType[key as keyof typeof FileServicesType],
          }))}
          options={options}
          field={field}
        />
      ),
    },
    {
      name: 'name',
      label: t('Service Name'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput options={options} field={field} />
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
        <FormTextarea rows={6} options={options} field={field} />
      ),
    },
  ];

  const formSchema = z.object({
    type: z
      .string()
      .min(1, `${t('This field is require')}`)
      .optional(),
    name: z
      .string()
      .min(1, `${t('This field is require')}`)
      .optional(),
    enabled: z.boolean().optional(),
    configs: z
      .string()
      .min(1, `${t('This field is require')}`)
      .optional(),
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
      p = putFileService({
        ...values,
        id: selected.id,
      } as PutFileServicesParams);
    } else {
      p = postFileService(values as PostFileServicesParams);
    }
    p.then(() => {
      onSuccessful();
      toast.success(t('Save successful!'));
    }).catch(() => {
      toast.error(
        t(
          'Operation failed! Please try again later, or contact technical personnel.',
        ),
      );
    });
  }

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
      if (selected) {
        form.setValue('name', selected.name);
        form.setValue('type', selected.type);
        form.setValue('enabled', selected.enabled);
        form.setValue(
          'configs',
          mergeConfigs(getFileConfigs(selected.type), selected.configs),
        );
      }
    }
  }, [isOpen]);

  useEffect(() => {
    const subscription = form.watch((value, { name, type }) => {
      if (name === 'type' && type === 'change') {
        const type = value.type as FileServicesType;
        form.setValue(
          'configs',
          JSON.stringify(getFileConfigs(type) || {}, null, 2),
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
            {selected ? t('Edit File Service') : t('Add File Service')}
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