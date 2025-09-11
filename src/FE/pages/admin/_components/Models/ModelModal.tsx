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
  postModels,
  putModels,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  isOpen: boolean;
  modelKeys: GetModelKeysResult[];
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
  // For edit mode
  selected?: AdminModelDto;
  // For add mode: preselect model key when creating a model
  defaultModelKeyId?: number;
}

const ModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const [modelVersions, setModelVersions] = useState<SimpleModelReferenceDto[]>(
    [],
  );
  const { 
    isOpen, 
    onClose, 
    onSuccessful, 
    modelKeys, 
    defaultModelKeyId, 
    selected 
  } = props;

  // Determine if this is edit mode
  const isEditMode = !!selected;

  const formSchema = z.object({
    modelReferenceId: z.string().default('0'),
    name: z.string().min(1, `${t('This field is require')}`),
    enabled: z.boolean(),
    deploymentName: z.string().optional(),
    modelKeyId: z
      .string()
      .min(1, `${t('This field is require')}`)
      .default('0'),
    inputPrice1M: z.coerce.number(),
    outputPrice1M: z.coerce.number(),
    modelId: z.string().optional(),
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
      modelId: '',
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
    };

    const apiCall = isEditMode 
      ? putModels(values.modelId!, dto)
      : postModels(dto);

    apiCall.then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    });
  };

  const onDelete = async () => {
    if (!selected) return;
    
    try {
      await deleteModels(selected.modelId);
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
  };

  useEffect(() => {
    if (isOpen) {
      // Clear model versions first
      setModelVersions([]);
      form.reset();
      form.formState.isValid;
      
      if (isEditMode && selected) {
        // Edit mode: populate form with existing data
        const {
          name,
          modelId,
          modelReferenceId,
          enabled,
          modelKeyId,
          deploymentName,
          inputTokenPrice1M,
          outputTokenPrice1M,
        } = selected;
        
        form.setValue('name', name);
        form.setValue('modelId', modelId.toString());
        form.setValue('enabled', enabled);
        form.setValue('modelKeyId', modelKeyId.toString());
        form.setValue('deploymentName', deploymentName || '');
        form.setValue('inputPrice1M', inputTokenPrice1M);
        form.setValue('outputPrice1M', outputTokenPrice1M);
        form.setValue('modelReferenceId', modelReferenceId.toString());
      } else {
        // Add mode: set default values
        if (defaultModelKeyId !== undefined) {
          form.setValue('modelKeyId', defaultModelKeyId.toString());
          const mk = modelKeys.find((x) => x.id === defaultModelKeyId);
          if (mk) {
            getModelProviderModels(mk.modelProviderId).then(setModelVersions);
          }
        }
      }
    } else {
      // Clear model versions when modal is closed
      setModelVersions([]);
    }
  }, [isOpen, selected, defaultModelKeyId, modelKeys, isEditMode]);

  // Separate useEffect for loading model versions in edit mode
  useEffect(() => {
    if (isOpen && isEditMode && selected && modelKeys.length > 0) {
      const { modelKeyId } = selected;
      const modelProviderId = modelKeys.find((x) => x.id === modelKeyId)?.modelProviderId;
      if (modelProviderId !== undefined) {
        getModelProviderModels(modelProviderId).then((possibleModels) => {
          setModelVersions(possibleModels);
        });
      }
    }
  }, [isOpen, isEditMode, selected, modelKeys]);

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
        const modelProvider = modelKeys.find((x) => x.id === +modelKeyId!);
        if (modelProvider !== undefined) {
          const possibleModels = await getModelProviderModels(modelProvider.modelProviderId);
          setModelVersions(possibleModels);
        }
      }
      if (name === 'modelReferenceId' && type === 'change') {
        const modelReferenceId = +value.modelReferenceId!;
        onModelReferenceChanged(modelReferenceId);
      }
    });
    return () => subscription?.unsubscribe();
  }, [form.watch]);

  const getAvailableModelKeys = () => {
    if (isEditMode && selected) {
      // In edit mode, only show keys for the same provider
      return modelKeys.filter(
        (x) => x.modelProviderId === selected.modelProviderId,
      );
    } else {
      // In add mode, show all keys
      return modelKeys;
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-3/4">
        <DialogHeader>
          <DialogTitle>
            {isEditMode ? t('Edit Model') : t('Add Model')}
          </DialogTitle>
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
                        items={getAvailableModelKeys().map((keys) => ({
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
                      <span className="text-primary invisible sm:visible">
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
              {isEditMode && (
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
              )}
              <Button type="submit">{t('Save')}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};

export default ModelModal;
