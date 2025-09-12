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
import DeletePopover from '@/pages/home/_components/Popover/DeletePopover';

import { getModels, getUserInitialConfig, deleteUserInitialConfig } from '@/apis/adminApis';
import toast from 'react-hot-toast';

export default function UserInitialConfig() {
  const { t } = useTranslation();
  const [isOpenModal, setIsOpenModal] = useState(false);
  const [configList, setConfigList] = useState<GetUserInitialConfigResult[]>(
    [],
  );
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [selectConfig, setSelectConfig] =
    useState<GetUserInitialConfigResult | null>(null);
  const [loading, setLoading] = useState(true);

  const handleShowAddModal = () => {
    setSelectConfig(null);
    setIsOpenModal(true);
  };

  const getConfigs = () => {
    setLoading(true);
    getUserInitialConfig()
      .then((data) => {
        setConfigList(data);
      })
      .catch((error) => {
        console.error('Error fetching configs:', error);
        toast.error(t('Failed to load configurations'));
      })
      .finally(() => {
        setLoading(false);
      });
  };

  useEffect(() => {
    setLoading(true);
    getModels()
      .then((data) => {
        setModels(data.filter((x) => x.enabled === true));
        getConfigs();
      })
      .catch((error) => {
        console.error('Error fetching models:', error);
        toast.error(t('Failed to load models'));
        setLoading(false);
      });
  }, []);

  const NameCell = (config: GetUserInitialConfigResult) => {
    return (
      <TableCell>
        <div className="flex items-center gap-2">
          <div>{config.name}</div>
        </div>
      </TableCell>
    );
  };

  const InitialPriceCell = (config: GetUserInitialConfigResult) => {
    return (
      <TableCell>
        <div className="flex items-center gap-2">
          <div>{toFixed(+config.price)}</div>
        </div>
      </TableCell>
    );
  };

  const Cell = (value: string) => {
    return (
      <TableCell>
        <div className="flex items-center gap-2">
          <div>{value}</div>
        </div>
      </TableCell>
    );
  };

  const handleEditModal = (config: GetUserInitialConfigResult) => {
    setSelectConfig(config);
    setIsOpenModal(true);
  };

  const handleDeleteConfig = async (id: string) => {
    try {
      await deleteUserInitialConfig(id);
      toast.success(t('Delete successful'));
      getConfigs(); // 这会自动设置loading状态
    } catch (error) {
      toast.error(t('Delete failed'));
      console.error('Error deleting config:', error);
    }
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
              <TableHead rowSpan={2}>{t('Model Count')}</TableHead>
              <TableHead rowSpan={2}>{t('Actions')}</TableHead>
            </TableRow>
            <TableRow className="pointer-events-none">
            </TableRow>
          </TableHeader>

          <TableBody 
            isLoading={loading} 
            isEmpty={!loading && configList.length === 0}
          >
            {configList.map((config) => (
              <TableRow 
                key={config.id}
                className="tbody-hover cursor-pointer"
                style={{ borderTop: '1px solid hsl(var(--muted))' }}
                onClick={() => handleEditModal(config)}
              >
                {NameCell(config)}
                {InitialPriceCell(config)}
                {Cell(config.loginType)}
                {Cell(config.invitationCode)}
                <TableCell>{config.models.length}</TableCell>
                <TableCell>
                  <div className="flex gap-1">
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleEditModal(config);
                      }}
                    >
                      ✏️
                    </Button>
                    <div onClick={(e) => e.stopPropagation()}>
                      <DeletePopover
                        onDelete={() => handleDeleteConfig(config.id)}
                      />
                    </div>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
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
