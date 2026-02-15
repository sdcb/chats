import React, { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { IconInfo } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';

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
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import FormSelect from '@/components/ui/form/select';
import FormSwitch from '@/components/ui/form/switch';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';
import { Textarea } from '@/components/ui/textarea';

import {
  deleteFileService,
  getFileServiceTypeInitialConfig,
  postFileService,
  putFileService,
  validateFileService,
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
  const [validating, setValidating] = useState(false);
  const [validateError, setValidateError] = useState<string | null>(null);

  const [isEditingConfigs, setIsEditingConfigs] = useState(false);
  const configsTextareaRef = useRef<HTMLTextAreaElement>(null);

  const toMaskedNull = (value: unknown) => {
    if (value === null || value === undefined) return value;
    if (typeof value !== 'string') return value;
    return value.length > 7 ? value.slice(0, 5) + '****' + value.slice(-2) : value;
  };

  const maskJsonValues = (input: string) => {
    try {
      const parsed = JSON.parse(input);
      const maskDeep = (node: any): any => {
        if (node === null || node === undefined) return node;
        if (typeof node === 'string') return toMaskedNull(node);
        if (Array.isArray(node)) return node.map(maskDeep);
        if (typeof node === 'object') {
          const out: Record<string, any> = {};
          for (const [k, v] of Object.entries(node)) out[k] = maskDeep(v);
          return out;
        }
        return node;
      };

      return JSON.stringify(maskDeep(parsed), null, 2);
    } catch {
      return input;
    }
  };

  const getErrorMessage = (err: any) => {
    if (!err) return '';
    if (typeof err === 'string') return err;
    if (err instanceof Error) return err.message;
    if (typeof err === 'object') {
      const msg = (err as any).message || (err as any).errMessage;
      if (typeof msg === 'string') return msg;
      try {
        return JSON.stringify(err);
      } catch {
        return String(err);
      }
    }
    return String(err);
  };
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
      render: (options: IFormFieldOption, field: FormFieldType) => {
        const { ref: rhfRef, ...fieldRest } = field;
        const v = typeof field.value === 'string' ? field.value : '';
        const displayValue = maskJsonValues(v);

        return (
          <FormItem className="py-1">
            <FormLabel>{options.label}</FormLabel>
            <FormControl>
              {isEditingConfigs ? (
                <Textarea
                  rows={6}
                  placeholder={options?.placeholder}
                  className="font-mono"
                  {...fieldRest}
                  ref={(el) => {
                    configsTextareaRef.current = el;
                    if (typeof rhfRef === 'function') rhfRef(el);
                    else if (rhfRef) (rhfRef as any).current = el;
                  }}
                  onBlur={(e) => {
                    field.onBlur();
                    setIsEditingConfigs(false);
                  }}
                />
              ) : (
                <div
                  className="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm cursor-text"
                  style={{
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-word',
                  }}
                  onClick={() => setIsEditingConfigs(true)}
                >
                  {displayValue ? (
                    displayValue
                  ) : (
                    <span className="text-muted-foreground">
                      {options?.placeholder || ''}
                    </span>
                  )}
                </div>
              )}
            </FormControl>
            <FormMessage />
          </FormItem>
        );
      },
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
      const msg = getErrorMessage(err);
      toast.error(
        msg
          ? msg
          : t('Operation failed, Please try again later, or contact technical personnel'),
      );
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

  async function onValidate() {
    try {
      setValidating(true);
      setValidateError(null);
      const ok = await form.trigger();
      if (!ok) return;

      const values = form.getValues();
      await validateFileService({
        fileServiceTypeId: parseInt(values.fileServiceTypeId!),
        name: values.name!,
        isDefault: values.isDefault,
        configs: values.configs!,
      });
      setValidateError(null);
      toast.success(t('Verified Successfully'));
    } catch (err: any) {
      const msg = getErrorMessage(err);
      setValidateError(msg || t('Verified Failed'));
    } finally {
      setValidating(false);
    }
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
      setValidateError(null);
      setIsEditingConfigs(false);
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
    if (isEditingConfigs) {
      // next tick focus
      setTimeout(() => configsTextareaRef.current?.focus(), 0);
    }
  }, [isEditingConfigs]);

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
            <DialogFooter className="pt-4 sm:justify-between">
              {selected ? (
                <Button type="button" variant="destructive" onClick={onDelete}>
                  {t('Delete')}
                </Button>
              ) : (
                <div />
              )}
              <div className="flex gap-2 justify-end">
                <div className="flex items-center gap-1">
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={onValidate}
                    disabled={validating}
                  >
                    {validating ? t('Validating...') : t('Validate')}
                  </Button>

                  {validateError && (
                    <Tips
                      className="h-8"
                      side="bottom"
                      trigger={
                        <Button
                          variant="ghost"
                          className="p-0.5 m-0 h-8 w-8"
                        >
                          <IconInfo stroke="#FFD738" size={16} />
                        </Button>
                      }
                      content={
                        <div className="w-80">
                          <div className="font-mono text-xs whitespace-pre-wrap break-all">
                            {validateError}
                          </div>
                        </div>
                      }
                    />
                  )}
                </div>
                <Button type="submit">{t('Save')}</Button>
              </div>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};

export default FileServiceModal;
