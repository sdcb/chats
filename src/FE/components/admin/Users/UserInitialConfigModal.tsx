import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { termDateString } from '@/utils/common';
import { formatDate } from '@/utils/date';
import { isEmptyOrJsonObject } from '@/utils/json';
import { getMcpDisplayLabel } from '@/utils/mcp';

import {
  AdminModelDto,
  GetInvitationCodeResult,
  GetUserInitialConfigResult,
  UserInitialMcp,
  UserInitialModel,
} from '@/types/adminApis';
import { McpServerListManagementItemDto } from '@/types/clientApis';
import { feModelProviders } from '@/types/model';
import { LoginType } from '@/types/user';

import { IconPlus } from '@/components/Icons';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuPortal,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Form, FormField } from '@/components/ui/form';
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
import { Textarea } from '@/components/ui/textarea';

import {
  getInvitationCode,
  postUserInitialConfig,
  putUserInitialConfig,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  models: AdminModelDto[];
  mcpServers: McpServerListManagementItemDto[];
  select?: GetUserInitialConfigResult;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}

const UserInitialConfigModal = (props: IProps) => {
  const { t } = useTranslation();
  const { models, mcpServers, isOpen, select, onClose, onSuccessful } = props;
  const [submit, setSubmit] = useState(false);
  const [editModels, setEditModels] = useState<UserInitialModel[]>([]);
  const [allModels, setAllModels] = useState<AdminModelDto[]>([]);
  const [selectedModelIds, setSelectedModelIds] = useState<Set<number>>(
    new Set(),
  );
  const [availableModels, setAvailableModels] = useState<AdminModelDto[]>([]);
  const [editMcps, setEditMcps] = useState<UserInitialMcp[]>([]);
  const [apiKeyEnabled, setApiKeyEnabled] = useState(true);
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
        setEditMcps(select.mcps);
        setApiKeyEnabled(select.apiKeyEnabled);
      } else {
        setEditMcps([]);
        setApiKeyEnabled(true);
      }

      // 设置所有可用模型
      setAllModels(models);
      updateAvailableModels(models, select ? select.models : []);

      // 初始化选中的模型
      if (select) {
        const selectedIds = new Set(select.models.map((m) => m.modelId));
        setSelectedModelIds(selectedIds);
        setEditModels(select.models);
      } else {
        setSelectedModelIds(new Set());
        setEditModels([]);
      }
    }
  }, [isOpen, models, select]);

  const updateAvailableModels = (
    allModelsList: AdminModelDto[],
    currentModels: UserInitialModel[],
  ) => {
    const currentModelIds = new Set(currentModels.map((m) => m.modelId));
    const available = allModelsList.filter(
      (model) => model.enabled && !currentModelIds.has(model.modelId),
    );
    setAvailableModels(available);
  };

  const addModel = (model: AdminModelDto) => {
    const newModel: UserInitialModel = {
      modelId: model.modelId,
      tokens: 0,
      counts: 0,
      expires: termDateString(),
    };
    const newEditModels = [newModel, ...editModels];
    setEditModels(newEditModels);
    const newSelectedIds = new Set(selectedModelIds);
    newSelectedIds.add(model.modelId);
    setSelectedModelIds(newSelectedIds);

    // 更新可用模型列表
    updateAvailableModels(allModels, newEditModels);
  };

  const removeModel = (modelId: number) => {
    const newEditModels = editModels.filter((m) => m.modelId !== modelId);
    setEditModels(newEditModels);
    const newSelectedIds = new Set(selectedModelIds);
    newSelectedIds.delete(modelId);
    setSelectedModelIds(newSelectedIds);

    // 更新可用模型列表
    updateAvailableModels(allModels, newEditModels);
  };

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    const invalidHeaders = editMcps.some(
      (mcp) => !isEmptyOrJsonObject(mcp.customHeaders ?? ''),
    );
    if (invalidHeaders) {
      toast.error(t('Invalid JSON format in MCP custom headers'));
      return;
    }

    setSubmit(true);
    const { name, loginType, price, invitationCodeId } = values;
    let p;
    if (select) {
      p = putUserInitialConfig({
        id: select.id,
        name: name!,
        loginType: loginType!,
        price: Number(price || 0),
        models: editModels,
        mcps: editMcps,
        apiKeyEnabled,
        invitationCodeId: invitationCodeId === '-' ? null : invitationCodeId!,
      });
    } else {
      p = postUserInitialConfig({
        name: name!,
        loginType: loginType!,
        price: Number(price || 0),
        models: editModels,
        mcps: editMcps,
        apiKeyEnabled,
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

  const availableMcpServers = mcpServers.filter(
    (server) => !editMcps.some((mcp) => mcp.mcpServerId === server.id),
  );

  const addMcp = (mcpServerId: number) => {
    setEditMcps((current) => [
      ...current,
      { mcpServerId, showShortcut: false, customHeaders: null },
    ]);
  };

  const updateMcp = (mcpServerId: number, update: Partial<UserInitialMcp>) => {
    setEditMcps((current) =>
      current.map((mcp) =>
        mcp.mcpServerId === mcpServerId ? { ...mcp, ...update } : mcp,
      ),
    );
  };

  const removeMcp = (mcpServerId: number) => {
    setEditMcps((current) =>
      current.filter((mcp) => mcp.mcpServerId !== mcpServerId),
    );
  };

  const onChangeModel = (
    modelId: number,
    type: 'tokens' | 'counts' | 'expires',
    value: any,
  ) => {
    const _models = [...editModels];
    const modelIndex = _models.findIndex((m) => m.modelId === modelId);
    if (modelIndex !== -1) {
      (_models[modelIndex] as any)[type] = value;
      setEditModels(_models);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-4xl max-h-[90vh] flex flex-col">
        <DialogHeader className="flex-shrink-0">
          <DialogTitle>{t('Account Initial Config')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form
            onSubmit={form.handleSubmit(onSubmit)}
            className="flex flex-col flex-1 min-h-0"
          >
            <div className="flex flex-col flex-1 min-h-0 space-y-4 mb-4 overflow-y-auto pr-1">
              {/* 基本信息区域 - 单行四列 */}
              <div className="grid grid-cols-4 gap-3">
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

              <div className="flex items-center justify-between rounded-lg border px-4 py-3">
                <div>
                  <div className="font-medium">{t('API Key')}</div>
                  <div className="text-sm text-muted-foreground">
                    {apiKeyEnabled ? t('Enabled') : t('Disabled')}
                  </div>
                </div>
                <Switch
                  checked={apiKeyEnabled}
                  onCheckedChange={setApiKeyEnabled}
                />
              </div>

              {/* 模型列表区域 */}
              <div>
                <div className="flex justify-between items-center mb-3">
                  <h3 className="text-base font-medium">{t('Models')}</h3>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        type="button"
                        variant="secondary"
                        size="sm"
                        disabled={availableModels.length === 0}
                      >
                        <IconPlus size={16} />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent className="w-40 md:w-52">
                      {availableModels.length === 0 ? (
                        <div className="p-2 mx-1 text-center text-muted-foreground text-sm">
                          {t('No available models')}
                        </div>
                      ) : (
                        <DropdownMenuGroup>
                          {(() => {
                            // 按提供商分组模型
                            const modelGroups = availableModels.reduce(
                              (groups, model) => {
                                const providerId = model.modelProviderId;
                                if (!groups[providerId]) {
                                  groups[providerId] = [];
                                }
                                groups[providerId].push(model);
                                return groups;
                              },
                              {} as Record<number, AdminModelDto[]>,
                            );

                            return Object.entries(modelGroups).map(
                              ([providerId, providerModels]) => {
                                const provider =
                                  feModelProviders[parseInt(providerId)];
                                if (!provider) return null;

                                return (
                                  <DropdownMenuSub key={providerId}>
                                    <DropdownMenuSubTrigger className="p-2 flex gap-2">
                                      <ModelProviderIcon
                                        providerId={parseInt(providerId)}
                                      />
                                      <span className="w-full text-nowrap overflow-hidden text-ellipsis whitespace-nowrap">
                                        {t(provider.name)}
                                      </span>
                                    </DropdownMenuSubTrigger>
                                    <DropdownMenuPortal>
                                      <DropdownMenuSubContent className="max-h-96 overflow-y-auto scroller max-w-[64px] md:max-w-[256px]">
                                        {providerModels.map((model) => (
                                          <DropdownMenuItem
                                            key={model.modelId}
                                            onClick={() => addModel(model)}
                                          >
                                            {model.name}
                                          </DropdownMenuItem>
                                        ))}
                                      </DropdownMenuSubContent>
                                    </DropdownMenuPortal>
                                  </DropdownMenuSub>
                                );
                              },
                            );
                          })()}
                        </DropdownMenuGroup>
                      )}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>

                <div className="border rounded-lg max-h-80 overflow-y-auto">
                  <Table>
                    <TableHeader className="sticky top-0 bg-background z-10">
                      <TableRow>
                        <TableHead>{t('Model Display Name')}</TableHead>
                        <TableHead>{t('Tokens')}</TableHead>
                        <TableHead>{t('Chat Counts')}</TableHead>
                        <TableHead>{t('Expiration Time')}</TableHead>
                        <TableHead className="w-16">{t('Actions')}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {editModels.length === 0 ? (
                        <TableRow>
                          <TableCell
                            colSpan={5}
                            className="text-center py-8 text-muted-foreground"
                          >
                            {t('No models configured')}
                          </TableCell>
                        </TableRow>
                      ) : (
                        editModels.map((model) => {
                          const availableModel = allModels.find(
                            (m) => m.modelId === model.modelId,
                          );
                          return (
                            <TableRow key={model.modelId}>
                              <TableCell>
                                {availableModel?.name || 'Unknown Model'}
                              </TableCell>
                              <TableCell>
                                <Input
                                  className="w-24"
                                  type="number"
                                  value={model.tokens}
                                  onChange={(e) => {
                                    onChangeModel(
                                      model.modelId,
                                      'tokens',
                                      Number(e.target.value),
                                    );
                                  }}
                                />
                              </TableCell>
                              <TableCell>
                                <Input
                                  className="w-24"
                                  type="number"
                                  value={model.counts}
                                  onChange={(e) => {
                                    onChangeModel(
                                      model.modelId,
                                      'counts',
                                      Number(e.target.value),
                                    );
                                  }}
                                />
                              </TableCell>
                              <TableCell>
                                <Popover>
                                  <PopoverTrigger asChild>
                                    <Button
                                      variant="outline"
                                      className="w-[140px] justify-start text-left font-normal"
                                    >
                                      {model.expires &&
                                      model.expires !== '-' ? (
                                        formatDate(model.expires)
                                      ) : (
                                        <span className="text-muted-foreground">
                                          {t('Pick a date')}
                                        </span>
                                      )}
                                    </Button>
                                  </PopoverTrigger>
                                  <PopoverContent
                                    className="w-auto p-0"
                                    align="start"
                                  >
                                    <Calendar
                                      mode="single"
                                      selected={
                                        model.expires
                                          ? new Date(model.expires)
                                          : undefined
                                      }
                                      onSelect={(date) => {
                                        onChangeModel(
                                          model.modelId,
                                          'expires',
                                          date?.toISOString() || '-',
                                        );
                                      }}
                                      initialFocus
                                    />
                                  </PopoverContent>
                                </Popover>
                              </TableCell>
                              <TableCell>
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="ghost"
                                  onClick={() => removeModel(model.modelId)}
                                >
                                  🗑️
                                </Button>
                              </TableCell>
                            </TableRow>
                          );
                        })
                      )}
                    </TableBody>
                  </Table>
                </div>
              </div>

              <div>
                <div className="flex justify-between items-center mb-3">
                  <h3 className="text-base font-medium">{t('MCP')}</h3>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        type="button"
                        variant="secondary"
                        size="sm"
                        disabled={availableMcpServers.length === 0}
                      >
                        <IconPlus size={16} />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent className="w-64">
                      {availableMcpServers.length === 0 ? (
                        <div className="p-2 text-center text-sm text-muted-foreground">
                          {t('No MCP servers found')}
                        </div>
                      ) : (
                        availableMcpServers.map((server) => (
                          <DropdownMenuItem
                            key={server.id}
                            onClick={() => addMcp(server.id)}
                          >
                            {getMcpDisplayLabel(server)}
                          </DropdownMenuItem>
                        ))
                      )}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>

                <div className="border rounded-lg max-h-80 overflow-y-auto">
                  <Table>
                    <TableHeader className="sticky top-0 bg-background z-10">
                      <TableRow>
                        <TableHead>{t('MCP')}</TableHead>
                        <TableHead>{t('Show Shortcut')}</TableHead>
                        <TableHead>{t('Custom Headers (JSON)')}</TableHead>
                        <TableHead className="w-16">{t('Actions')}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {editMcps.length === 0 ? (
                        <TableRow>
                          <TableCell
                            colSpan={4}
                            className="text-center py-8 text-muted-foreground"
                          >
                            {t('No MCP servers found')}
                          </TableCell>
                        </TableRow>
                      ) : (
                        editMcps.map((mcp) => {
                          const server = mcpServers.find(
                            (candidate) => candidate.id === mcp.mcpServerId,
                          );
                          return (
                            <TableRow key={mcp.mcpServerId}>
                              <TableCell>
                                {server
                                  ? getMcpDisplayLabel(server)
                                  : `MCP #${mcp.mcpServerId}`}
                              </TableCell>
                              <TableCell>
                                <Switch
                                  checked={mcp.showShortcut}
                                  onCheckedChange={(checked) =>
                                    updateMcp(mcp.mcpServerId, {
                                      showShortcut: checked,
                                    })
                                  }
                                />
                              </TableCell>
                              <TableCell>
                                <Textarea
                                  value={mcp.customHeaders ?? ''}
                                  placeholder="{}"
                                  rows={2}
                                  className={
                                    !isEmptyOrJsonObject(
                                      mcp.customHeaders ?? '',
                                    )
                                      ? 'border-red-500 focus:border-red-500'
                                      : undefined
                                  }
                                  onChange={(event) =>
                                    updateMcp(mcp.mcpServerId, {
                                      customHeaders: event.target.value || null,
                                    })
                                  }
                                />
                              </TableCell>
                              <TableCell>
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="ghost"
                                  onClick={() => removeMcp(mcp.mcpServerId)}
                                >
                                  🗑️
                                </Button>
                              </TableCell>
                            </TableRow>
                          );
                        })
                      )}
                    </TableBody>
                  </Table>
                </div>
              </div>
            </div>
            <DialogFooter className="flex-shrink-0 pt-3 border-t">
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
