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
        header: t('Provider'),
        cell: (row: KeycloakAttemptLog) => row.provider,
      },
      {
        header: t('Subject'),
        className: 'max-w-xs truncate',
        cell: (row: KeycloakAttemptLog) => row.sub || '-',
      },
      {
        header: t('E-Mail'),
        className: 'max-w-xs truncate',
        cell: (row: KeycloakAttemptLog) => row.email || '-',
      },
      {
        header: t('User Name'),
        cell: (row: KeycloakAttemptLog) => row.userName || '-',
      },
      {
        header: t('Result'),
        cell: (row: KeycloakAttemptLog) =>
          row.isSuccessful ? t('Yes') : t('No'),
      },
      {
        header: t('Failure Reason'),
        className: 'max-w-xs truncate',
        cell: (row: KeycloakAttemptLog) => row.failureReason || '-',
      },
      {
        header: t('IP Address'),
        cell: (row: KeycloakAttemptLog) => row.ip,
      },
      {
        header: t('User Agent'),
        className: 'max-w-sm truncate',
        cell: (row: KeycloakAttemptLog) => row.userAgent,
      },
      {
        header: t('Created Time'),
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
