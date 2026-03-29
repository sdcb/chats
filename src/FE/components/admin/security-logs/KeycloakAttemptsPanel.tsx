import { useMemo } from 'react';

import { clearKeycloakAttempts, getKeycloakAttempts } from '@/apis/adminApis';
import SecurityLogPanel from './SecurityLogPanel';
import { KeycloakAttemptLog } from '@/types/adminApis';
import useTranslation from '@/hooks/useTranslation';
import { formatDateTime } from '@/utils/date';

const KeycloakAttemptsPanel = () => {
  const { t } = useTranslation();

  const columns = useMemo(
    () => [
      {
        key: 'provider',
        title: t('Provider'),
        cell: (row: KeycloakAttemptLog) => row.provider,
      },
      {
        key: 'sub',
        title: t('Subject'),
        className: 'max-w-xs truncate',
        cell: (row: KeycloakAttemptLog) => row.sub || '-',
      },
      {
        key: 'email',
        title: t('E-Mail'),
        className: 'max-w-xs truncate',
        cell: (row: KeycloakAttemptLog) => row.email || '-',
      },
      {
        key: 'userName',
        title: t('User Name'),
        cell: (row: KeycloakAttemptLog) => row.userName || '-',
      },
      {
        key: 'result',
        title: t('Result'),
        cell: (row: KeycloakAttemptLog) =>
          row.isSuccessful ? t('Yes') : t('No'),
      },
      {
        key: 'failureReason',
        title: t('Failure Reason'),
        className: 'max-w-xs truncate',
        cell: (row: KeycloakAttemptLog) => row.failureReason || '-',
      },
      {
        key: 'ip',
        title: t('IP Address'),
        cell: (row: KeycloakAttemptLog) => row.ip,
      },
      {
        key: 'userAgent',
        title: t('User Agent'),
        className: 'max-w-sm truncate',
        cell: (row: KeycloakAttemptLog) => row.userAgent,
      },
      {
        key: 'createdAt',
        title: t('Created Time'),
        cell: (row: KeycloakAttemptLog) => formatDateTime(row.createdAt),
      },
    ],
    [t],
  );

  return (
    <SecurityLogPanel<KeycloakAttemptLog>
      tab="keycloak"
      fetchList={getKeycloakAttempts}
      clearList={clearKeycloakAttempts}
      exportUrl="/api/admin/security-logs/keycloak-attempts/export"
      columns={columns}
      getRowKey={(row) => row.id}
    />
  );
};

export default KeycloakAttemptsPanel;
