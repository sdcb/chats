import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { formatDateTime } from '@/utils/date';

import { GetModelKeysResult } from '@/types/adminApis';
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

import ConfigModelModal from '../_components/ModelKeys/ConfigModelModal';
import ModelKeysModal from '../_components/ModelKeys/ModelKeysModal';

import { getModelKeys } from '@/apis/adminApis';

interface IQuery {
  modelProviderId: string;
  modelKeyId: string;
}

export default function ModelKeys() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [isOpenConfigModel, setIsOpenConfigModel] = useState(false);
  const [modelKeyId, setModelKeyId] = useState<number>();
  const [selected, setSelected] = useState<GetModelKeysResult | null>(null);
  const [services, setServices] = useState<GetModelKeysResult[]>([]);
  const [filteredServices, setFilteredServices] = useState<
    GetModelKeysResult[]
  >([]);
  const [loading, setLoading] = useState(true);

  const [query, setQuery] = useState<IQuery>({
    modelProviderId: '',
    modelKeyId: '',
  });

  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getModelKeys().then((data) => {
      setServices(data);
      setFilteredServices(data);
      setIsOpen(false);
      setLoading(false);
    });
  };

  const handleShow = (item: GetModelKeysResult) => {
    setSelected(item);
    setIsOpen(true);
  };

  const handleClose = () => {
    setIsOpen(false);
    setIsOpenConfigModel(false);
    setSelected(null);
  };

  const handleQuery = (params: IQuery) => {
    const { modelProviderId, modelKeyId } = params;
    let serviceList = [...services];
    if (modelProviderId) {
      serviceList = serviceList.filter(
        (x) => x.modelProviderId.toString() === modelProviderId,
      );
    }
    if (modelKeyId) {
      serviceList = serviceList.filter((x) => x.id.toString() === modelKeyId);
    }
    setQuery(params);
    setFilteredServices(serviceList);
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
              {services.map((m) => (
                <SelectItem key={m.id} value={m.id.toString()}>
                  {m.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex justify-end gap-3 items-center">
          <Button
            onClick={() => {
              setIsOpen(true);
            }}
            color="primary"
          >
            {t('Add Model Keys')}
          </Button>
        </div>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Key Name')}</TableHead>
              <TableHead>{t('Model Provider')}</TableHead>
              <TableHead>{t('Model Count')}</TableHead>
              <TableHead>{t('Created Time')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody
            isLoading={loading}
            isEmpty={filteredServices.length === 0}
          >
            {filteredServices.map((item) => (
              <TableRow
                className="cursor-pointer"
                key={item.id}
                onClick={() => {
                  handleShow(item);
                }}
              >
                <TableCell className="flex items-center gap-1">
                  {item.name}
                </TableCell>
                <TableCell>
                  <div className="flex gap-1">
                    <ChatIcon
                      className="inline"
                      providerId={item.modelProviderId}
                    />
                    {t(feModelProviders[item.modelProviderId].name)}
                  </div>
                </TableCell>
                <TableCell>
                  {item.enabledModelCount === item.totalModelCount
                    ? `${item.totalModelCount}`
                    : `${item.enabledModelCount}/${item.totalModelCount}`}
                </TableCell>
                <TableCell>{formatDateTime(item.createdAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      {
        <ModelKeysModal
          selected={selected}
          isOpen={isOpen}
          onClose={handleClose}
          onConfigModel={(id) => {
            setModelKeyId(id);
            setIsOpenConfigModel(true);
          }}
          onSaveSuccessful={() => {
            toast.success(t('Save successful'));
            init();
          }}
          onDeleteSuccessful={init}
        />
      }
      <ConfigModelModal
        modelKeyId={modelKeyId!}
        modelProverId={selected?.modelProviderId!}
        isOpen={isOpenConfigModel}
        onClose={handleClose}
        onSuccessful={init}
      />
    </>
  );
}
