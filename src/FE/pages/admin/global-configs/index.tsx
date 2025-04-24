import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { GetConfigsResult } from '@/types/adminApis';

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

import GlobalConfigsModal from '../_components/GlobalConfigs/GlobalConfigsModal';

import { getConfigs } from '@/apis/adminApis';

export default function Configs() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [selected, setSelected] = useState<GetConfigsResult | null>(null);
  const [configs, setConfigs] = useState<GetConfigsResult[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getConfigs().then((data) => {
      setConfigs(data);
      setIsOpen(false);
      setSelected(null);
      setLoading(false);
    });
  };

  const handleShow = (item: GetConfigsResult) => {
    setSelected(item);
    setIsOpen(true);
  };

  const handleClose = () => {
    setIsOpen(false);
    setSelected(null);
  };

  return (
    <>
      <div className="flex gap-4 mb-4">
        <Button
          onClick={() => {
            setIsOpen(true);
          }}
          color="primary"
        >
          {t('Add Configs')}
        </Button>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Key')}</TableHead>
              <TableHead>{t('Value')}</TableHead>
              <TableHead>{t('Description')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={configs.length === 0}>
            {configs.map((item) => (
              <TableRow
                className="cursor-pointer"
                key={item.key}
                onClick={() => {
                  handleShow(item);
                }}
              >
                <TableCell>{item.key}</TableCell>
                <TableCell className="max-w-[200px]">{item.value}</TableCell>
                <TableCell className="max-w-[200px]">
                  {item.description}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      <GlobalConfigsModal
        configKeys={selected ? [] : configs.map((x) => x.key)}
        selected={selected}
        isOpen={isOpen}
        onClose={handleClose}
        onSuccessful={init}
      />
    </>
  );
}
