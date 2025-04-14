import React, { useState } from 'react';
import { useForm } from 'react-hook-form';

import useTranslation from '@/hooks/useTranslation';

import { UserRole } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import { Prompt } from '@/types/prompt';

import TemperatureSlider from '@/components/TemperatureSlider/TemperatureSlider';
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
import FormSwitch from '@/components/ui/form/switch';
import FormTextarea from '@/components/ui/form/textarea';

import { useUserInfo } from '@/providers/UserProvider';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  prompt: Prompt;
  onUpdatePrompt: (prompt: Prompt) => void;
  onCreatePrompt?: (prompt: Prompt) => void;
  isCreate?: boolean;
  onClose: () => void;
}

const PromptModal = (props: IProps) => {
  const { t } = useTranslation();
  const {
    prompt,
    onUpdatePrompt,
    onCreatePrompt,
    isCreate = false,
    onClose,
  } = props;
  const user = useUserInfo();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const formSchema = z.object({
    name: z.string().min(1, t('This field is require')),
    content: z.string().min(1, t('This field is require')),
    isDefault: z.boolean().optional(),
    isSystem: z.boolean().optional(),
    setsTemperature: z.boolean().default(false),
    temperature: z.number().nullable(),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: prompt.name || '',
      content: prompt.content || '',
      isDefault: prompt.isDefault || false,
      isSystem: prompt.isSystem || false,
      setsTemperature: prompt.temperature !== null,
      temperature: prompt.temperature,
    },
  });

  const onSubmit = async (values: z.infer<typeof formSchema>) => {
    setIsSubmitting(true);
    try {
      const updatedPrompt: Prompt = {
        ...prompt,
        name: values.name,
        content: values.content.trim(),
        isDefault: values.isDefault || false,
        isSystem: values.isSystem || false,
        temperature: values.setsTemperature
          ? values.temperature || DEFAULT_TEMPERATURE
          : null,
      };

      if (isCreate && onCreatePrompt) {
        await onCreatePrompt(updatedPrompt);
      } else {
        await onUpdatePrompt(updatedPrompt);
      }

      onClose();
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={true} onOpenChange={onClose}>
      <DialogContent className="w-[95vw] max-w-2xl max-h-[90vh] flex flex-col p-0 gap-0 overflow-hidden">
        <DialogHeader className="p-4 pb-0">
          <DialogTitle>
            {isCreate ? t('Create Prompt') : t('Edit Prompt')}
          </DialogTitle>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 pt-2">
          <Form {...form}>
            <form id="promptForm" onSubmit={form.handleSubmit(onSubmit)}>
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormInput label={t('Name')} field={field} />
                )}
              />
              <FormField
                control={form.control}
                name="content"
                render={({ field }) => (
                  <FormTextarea label={t('Prompt')} field={field} rows={5} />
                )}
              />

              <div className="mt-4">
                <FormField
                  control={form.control}
                  name="setsTemperature"
                  render={({ field }) => (
                    <FormSwitch field={field} label={t('Sets Temperature')} />
                  )}
                />
                {form.getValues('setsTemperature') && (
                  <TemperatureSlider
                    label={t('Temperature')}
                    labelClassName="text-sm"
                    min={0}
                    max={1}
                    defaultTemperature={
                      form.getValues('temperature') !== null
                        ? form.getValues('temperature')!
                        : DEFAULT_TEMPERATURE
                    }
                    onChangeTemperature={(temperature) =>
                      form.setValue('temperature', temperature)
                    }
                  />
                )}
              </div>

              <div className="flex flex-wrap gap-4 mt-4">
                <FormField
                  control={form.control}
                  name="isDefault"
                  render={({ field }) => (
                    <FormSwitch field={field} label={t('Is Default')} />
                  )}
                />
                {user?.role === UserRole.admin && (
                  <FormField
                    control={form.control}
                    name="isSystem"
                    render={({ field }) => (
                      <FormSwitch field={field} label={t('Is System')} />
                    )}
                  />
                )}
              </div>
            </form>
          </Form>
        </div>

        <DialogFooter className="px-4 py-3 border-t">
          <Button type="submit" form="promptForm" disabled={isSubmitting}>
            {isSubmitting ? t('Saving...') : t('Save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
export default PromptModal;
