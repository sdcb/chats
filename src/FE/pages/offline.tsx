import Head from 'next/head';

import useTranslation from '@/hooks/useTranslation';

const OfflinePage = () => {
  const { t } = useTranslation();

  return (
    <>
      <Head>
        <title>Chats</title>
      </Head>
      <main className="flex h-screen flex-col items-center justify-center gap-y-4 px-6 text-center">
        <h1 className="text-2xl">{t('You are offline')}</h1>
        <p className="text-muted-foreground">
          {t('Please check your network connection and try again.')}
        </p>
        <button
          className="rounded-md bg-primary px-4 py-2 text-sm text-primary-foreground"
          onClick={() => window.location.reload()}
        >
          {t('Retry')}
        </button>
      </main>
    </>
  );
};

export default OfflinePage;
