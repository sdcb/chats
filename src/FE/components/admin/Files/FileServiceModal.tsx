import React, { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  GetFileServicesResult,
  PostFileServicesParams,
} from '@/types/adminApis';
import { feFileServiceTypes } from '@/types/file';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form, FormField } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import FormSelect from '@/components/ui/form/select';
import FormSwitch from '@/components/ui/form/switch';
import FormTextarea from '@/components/ui/form/textarea';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';

import {
  deleteFileService,
  getFileServiceTypeInitialConfig,
  postFileService,
  putFileService,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  selected: GetFileServicesResult | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

const FileServiceModal = (props: IProps) => {
  const { t } = useTranslation();
  const { selected, isOpen, onClose, onSuccessful } = props;
  const formFields: IFormFieldOption[] = [
    {
      name: 'fileServiceTypeId',
      label: t('File Service Type'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormSelect
          items={feFileServiceTypes.map((x) => ({
            name: t(x.name),
            value: x.id.toString(),
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
      name: 'isDefault',
      label: t('Is Default'),
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
    fileServiceTypeId: z.string().min(1, `${t('This field is require')}`),
    name: z.string().min(1, `${t('This field is require')}`),
    isDefault: z.boolean(),
    configs: z.string().min(1, `${t('This field is require')}`),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: formFields.reduce((obj: any, field) => {
      obj[field.name] = field.defaultValue;
      return obj;
    }, {}),
  });

  async function onDelete() {
    try {
      await deleteFileService(selected!.id);
      onSuccessful();
      toast.success(t('Deleted successful'));
    } catch (err: any) {
      try {
        const resp = await err.json();
        toast.error(t(resp.message));
      } catch {
        toast.error(
          t(
            'Operation failed, Please try again later, or contact technical personnel',
          ),
        );
      }
    }
  }

  function onSubmit(values: z.infer<typeof formSchema>) {
    if (!form.formState.isValid) return;
    let p = null;
    const dto: PostFileServicesParams = {
      fileServiceTypeId: parseInt(values.fileServiceTypeId!),
      name: values.name!,
      isDefault: values.isDefault,
      configs: values.configs!,
    };
    if (selected) {
      p = putFileService(selected.id, dto);
    } else {
      p = postFileService(dto);
    }
    p.then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    });
  }

  const formatConfigs = (config: string) => {
    try {
      const parsed = JSON.parse(config);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return config;
    }
  };

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
      if (selected) {
        form.setValue('name', selected.name);
        form.setValue(
          'fileServiceTypeId',
          selected.fileServiceTypeId.toString(),
        );
        form.setValue('isDefault', selected.isDefault);
        form.setValue('configs', formatConfigs(selected.configs));
      }
    }
  }, [isOpen]);

  useEffect(() => {
    const subscription = form.watch((value, { name, type }) => {
      if (name === 'fileServiceTypeId' && type === 'change') {
        const fileServiceTypeId = parseInt(value.fileServiceTypeId!);
        getFileServiceTypeInitialConfig(fileServiceTypeId).then((res) => {
          form.setValue('configs', formatConfigs(res), { shouldValidate: true });
        });
        form.setValue('name', t(feFileServiceTypes[fileServiceTypeId].name));
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
              {selected && (
                <Button type="button" variant="destructive" onClick={onDelete}>
                  {t('Delete')}
                </Button>
              )}
              <Button type="submit">{t('Save')}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};

export default FileServiceModal;
