import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';
import { formatDate } from '@/utils/date';

import { AdminModelDto, GetUserInitialConfigResult } from '@/types/adminApis';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import UserInitialConfigModal from '../_components/Users/UserInitialConfigModal';

import { getModels, getUserInitialConfig } from '@/apis/adminApis';

export default function UserInitialConfig() {
  const { t } = useTranslation();
  const [isOpenModal, setIsOpenModal] = useState(false);
  const [configList, setConfigList] = useState<GetUserInitialConfigResult[]>(
    [],
  );
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [selectConfig, setSelectConfig] =
    useState<GetUserInitialConfigResult | null>(null);

  const handleShowAddModal = () => {
    setSelectConfig(null);
    setIsOpenModal(true);
  };

  const getConfigs = () => {
    getUserInitialConfig().then((data) => {
      setConfigList(data);
    });
  };

  useEffect(() => {
    getModels().then((data) => {
      setModels(data.filter((x) => x.enabled === true));
      getConfigs();
    });
  }, []);

  const NameCell = (
    config: GetUserInitialConfigResult,
    rowSpan: number = 1,
  ) => {
    return (
      <TableCell rowSpan={rowSpan}>
        <div className="flex items-center gap-2">
          <div>{config.name}</div>
        </div>
      </TableCell>
    );
  };

  const InitialPriceCell = (
    config: GetUserInitialConfigResult,
    rowSpan: number = 1,
  ) => {
    return (
      <TableCell rowSpan={rowSpan}>
        <div className="flex items-center gap-2">
          <div>{toFixed(+config.price)}</div>
        </div>
      </TableCell>
    );
  };

  const Cell = (value: string, rowSpan: number = 1) => {
    return (
      <TableCell rowSpan={rowSpan}>
        <div className="flex items-center gap-2">
          <div>{value}</div>
        </div>
      </TableCell>
    );
  };

  const ModelCell = (value: any) => {
    return <TableCell>{value}</TableCell>;
  };

  const handleEditModal = (config: GetUserInitialConfigResult) => {
    setSelectConfig(config);
    setIsOpenModal(true);
  };

  return (
    <>
      <div className="flex gap-4 mb-4">
        <Button
          onClick={() => {
            handleShowAddModal();
          }}
          color="primary"
        >
          {t('Add Account Initial Config')}
        </Button>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow className="pointer-events-none">
              <TableHead rowSpan={2}>{t('Name')}</TableHead>
              <TableHead
                rowSpan={2}
                style={{ borderRight: '1px solid hsl(var(--muted))' }}
              >
                {t('Initial Price')}
              </TableHead>
              <TableHead
                rowSpan={2}
                style={{ borderRight: '1px solid hsl(var(--muted))' }}
              >
                {t('Login Type')}
              </TableHead>
              <TableHead
                rowSpan={2}
                style={{ borderRight: '1px solid hsl(var(--muted))' }}
              >
                {t('Invitation Code')}
              </TableHead>
              <TableHead colSpan={4} className="text-center">
                {t('Models')}
              </TableHead>
            </TableRow>
            <TableRow className="pointer-events-none">
              <TableHead>{t('Model Display Name')}</TableHead>
              <TableHead>{t('Tokens')}</TableHead>
              <TableHead>{t('Chat Counts')}</TableHead>
              <TableHead>{t('Expiration Time')}</TableHead>
            </TableRow>
          </TableHeader>

          {configList.map((config) => (
            <TableBody
              key={config.id}
              className="tbody-hover"
              style={{ borderTop: '1px solid hsl(var(--muted))' }}
            >
              {config.models.length > 0 ? (
                config.models.map((model, index) => {
                  return (
                    <TableRow
                      onClick={() => handleEditModal(config)}
                      key={model.modelId}
                      className={`${
                        index !== config.models.length - 1 && 'border-none'
                      }`}
                    >
                      {index === 0 && NameCell(config, config.models.length)}
                      {index === 0 &&
                        InitialPriceCell(config, config.models.length)}
                      {index === 0 &&
                        Cell(config.loginType, config.models.length)}
                      {index === 0 &&
                        Cell(config.invitationCode, config.models.length)}
                      {ModelCell(
                        models.find((x) => x.modelId === model.modelId)?.name,
                      )}
                      {ModelCell(model.tokens)}
                      {ModelCell(model.counts)}
                      {ModelCell(formatDate(model.expires))}
                    </TableRow>
                  );
                })
              ) : (
                <TableRow
                  key={config.id}
                  onClick={() => handleEditModal(config)}
                >
                  {NameCell(config, config.models.length)}
                  {InitialPriceCell(config, config.models.length)}
                  {ModelCell(config.loginType)}
                  {ModelCell(config.invitationCode)}
                  <TableCell colSpan={4}></TableCell>
                </TableRow>
              )}
            </TableBody>
          ))}
        </Table>

        {configList.length === 0 && (
          <div className="flex p-4 h-32 justify-center items-center text-sm w-full text-muted-foreground">
            {t('No data')}
          </div>
        )}
      </Card>
      <UserInitialConfigModal
        models={models}
        select={selectConfig || undefined}
        onClose={() => {
          setIsOpenModal(false);
        }}
        onSuccessful={() => {
          getConfigs();
          setIsOpenModal(false);
        }}
        isOpen={isOpenModal}
      />
    </>
  );
}
