import { useMemo } from 'react';

import { clearSmsAttempts, getSmsAttempts } from '@/apis/adminApis';
import SecurityLogPanel from './SecurityLogPanel';
import { SmsAttemptLog } from '@/types/adminApis';
import useTranslation from '@/hooks/useTranslation';
import { formatDateTime } from '@/utils/date';

const SmsAttemptsPanel = () => {
  const { t } = useTranslation();

  const columns = useMemo(
    () => [
      {
        header: t('Phone Number'),
        cell: (row: SmsAttemptLog) => row.phoneNumber,
      },
      {
        header: t('Code'),
        cell: (row: SmsAttemptLog) => row.code,
      },
      {
        header: t('User Name'),
        cell: (row: SmsAttemptLog) => row.userName || '-',
      },
      {
        header: t('Type'),
        cell: (row: SmsAttemptLog) => row.type || '-',
      },
      {
        header: t('Status'),
        cell: (row: SmsAttemptLog) => row.status || '-',
      },
      {
        header: t('IP Address'),
        cell: (row: SmsAttemptLog) => row.ip,
      },
      {
        header: t('User Agent'),
        className: 'max-w-sm truncate',
        cell: (row: SmsAttemptLog) => row.userAgent,
      },
      {
        header: t('Created Time'),
        cell: (row: SmsAttemptLog) => formatDateTime(row.createdAt),
      },
    ],
    [t],
  );

  return (
    <SecurityLogPanel<SmsAttemptLog>
      tab="sms"
      fetchList={getSmsAttempts}
      clearList={clearSmsAttempts}
      exportUrl="/api/admin/security-logs/sms-attempts/export"
      columns={columns}
      getRowKey={(row) => row.id}
    />
  );
};

export default SmsAttemptsPanel;
