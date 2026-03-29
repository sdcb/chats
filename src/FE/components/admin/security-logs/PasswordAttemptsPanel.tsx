import { useMemo } from 'react';

import {
  clearPasswordAttempts,
  getPasswordAttempts,
} from '@/apis/adminApis';
import SecurityLogPanel from './SecurityLogPanel';
import { PasswordAttemptLog } from '@/types/adminApis';
import useTranslation from '@/hooks/useTranslation';
import { formatDateTime } from '@/utils/date';

const PasswordAttemptsPanel = () => {
  const { t } = useTranslation();

  const columns = useMemo(
    () => [
      {
        key: 'userName',
        title: t('Recorded Username'),
        cell: (row: PasswordAttemptLog) => row.userName,
      },
      {
        key: 'matchedUserName',
        title: t('Bound User'),
        cell: (row: PasswordAttemptLog) => row.matchedUserName || '-',
      },
      {
        key: 'result',
        title: t('Result'),
        cell: (row: PasswordAttemptLog) =>
          row.isSuccessful ? t('Yes') : t('No'),
      },
      {
        key: 'failureReason',
        title: t('Failure Reason'),
        className: 'max-w-xs truncate',
        cell: (row: PasswordAttemptLog) => row.failureReason || '-',
      },
      {
        key: 'ip',
        title: t('IP Address'),
        cell: (row: PasswordAttemptLog) => row.ip,
      },
      {
        key: 'userAgent',
        title: t('User Agent'),
        className: 'max-w-sm truncate',
        cell: (row: PasswordAttemptLog) => row.userAgent,
      },
      {
        key: 'createdAt',
        title: t('Created Time'),
        cell: (row: PasswordAttemptLog) => formatDateTime(row.createdAt),
      },
    ],
    [t],
  );

  return (
    <SecurityLogPanel<PasswordAttemptLog>
      tab="password"
      fetchList={getPasswordAttempts}
      clearList={clearPasswordAttempts}
      exportUrl="/api/admin/security-logs/password-attempts/export"
      columns={columns}
      getRowKey={(row) => row.id}
    />
  );
};

export default PasswordAttemptsPanel;
