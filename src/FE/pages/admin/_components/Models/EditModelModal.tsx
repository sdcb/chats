import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  AdminModelDto,
  GetModelKeysResult,
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
  deleteModels,
  getModelProviderModels,
  getModelReference,
  putModels,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  isOpen: boolean;
  selected: AdminModelDto;
  modelKeys: GetModelKeysResult[];
  modelVersionList?: SimpleModelReferenceDto[];
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

const EditModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const {
    isOpen,
    onClose,
    selected,
    onSuccessful,
    modelKeys,
    modelVersionList,
  } = props;

  const [modelVersions, setModelVersions] = useState<SimpleModelReferenceDto[]>(
    modelVersionList || [],
  );

  const formSchema = z.object({
    modelReferenceId: z.string(),
    name: z
      .string()
      .min(1, `${t('This field is require')}`)
      .optional(),
    modelId: z.string().optional(),
    enabled: z.boolean().optional(),
    deploymentName: z.string().optional(),
    modelKeyId: z.string().nullable().default(null),
    inputPrice1M: z.coerce.number(),
    outputPrice1M: z.coerce.number(),
    rank: z.coerce.number().nullable().default(null),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      modelReferenceId: '',
      name: '',
      modelId: '',
      enabled: true,
      deploymentName: '',
      modelKeyId: '',
      inputPrice1M: 0,
      outputPrice1M: 0,
      rank: null,
    },
  });

  function onSubmit(values: z.infer<typeof formSchema>) {
    if (!form.formState.isValid) return;
    const dto: UpdateModelDto = {
      deploymentName: values.deploymentName || null,
      enabled: values.enabled!,
      inputTokenPrice1M: values.inputPrice1M,
      outputTokenPrice1M: values.outputPrice1M,
      modelKeyId: parseInt(values.modelKeyId!),
      modelReferenceId: parseInt(values.modelReferenceId),
      name: values.name!,
      rank: values.rank,
    };
    putModels(values.modelId!, dto).then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    });
  }

  async function onDelete() {
    try {
      await deleteModels(selected!.modelId);
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

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
      const {
        name,
        modelId,
        modelReferenceId,
        enabled,
        modelKeyId,
        deploymentName,
        inputTokenPrice1M,
        outputTokenPrice1M,
        rank,
      } = selected;
      form.setValue('name', name);
      form.setValue('modelId', modelId.toString());
      form.setValue('enabled', enabled);
      form.setValue('modelKeyId', modelKeyId.toString());
      form.setValue('deploymentName', deploymentName || '');
      form.setValue('inputPrice1M', inputTokenPrice1M);
      form.setValue('outputPrice1M', outputTokenPrice1M);
      form.setValue('modelReferenceId', modelReferenceId.toString());
      form.setValue('rank', rank);
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
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('Edit Model')}</DialogTitle>
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
                        items={modelKeys
                          .filter(
                            (x) =>
                              x.modelProviderId === selected.modelProviderId,
                          )
                          .map((keys) => ({
                            name: keys.name,
                            value: keys.id.toString(),
                          }))}
                      />
                    );
                  }}
                ></FormField>
                <div
                  hidden={!form.getValues('modelKeyId')}
                  className="text-sm mt-12 w-36 text-right"
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
                          .find(
                            (x) =>
                              x.id === parseInt(form.getValues('modelKeyId')!),
                          )
                          ?.toConfigs(),
                        null,
                        2,
                      )}
                    </PopoverContent>
                  </Popover>
                </div>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
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
                  key="rank"
                  control={form.control}
                  name="rank"
                  render={({ field }) => {
                    return <FormInput label={t('Rank')} field={field} />;
                  }}
                ></FormField>
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
              <Button
                type="button"
                variant="destructive"
                onClick={(e) => {
                  onDelete();
                  e.preventDefault();
                }}
              >
                {t('Delete')}
              </Button>
              <Button type="submit">{t('Save')}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};
export default EditModelModal;
