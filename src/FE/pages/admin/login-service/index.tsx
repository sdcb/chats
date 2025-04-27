import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatDateTime } from '@/utils/date';

import { GetLoginServicesResult } from '@/types/adminApis';

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

import LoginServiceModal from '../_components/LoginService/LoginServiceModal';

import { getLoginServices } from '@/apis/adminApis';

export default function LoginService() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [selected, setSelected] = useState<GetLoginServicesResult | null>(null);
  const [services, setServices] = useState<GetLoginServicesResult[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getLoginServices().then((data) => {
      setServices(data);
      setIsOpen(false);
      setSelected(null);
      setLoading(false);
    });
  };

  const handleShow = (item: GetLoginServicesResult) => {
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
          {t('Add Login Service')}
        </Button>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Login Service Type')}</TableHead>
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
                  <div
                    className={`w-2 h-2 rounded-full ${
                      item.enabled ? 'bg-green-400' : 'bg-gray-400'
                    }`}
                  ></div>
                  {item.type}
                </TableCell>
                <TableCell>{formatDateTime(item.createdAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      <LoginServiceModal
        selected={selected}
        types={services.map((x) => x.type)}
        isOpen={isOpen}
        onClose={handleClose}
        onSuccessful={init}
      />
    </>
  );
}
