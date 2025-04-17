import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { termDateString } from '@/utils/common';

import {
  AdminModelDto,
  GetInvitationCodeResult,
  GetUserInitialConfigResult,
  UserInitialModel,
} from '@/types/adminApis';
import { LoginType } from '@/types/user';

import { IconSquareRoundedX } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form, FormControl, FormField } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import FormSelect from '@/components/ui/form/select';
import { Input } from '@/components/ui/input';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import {
  deleteUserInitialConfig,
  getInvitationCode,
  postUserInitialConfig,
  putUserInitialConfig,
} from '@/apis/adminApis';
import { cn } from '@/lib/utils';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { formatDate } from '@/utils/date';

interface IProps {
  models: AdminModelDto[];
  select?: GetUserInitialConfigResult;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}
let ModelKeyMap = {} as any;
const UserInitialConfigModal = (props: IProps) => {
  const { t } = useTranslation();
  const { models, isOpen, select, onClose, onSuccessful } = props;
  const [submit, setSubmit] = useState(false);
  const [editModels, setEditModels] = useState<UserInitialModel[]>([]);
  const [invitationCodes, setInvitationCodes] = useState<
    GetInvitationCodeResult[]
  >([]);

  const formSchema = z.object({
    name: z
      .string()
      .min(
        2,
        t('Must contain at least {{length}} character(s)', {
          length: 2,
        })!,
      )
      .max(50, t('Contain at most {{length}} character(s)', { length: 50 })!),
    price: z.union([z.string(), z.number()]).optional(),
    loginType: z.string().optional(),
    invitationCodeId: z.string().optional(),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: '',
      price: 0,
      loginType: '-',
      invitationCodeId: '-',
    },
  });

  useEffect(() => {
    if (isOpen) {
      getInvitationCode().then((data) => {
        setInvitationCodes(data);
      });
      form.reset();
      form.formState.isValid;
      if (select) {
        form.setValue('name', select.name);
        form.setValue('price', `${select.price}` || 0);
        form.setValue('loginType', select.loginType);
        form.setValue('invitationCodeId', select.invitationCodeId);
      }
      setEditModels([]);

      const model = models.map((x) => {
        ModelKeyMap[x.modelId] = x.name;
        const model = select?.models.find(
          (model) => model.modelId === x.modelId,
        );
        if (model) return model;
        return {
          modelId: x.modelId,
          tokens: 0,
          counts: 0,
          expires: termDateString(),
          enabled: false,
        };
      });
      setEditModels(model);
    }
  }, [isOpen]);

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    setSubmit(true);
    const { name, loginType, price, invitationCodeId } = values;
    let p;
    if (select) {
      p = putUserInitialConfig({
        id: select.id,
        name: name!,
        loginType: loginType!,
        price: Number(price || 0),
        models: editModels.filter((x) => x.enabled),
        invitationCodeId: invitationCodeId === '-' ? null : invitationCodeId!,
      });
    } else {
      p = postUserInitialConfig({
        name: name!,
        loginType: loginType!,
        price: Number(price || 0),
        models: editModels.filter((x) => x.enabled),
        invitationCodeId: invitationCodeId === '-' ? null : invitationCodeId!,
      });
    }
    p.then(() => {
      toast.success(t('Save successful'));
      onSuccessful();
    }).finally(() => {
      setSubmit(false);
    });
  };

  const onChangeModel = (
    index: number,
    type: 'tokens' | 'counts' | 'expires' | 'enabled',
    value: any,
  ) => {
    const _models = editModels as any;
    _models[index][type] = value;
    setEditModels([..._models]);
  };

  const onDeleteConfig = () => {
    setSubmit(true);
    deleteUserInitialConfig(select!.id)
      .then(() => {
        toast.success(t('Delete successful'));
        onSuccessful();
      })
      .finally(() => {
        setSubmit(false);
      });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('Account Initial Config')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)}>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <FormField
                  key="name"
                  control={form.control}
                  name="name"
                  render={({ field }) => {
                    return <FormInput field={field} label={t('Name')!} />;
                  }}
                ></FormField>
                <FormField
                  key="price"
                  control={form.control}
                  name="price"
                  render={({ field }) => {
                    return (
                      <FormInput field={field} label={t('Initial Price')!} />
                    );
                  }}
                ></FormField>
                <FormField
                  key="loginType"
                  control={form.control}
                  name="loginType"
                  render={({ field }) => {
                    return (
                      <FormSelect
                        className="w-full"
                        field={field}
                        label={t('Login Type')!}
                        items={[
                          { name: '-', value: '-' },
                          ...Object.keys(LoginType).map((key) => ({
                            name: key,
                            value: key,
                          })),
                        ]}
                      />
                    );
                  }}
                ></FormField>
                <FormField
                  key="invitationCodeId"
                  control={form.control}
                  name="invitationCodeId"
                  render={({ field }) => {
                    return (
                      <FormSelect
                        className="w-full"
                        field={field}
                        label={t('Invitation Code')!}
                        items={[
                          { name: '-', value: '-' },
                          ...invitationCodes.map((x) => ({
                            name: x.value,
                            value: x.id.toString(),
                          })),
                        ]}
                      />
                    );
                  }}
                ></FormField>
              </div>
              <div>
                <div className="flex">{t('Models')}</div>
                <div className="h-96 overflow-scroll flex justify-start gap-2 flex-wrap">
                  <Table>
                    <TableHeader>
                      <TableRow className="pointer-events-none">
                        <TableHead>{t('Model Display Name')}</TableHead>
                        <TableHead>{t('Tokens')}</TableHead>
                        <TableHead>{t('Chat Counts')}</TableHead>
                        <TableHead>{t('Expiration Time')}</TableHead>
                        <TableHead>{t('Is Enabled')}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {editModels.map((model, index) => (
                        <TableRow key={model.modelId}>
                          <TableCell>{ModelKeyMap[model.modelId]}</TableCell>
                          <TableCell>
                            <Input
                              className="w-24"
                              value={model.tokens}
                              onChange={(e) => {
                                onChangeModel(index, 'tokens', e.target.value);
                              }}
                            />
                          </TableCell>
                          <TableCell>
                            <Input
                              className="w-24"
                              value={model.counts}
                              onChange={(e) => {
                                onChangeModel(index, 'counts', e.target.value);
                              }}
                            />
                          </TableCell>
                          <TableCell>
                            <Popover>
                              <PopoverTrigger asChild>
                                <FormControl className="flex">
                                  <Button
                                    variant={'outline'}
                                    className={cn(
                                      'pl-3 text-left font-normal w-[132px]',
                                    )}
                                  >
                                    {model.expires ? (
                                      model.expires === '-' ? null : (
                                        formatDate(model.expires)
                                      )
                                    ) : (
                                      <span></span>
                                    )}
                                    <IconSquareRoundedX
                                      onClick={(e) => {
                                        onChangeModel(index, 'expires', '-');
                                        e.preventDefault();
                                      }}
                                      className="z-10 ml-auto h-5 w-5 opacity-50"
                                    />
                                  </Button>
                                </FormControl>
                              </PopoverTrigger>
                              <PopoverContent
                                className="w-auto p-0"
                                align="start"
                              >
                                <Calendar
                                  mode="single"
                                  selected={new Date(model.expires)}
                                  onSelect={(d) => {
                                    onChangeModel(
                                      index,
                                      'expires',
                                      d?.toISOString(),
                                    );
                                  }}
                                  initialFocus
                                />
                              </PopoverContent>
                            </Popover>
                          </TableCell>
                          <TableCell>
                            <Switch
                              checked={model.enabled}
                              onCheckedChange={(checked) => {
                                onChangeModel(index, 'enabled', checked);
                              }}
                            />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </div>
            </div>
            <DialogFooter className="pt-4">
              {select && (
                <Button
                  type="button"
                  disabled={submit}
                  variant="destructive"
                  onClick={onDeleteConfig}
                >
                  {t('Delete')}
                </Button>
              )}
              <Button disabled={submit} type="submit">
                {t('Save')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};
export default UserInitialConfigModal;
