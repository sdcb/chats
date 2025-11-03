import React, { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  GetModelKeysResult,
  ModelProviderInitialConfig,
  PostModelKeysParams,
} from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import Spinner from '@/components/Spinner/Spinner';
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
import FormTextarea from '@/components/ui/form/textarea';

import {
  deleteModelKeys,
  getModelProviderInitialConfig,
  postModelKeys,
  putModelKeys,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  selected: GetModelKeysResult | null;
  isOpen: boolean;
  onClose: () => void;
  onConfigModel?: (id: number) => void;
  onSaveSuccessful: () => void;
  onDeleteSuccessful: () => void;
  saveLoading?: boolean;
  // Optional: preselect model provider when creating a new key
  defaultModelProviderId?: number;
}

const ModelKeysModal = (props: IProps) => {
  const { t } = useTranslation();
  const {
    selected,
    isOpen,
    onClose,
    onConfigModel,
    onSaveSuccessful,
    onDeleteSuccessful,
    defaultModelProviderId,
  } = props;
  const [initialConfig, setInitialConfig] =
    React.useState<ModelProviderInitialConfig>();
  const [loading, setLoading] = React.useState(false);

  const formSchema = z.object({
    modelProviderId: z
      .string()
      .min(1, `${t('This field is require')}`)
      .default('0'),
    name: z.string().min(1, `${t('This field is require')}`),
    host: z.string().optional(),
    secret: z.string().optional(),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      modelProviderId: '0',
      name: '',
      host: '',
      secret: '',
    },
  });

  const onSave = async (
    values: z.infer<typeof formSchema>,
  ): Promise<number | undefined> => {
    if (!form.formState.isValid) return undefined;

    const modelKeyDto: PostModelKeysParams = {
      modelProviderId: parseInt(values.modelProviderId),
      name: values.name,
      host: values.host || null,
      secret: values.secret || null,
    };

    try {
      const id = await handleModelKeyRequest();
      
      // If onConfigModel is provided and we're creating a new key, call it
      if (!selected && onConfigModel) {
        onConfigModel(id);
      } else {
        // Otherwise just call onSaveSuccessful
        onSaveSuccessful();
        toast.success(t('Saved successful'));
      }
    } catch {
      toast.error(
        t(
          'Operation failed, Please try again later, or contact technical personnel',
        ),
      );
    }

    async function handleModelKeyRequest() {
      if (selected) {
        await putModelKeys(selected.id, modelKeyDto);
        return selected.id;
      } else {
        return await postModelKeys(modelKeyDto);
      }
    }
  };

  async function onDelete() {
    try {
      await deleteModelKeys(selected?.id!);
      onDeleteSuccessful();
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

  const reloadInitialConfig = async (modelProviderId: number) => {
    setInitialConfig(await getModelProviderInitialConfig(modelProviderId));
  };

  useEffect(() => {
    if (selected) return;
    form.setValue('host', initialConfig?.initialHost || undefined);
    form.setValue('secret', initialConfig?.initialSecret || undefined);
  }, [initialConfig]);

  useEffect(() => {
    const subscription = form.watch((value, { name, type }) => {
      if (name === 'modelProviderId' && type === 'change') {
        const id = parseInt(form.getValues('modelProviderId') || '0');
        reloadInitialConfig(id);
        // When creating a new key, always set name to the provider's name on change
        if (!selected) {
          const provider = feModelProviders.find((p) => p.id === id);
          if (provider) {
            form.setValue('name', t(provider.name));
          }
        }
      }
    });
    return () => subscription.unsubscribe();
  }, [form.watch, selected, t]);

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
      if (selected) {
        const { name, modelProviderId, host, secret } = selected;
        form.setValue('name', name);
        form.setValue('modelProviderId', modelProviderId.toString());
        form.setValue('host', host || undefined);
        form.setValue('secret', secret || undefined);
      } else if (defaultModelProviderId !== undefined) {
        // Preselect provider when creating a new key
        form.setValue('modelProviderId', defaultModelProviderId.toString());
        // Always set name to provider's name
        const provider = feModelProviders.find(
          (p) => p.id === defaultModelProviderId,
        );
        if (provider) {
          form.setValue('name', t(provider.name));
        }
      }
      reloadInitialConfig(selected?.modelProviderId || defaultModelProviderId || 0);
    }
  }, [isOpen]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="min-w-[375px] w-3/5">
        <DialogHeader>
          <DialogTitle>
            {selected ? t('Edit Model Keys') : t('Add Model Keys')}
          </DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSave)}>
            <FormField
              key="modelProviderId"
              control={form.control}
              name="modelProviderId"
              render={({ field }) => (
                <FormSelect
                  label={t('Model Provider')}
                  field={field}
                  items={feModelProviders.map((p) => ({
                    value: p.id.toString(),
                    name: t(p.name),
                  }))}
                />
              )}
            />
            <FormField
              key="name"
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormInput label={t('Name')} field={field} />
              )}
            />
            {initialConfig?.initialHost !== null && (
              <FormField
                key="host"
                control={form.control}
                name="host"
                render={({ field }) => (
                  <FormInput label={t('Host')} field={field} />
                )}
              />
            )}
            {initialConfig?.initialSecret !== null && (
              <FormField
                key="secret"
                control={form.control}
                name="secret"
                render={({ field }) => (
                  <FormTextarea rows={2} label={t('Secret')} field={field} />
                )}
              />
            )}
            <DialogFooter className="pt-4">
              <div className="flex gap-4">
                {selected && (
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
                {!selected && onConfigModel ? (
                  <Button type="submit">{t('Save and add the model')}</Button>
                ) : (
                  <Button type="submit">{t('Save')}</Button>
                )}
              </div>
            </DialogFooter>
          </form>
        </Form>
        {loading && (
          <div
            className={`fixed top-0 left-0 bottom-0 right-0 bg-background z-50 text-center text-[12.5px]`}
          >
            <div className="fixed w-screen h-screen top-1/2">
              <div className="flex justify-center">
                <Spinner className="text-gray-500 dark:text-gray-50" />
              </div>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};
export default ModelKeysModal;
