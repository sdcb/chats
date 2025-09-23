import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { GetInvitationCodeResult } from '@/types/adminApis';

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

import InvitationCodeModal from '@/components/admin/InvitationCode/InvitationCodeModal';

import { getInvitationCode } from '@/apis/adminApis';

export default function InvitationCode() {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [selected, setSelected] = useState<GetInvitationCodeResult | null>(
    null,
  );
  const [codes, setCodes] = useState<GetInvitationCodeResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState(false);
  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getInvitationCode().then((data) => {
      setCodes(data);
      setIsOpen(false);
      setLoading(false);
    });
  };

  const handleClose = () => {
    setIsOpen(false);
  };

  const handleShow = (item: GetInvitationCodeResult) => {
    setSelected(item);
    setIsOpen(true);
  };

  return (
    <>
      <div className="flex gap-4 mb-4">
        <Button
          onClick={() => {
            setSelected(null);
            setIsOpen(true);
          }}
          color="primary"
        >
          {t('Add Invitation Code')}
        </Button>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Invitation Code')}</TableHead>
              <TableHead>{t('Use count')}</TableHead>
              <TableHead>{t('Created User')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={codes.length === 0}>
            {codes.map((item) => (
              <TableRow
                className="cursor-pointer"
                key={item.id}
                onClick={(e) => {
                  handleShow(item);
                  e.stopPropagation();
                }}
              >
                <TableCell>{item.value}</TableCell>
                <TableCell>{item.count}</TableCell>
                <TableCell>{item.username}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
      <InvitationCodeModal
        selected={selected}
        isOpen={isOpen}
        onClose={handleClose}
        onSuccessful={init}
      />
    </>
  );
}
