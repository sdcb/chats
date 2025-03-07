import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  GetModelKeysResult,
  ModelReferenceDto,
  SimpleModelReferenceDto,
  UpdateModelDto,
} from '@/types/adminApis';

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
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import {
  getModelProviderModels,
  getModelReference,
  postModels,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  isOpen: boolean;
  modelKeys: GetModelKeysResult[];
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

const AddModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const [modelVersions, setModelVersions] = useState<SimpleModelReferenceDto[]>(
    [],
  );
  const { isOpen, onClose, onSuccessful, modelKeys } = props;

  const formSchema = z.object({
    modelReferenceId: z.string().default('0'),
    name: z
      .string()
      .min(1, `${t('This field is require')}`)
      .optional(),
    enabled: z.boolean(),
    deploymentName: z.string().optional(),
    modelKeyId: z
      .string()
      .min(1, `${t('This field is require')}`)
      .default('0'),
    inputPrice1M: z.coerce.number(),
    outputPrice1M: z.coerce.number(),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      modelReferenceId: '0',
      name: '',
      enabled: true,
      deploymentName: '',
      modelKeyId: '',
      inputPrice1M: 0,
      outputPrice1M: 0,
    },
  });

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    if (!form.formState.isValid) return;
    const dto: UpdateModelDto = {
      deploymentName: values.deploymentName || null,
      enabled: values.enabled!,
      inputTokenPrice1M: values.inputPrice1M,
      outputTokenPrice1M: values.outputPrice1M,
      modelKeyId: parseInt(values.modelKeyId!),
      name: values.name!,
      modelReferenceId: +values.modelReferenceId!,
      rank: null,
    };
    postModels(dto).then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    });
  };

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
    }
  }, [isOpen]);

  const onModelReferenceChanged = async (modelReferenceId: number) => {
    getModelReference(modelReferenceId).then((data) => {
      form.setValue('inputPrice1M', data.promptTokenPrice1M);
      form.setValue('outputPrice1M', data.responseTokenPrice1M);
    });
  };

  useEffect(() => {
    const subscription = form.watch(async (value, { name, type }) => {
      if (name === 'modelKeyId' && type === 'change') {
        const modelKeyId = value.modelKeyId;
        const modelProviderId = modelKeys.find((x) => x.id === +modelKeyId!)
          ?.modelProviderId!;
        const possibleModels = await getModelProviderModels(modelProviderId);
        setModelVersions(possibleModels);
      }
      if (name === 'modelReferenceId' && type === 'change') {
        const modelReferenceId = +value.modelReferenceId!;
        onModelReferenceChanged(modelReferenceId);
      }
    });
    return () => subscription?.unsubscribe();
  }, [form.watch]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-3/4">
        <DialogHeader>
          <DialogTitle>{t('Add Model')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)}>
            <div className="grid grid-cols-2 gap-4">
              <FormField
                key="name"
                control={form.control}
                name="name"
                render={({ field }) => {
                  return (
                    <FormInput field={field} label={t('Model Display Name')!} />
                  );
                }}
              ></FormField>
              <div className="flex justify-between">
                <FormField
                  key="modelKeyId"
                  control={form.control}
                  name="modelKeyId"
                  render={({ field }) => {
                    return (
                      <FormSelect
                        className="w-full"
                        field={field}
                        label={t('Model Keys')!}
                        items={modelKeys.map((keys) => ({
                          name: keys.name,
                          value: keys.id.toString(),
                        }))}
                      />
                    );
                  }}
                ></FormField>
                <div
                  hidden={!form.getValues('modelKeyId')}
                  className="text-sm w-36 mt-12 text-right"
                >
                  <Popover>
                    <PopoverTrigger>
                      <span className="text-primary">
                        {t('Click View Configs')}
                      </span>
                    </PopoverTrigger>
                    <PopoverContent className="w-full">
                      {JSON.stringify(
                        modelKeys
                          .find((x) => x.id === +form.getValues('modelKeyId')!)
                          ?.toConfigs(),
                        null,
                        2,
                      )}
                    </PopoverContent>
                  </Popover>
                </div>
              </div>
            </div>
            <div
              className="grid grid-cols-2 gap-4"
              key={form.getValues('modelKeyId')! || 'modelVersionKey'}
            >
              <FormField
                key="modelReferenceId"
                control={form.control}
                name="modelReferenceId"
                render={({ field }) => {
                  return (
                    <FormSelect
                      field={field}
                      label={t('Model Version')!}
                      items={modelVersions.map((key) => ({
                        name: key.name,
                        value: key.id.toString(),
                      }))}
                    />
                  );
                }}
              ></FormField>
              <FormField
                key="deploymentName"
                control={form.control}
                name="deploymentName"
                render={({ field }) => {
                  return (
                    <FormInput label={t('Deployment Name')!} field={field} />
                  );
                }}
              ></FormField>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <FormField
                key="inputPrice1M"
                control={form.control}
                name="inputPrice1M"
                render={({ field }) => {
                  return (
                    <FormInput
                      type="number"
                      label={`${t('1M input tokens price')}(${t('Yuan')})`}
                      field={field}
                    />
                  );
                }}
              ></FormField>
              <FormField
                key="outputPrice1M"
                control={form.control}
                name="outputPrice1M"
                render={({ field }) => {
                  return (
                    <FormInput
                      type="number"
                      label={`${t('1M output tokens price')}(${t('Yuan')})`}
                      field={field}
                    />
                  );
                }}
              ></FormField>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="flex gap-4">
                <FormField
                  key={'enabled'}
                  control={form.control}
                  name={'enabled'}
                  render={({ field }) => {
                    return (
                      <FormSwitch label={t('Is it enabled')!} field={field} />
                    );
                  }}
                ></FormField>
              </div>
            </div>
            <DialogFooter className="pt-4">
              <Button type="submit">{t('Save')}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};
export default AddModelModal;
