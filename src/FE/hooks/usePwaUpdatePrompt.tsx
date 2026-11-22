import { useEffect } from 'react';
import toast from 'react-hot-toast';

import { translate as t } from '@/hooks/useTranslation';

const UPDATE_CHECK_INTERVAL_MS = 60 * 60 * 1000;
const TOAST_ID = 'pwa-update';

const showUpdateToast = (waiting: ServiceWorker, onAccept: () => void) => {
  toast(
    (tst) => (
      <div className="flex items-center gap-3">
        <span>{t('A new version of Chats is available')}</span>
        <button
          className="shrink-0 rounded-md bg-primary px-3 py-1 text-sm text-primary-foreground"
          onClick={() => {
            toast.dismiss(tst.id);
            onAccept();
            waiting.postMessage({ type: 'SKIP_WAITING' });
          }}
        >
          {t('Refresh')}
        </button>
        <button
          className="shrink-0 text-sm text-muted-foreground"
          onClick={() => toast.dismiss(tst.id)}
        >
          {t('Later')}
        </button>
      </div>
    ),
    { id: TOAST_ID, duration: Infinity },
  );
};

/**
 * Prompts the user to activate a newly installed service worker instead of
 * swapping it in silently, so open pages never lose chunks mid-session.
 */
const usePwaUpdatePrompt = () => {
  useEffect(() => {
    if (!('serviceWorker' in navigator)) return;

    let accepted = false;
    let timer: ReturnType<typeof setInterval> | undefined;
    let disposed = false;

    const onControllerChange = () => {
      if (accepted) window.location.reload();
    };
    navigator.serviceWorker.addEventListener(
      'controllerchange',
      onControllerChange,
    );

    navigator.serviceWorker.getRegistration().then((reg) => {
      if (!reg || disposed) return;

      const promptIfWaiting = () => {
        // No controller means first install: nothing to update from.
        if (reg.waiting && navigator.serviceWorker.controller) {
          showUpdateToast(reg.waiting, () => {
            accepted = true;
          });
        }
      };

      promptIfWaiting();
      reg.addEventListener('updatefound', () => {
        const installing = reg.installing;
        installing?.addEventListener('statechange', () => {
          if (installing.state === 'installed') promptIfWaiting();
        });
      });

      timer = setInterval(() => {
        reg.update().catch(() => {});
      }, UPDATE_CHECK_INTERVAL_MS);
    });

    return () => {
      disposed = true;
      if (timer) clearInterval(timer);
      navigator.serviceWorker.removeEventListener(
        'controllerchange',
        onControllerChange,
      );
    };
  }, []);
};

export default usePwaUpdatePrompt;
