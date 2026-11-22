import useTranslation from '@/hooks/useTranslation';

import { PublishedSiteStatus } from '@/types/websites';

export default function WebsiteStatusBadge({
  status,
}: {
  status: PublishedSiteStatus;
}) {
  const { t } = useTranslation();
  switch (status) {
    case PublishedSiteStatus.Publishing:
      return (
        <span className="text-blue-600 dark:text-blue-400">
          {t('Publishing')}
        </span>
      );
    case PublishedSiteStatus.Active:
      return (
        <span className="text-green-600 dark:text-green-400">
          {t('Active')}
        </span>
      );
    case PublishedSiteStatus.Failed:
      return (
        <span className="text-red-600 dark:text-red-400">{t('Failed')}</span>
      );
    case PublishedSiteStatus.Unpublishing:
      return (
        <span className="text-amber-600 dark:text-amber-400">
          {t('Unpublishing')}
        </span>
      );
    case PublishedSiteStatus.Unpublished:
      return <span className="text-muted-foreground">{t('Unpublished')}</span>;
    default:
      return <span>{status}</span>;
  }
}
