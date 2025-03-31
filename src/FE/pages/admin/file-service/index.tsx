import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { GetFileServicesResult } from '@/types/adminApis';

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

import FileServiceModal from '../_components/Files/FileServiceModal';

import { getFileServices } from '@/apis/adminApis';
import { feFileServiceTypes } from '@/types/file';

export default function FileService() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [selected, setSelected] = useState<GetFileServicesResult | null>(null);
  const [fileServices, setFileServices] = useState<GetFileServicesResult[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getFileServices().then((data) => {
      setFileServices(data);
      setIsOpen(false);
      setSelected(null);
      setLoading(false);
    });
  };

  const handleShow = (item: GetFileServicesResult) => {
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
            {t('Add File Service')}
          </Button>
        </div>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Service Name')}</TableHead>
              <TableHead>{t('File Service Type')}</TableHead>
              <TableHead>{t('File Count')}</TableHead>
              <TableHead>{t('Updated Time')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={fileServices.length === 0}>
            {fileServices.map((item) => (
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
                      item.isDefault ? 'bg-green-400' : 'bg-gray-400'
                    }`}
                  ></div>
                  {item.name}
                </TableCell>
                <TableCell>{t(feFileServiceTypes[item.fileServiceTypeId].name)}</TableCell>
                <TableCell>{item.fileCount}</TableCell>
                <TableCell>
                  {new Date(item.updatedAt).toLocaleString()}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      <FileServiceModal
        selected={selected}
        isOpen={isOpen}
        onClose={handleClose}
        onSuccessful={init}
      />
    </>
  );
}
