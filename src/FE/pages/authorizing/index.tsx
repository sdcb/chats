import { useEffect, useRef } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { saveUserInfo, setUserSession } from '@/utils/user';

import { singIn } from '@/apis/clientApis';

export default function Authorizing() {
  const { t } = useTranslation();
  const router = useRouter();
  const hasStartedRef = useRef(false);

  useEffect(() => {
    if (!router.isReady || hasStartedRef.current) {
      return;
    }
    hasStartedRef.current = true;
    const { code, provider } = router.query as {
      code: string;
      provider: string;
    };
    if (!code) {
      router.push('/login');
      return;
    }
    singIn({
      code,
      provider,
    }).then((response) => {
      setUserSession(response.sessionId);
      saveUserInfo({
        ...response,
      });
      router.push('/');
    });
  }, [router]);
  return (
    <>
      {router.isReady && (
        <div className="w-full text-center mt-8 text-gray-600">
          {t('Logging in...')}
        </div>
      )}
    </>
  );
}
