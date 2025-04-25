import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import {
  AdminModelDto,
  GetModelKeysResult,
  SimpleModelReferenceDto,
} from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import AddModelModal from '../_components/Models/AddModelModal';
import EditModelModal from '../_components/Models/EditModelModal';

import {
  getModelKeys,
  getModelProviderModels,
  getModels,
} from '@/apis/adminApis';
import { cn } from '@/lib/utils';

interface IQuery {
  modelProviderId: string;
  modelKeyId: string;
  enabled: string;
}

export default function Models() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState({ add: false, edit: false });
  const [selectedModel, setSelectedModel] = useState<AdminModelDto>();
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [filteredModels, setFilteredModels] = useState<AdminModelDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [modelKeys, setModelKeys] = useState<GetModelKeysResult[]>([]);
  const [modelVersions, setModelVersions] = useState<SimpleModelReferenceDto[]>(
    [],
  );
  const [query, setQuery] = useState<IQuery>({
    modelProviderId: '',
    modelKeyId: '',
    enabled: '',
  });

  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getModels()
      .then((data) => {
        setModels(data);
        setFilteredModels(data);
        setIsOpen({ add: false, edit: false });
        setSelectedModel(undefined);
        setLoading(false);
        handleQuery(query, data);
      })
    getModelKeys().then((data) => {
      setModelKeys(data);
    });
  };

  const showEditDialog = (item: AdminModelDto) => {
    getModelProviderModels(item.modelProviderId).then((possibleModels) => {
      setModelVersions(possibleModels);
      setSelectedModel(item);
      setIsOpen({ ...isOpen, edit: true });
    });
  };

  const showCreateDialog = () => {
    setIsOpen({ ...isOpen, add: true });
  };

  const handleClose = () => {
    setIsOpen({ add: false, edit: false });
    setSelectedModel(undefined);
  };

  const handleQuery = (params: IQuery, modelData?: AdminModelDto[]) => {
    const { modelProviderId, modelKeyId, enabled } = params;
    let modelList = [...(modelData || models)];
    if (modelProviderId) {
      modelList = modelList.filter(
        (x) => x.modelProviderId.toString() === modelProviderId,
      );
    }
    if (modelKeyId) {
      modelList = modelList.filter(
        (x) => x.modelKeyId.toString() === modelKeyId,
      );
    }
    if (enabled !== '') {
      modelList = modelList.filter((x) => x.enabled.toString() === enabled);
    }
    setQuery(params);
    setFilteredModels(modelList);
  };

  return (
    <>
      <div className="flex gap-4 mb-4 flex-warp">
        <div className="flex flex-wrap gap-3">
          <Select
            value={query.modelProviderId}
            onValueChange={(value) => {
              const params = { ...query, modelProviderId: value };
              handleQuery(params);
            }}
          >
            <SelectTrigger
              className="w-48"
              value={query.modelProviderId}
              onReset={() => {
                const params = { ...query, modelProviderId: '' };
                handleQuery(params);
              }}
            >
              <SelectValue placeholder={t('Select an Model Provider')} />
            </SelectTrigger>
            <SelectContent>
              {feModelProviders.map((m) => (
                <SelectItem key={m.id} value={m.id.toString()}>
                  {m.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            value={query.modelKeyId}
            onValueChange={(value) => {
              const params = { ...query, modelKeyId: value };
              handleQuery(params);
            }}
          >
            <SelectTrigger
              value={query.modelKeyId}
              className="w-48"
              onReset={() => {
                const params = { ...query, modelKeyId: '' };
                handleQuery(params);
              }}
            >
              <SelectValue placeholder={t('Select an Model Key')} />
            </SelectTrigger>
            <SelectContent>
              {modelKeys.map((m) => (
                <SelectItem key={m.id} value={m.id.toString()}>
                  {m.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            value={query.enabled}
            onValueChange={(value) => {
              const params = { ...query, enabled: value };
              handleQuery(params);
            }}
          >
            <SelectTrigger
              value={query.enabled}
              className="w-48"
              onReset={() => {
                const params = { ...query, enabled: '' };
                handleQuery(params);
              }}
            >
              <SelectValue placeholder={t('Is it enabled')} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem key="enabled-true" value={'true'}>
                {t('Yes')}
              </SelectItem>
              <SelectItem key="enabled-false" value={'false'}>
                {t('No')}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="flex justify-end gap-3 items-center">
          <Button
            onClick={() => {
              showCreateDialog();
            }}
            color="primary"
          >
            {t('Add Model')}
          </Button>
        </div>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-20">{t('Rank')}</TableHead>
              <TableHead>{t('Model Display Name')}</TableHead>
              <TableHead>{t('Model Key')}</TableHead>
              <TableHead>{t('Model Version')}</TableHead>
              <TableHead>{t('Token Price')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={filteredModels.length === 0}>
            {filteredModels.map((item) => (
              <TableRow
                className="cursor-pointer"
                key={item.modelId}
                onClick={() => showEditDialog(item)}
              >
                <TableCell>{item.rank}</TableCell>
                <TableCell>
                  <div className="flex items-center gap-1">
                    <div
                      className={cn(
                        'border-2 rounded-full',
                        item.enabled ? 'border-green-500' : 'border-gray-400',
                      )}
                    >
                      <ChatIcon
                        className="h-6 w-6 p-0.5"
                        providerId={item.modelProviderId}
                      />
                    </div>
                    {item.name}
                  </div>
                </TableCell>
                <TableCell>
                  {modelKeys.find((k) => k.id === item.modelKeyId)?.name}
                </TableCell>
                <TableCell>{item.modelReferenceName}</TableCell>
                <TableCell>
                  {'￥' +
                    formatNumberAsMoney(item.inputTokenPrice1M) +
                    '/' +
                    formatNumberAsMoney(item.outputTokenPrice1M)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      {isOpen.edit && (
        <EditModelModal
          selected={selectedModel!}
          modelKeys={modelKeys}
          modelVersionList={modelVersions}
          isOpen={isOpen.edit}
          onClose={handleClose}
          onSuccessful={init}
        />
      )}
      {isOpen.add && (
        <AddModelModal
          modelKeys={modelKeys}
          isOpen={isOpen.add}
          onClose={handleClose}
          onSuccessful={init}
        />
      )}
    </>
  );
}
