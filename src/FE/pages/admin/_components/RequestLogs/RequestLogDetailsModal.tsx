import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatDate, formatDateTime } from '@/utils/date';

import { GetRequestLogsDetailsResult } from '@/types/adminApis';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import { getRequestLogDetails } from '@/apis/adminApis';

interface IProps {
  requestLogId?: string;
  isOpen: boolean;
  onClose: () => void;
}

const RequestLogDetailsModal = (props: IProps) => {
  const { t } = useTranslation();
  const { isOpen, onClose } = props;
  const [log, setLog] = useState<GetRequestLogsDetailsResult>();

  useEffect(() => {
    if (isOpen) {
      getRequestLogDetails(props.requestLogId!).then((data) => {
        setLog(data);
      });
    }
  }, [isOpen]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      {isOpen && (
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('Request Logs Details')}</DialogTitle>
            <div className="pt-6 max-h-[60vh] overflow-scroll">
              <dl className="grid gap-3">
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Url')}：
                  </dt>
                  <dd>{log?.url}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Status Code')}：
                  </dt>
                  <dd>{log?.statusCode}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Method')}：
                  </dt>
                  <dd>{log?.method}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('User Name')}：
                  </dt>
                  <dd>{log?.user?.username}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('IP Address')}：
                  </dt>
                  <dd>{log?.ip}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Response Time')}：
                  </dt>
                  <dd>
                    {+(log?.responseTime || 0) - +(log?.requestTime || 0)}
                    {t('ms')}
                  </dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Created Time')}：
                  </dt>
                  <dd>{formatDateTime(log?.createdAt || '')}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Request Headers')}：
                  </dt>
                  <dd>{log?.headers}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Request')}：
                  </dt>
                  <dd>{log?.request}</dd>
                </div>
                <div className="flex items-center">
                  <dt className="text-muted-foreground min-w-[80px]">
                    {t('Response')}：
                  </dt>
                  <dd>{log?.response}</dd>
                </div>
              </dl>
            </div>
          </DialogHeader>
        </DialogContent>
      )}
    </Dialog>
  );
};
export default RequestLogDetailsModal;
