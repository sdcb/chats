import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { GetModelKeysResult } from '@/types/adminApis';

import { ModelKeysModal } from '@/components/Admin/ModelKeys/ModelKeysModal';
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

import { getModelKeys } from '@/apis/adminApis';
import { feModelProviders } from '@/types/model';

export default function ModelKeys() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [selected, setSelected] = useState<GetModelKeysResult | null>(null);
  const [services, setServices] = useState<GetModelKeysResult[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getModelKeys().then((data) => {
      setServices(data);
      setIsOpen(false);
      setSelected(null);
      setLoading(false);
    });
  };

  const handleShow = (item: GetModelKeysResult) => {
    setSelected(item);
    setIsOpen(true);
  };

  const handleClose = () => {
    setIsOpen(false);
    setSelected(null);
  };

  return (
    <>
      <div className="flex flex-col gap-4 mb-4">
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
          <TableBody isLoading={loading} isEmpty={services.length === 0}>
            {services.map((item) => (
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
                  {t(feModelProviders[item.modelProviderId].name)}
                </TableCell>
                <TableCell>
                  {item.enabledModelCount === item.totalModelCount
                    ? `${item.totalModelCount}`
                    : `${item.enabledModelCount}/${item.totalModelCount}`}
                </TableCell>
                <TableCell>
                  {new Date(item.createdAt).toLocaleString()}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      {<ModelKeysModal
        selected={selected}
        isOpen={isOpen}
        onClose={handleClose}
        onSuccessful={init}
      />
      }
    </>
  );
}