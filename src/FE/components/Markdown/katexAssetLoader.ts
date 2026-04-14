let katexStylesPromise: Promise<void> | null = null;

const KATEX_STYLE_ID = 'katex-on-demand-styles';
const KATEX_STYLE_HREF = '/vendor/katex/katex.min.css';

export const ensureKatexStylesLoaded = () => {
  if (typeof document === 'undefined') {
    return Promise.resolve();
  }

  if (document.getElementById(KATEX_STYLE_ID)) {
    return Promise.resolve();
  }

  if (!katexStylesPromise) {
    katexStylesPromise = new Promise<void>((resolve, reject) => {
      const link = document.createElement('link');
      link.id = KATEX_STYLE_ID;
      link.rel = 'stylesheet';
      link.href = KATEX_STYLE_HREF;
      link.onload = () => resolve();
      link.onerror = () =>
        reject(new Error(`Failed to load KaTeX styles from ${KATEX_STYLE_HREF}`));
      document.head.appendChild(link);
    }).catch((error) => {
      katexStylesPromise = null;
      throw error;
    });
  }

  return katexStylesPromise;
};
