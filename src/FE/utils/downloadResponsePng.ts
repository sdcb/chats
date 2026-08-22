let snapdomModulePromise:
  | Promise<typeof import('@zumer/snapdom')>
  | undefined;

const loadSnapdom = () => {
  snapdomModulePromise ??= import('@zumer/snapdom');
  return snapdomModulePromise;
};

const waitForImages = async (element: HTMLElement) => {
  const images = Array.from(element.querySelectorAll('img'));
  await Promise.all(
    images.map(async (image) => {
      if (!image.complete) {
        await new Promise<void>((resolve) => {
          const finish = () => {
            image.removeEventListener('load', finish);
            image.removeEventListener('error', finish);
            resolve();
          };
          image.addEventListener('load', finish, { once: true });
          image.addEventListener('error', finish, { once: true });
        });
      }

      if (image.decode) {
        await image.decode().catch(() => undefined);
      }
    }),
  );
};

const waitForLayout = () =>
  new Promise<void>((resolve) => {
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
  });

const validateRasterSize = (element: HTMLElement) => {
  const width = Math.ceil(element.scrollWidth || element.getBoundingClientRect().width);
  const height = Math.ceil(element.scrollHeight || element.getBoundingClientRect().height);
  const maxSide = 16384;
  const maxArea = 268_000_000;

  if (width > maxSide || height > maxSide || width * height > maxArea) {
    throw new Error('Response is too tall to export as one PNG');
  }
};

export const downloadResponsePng = async (
  element: HTMLElement,
  filename: string,
) => {
  if (typeof document === 'undefined') {
    throw new Error('PNG export is only available in the browser');
  }

  await document.fonts.ready;
  await waitForImages(element);
  await waitForLayout();
  validateRasterSize(element);

  const { snapdom } = await loadSnapdom();
  const blob = await snapdom.toBlob(element, {
    type: 'png',
    dpr: 1,
    embedFonts: false,
    exclude: ['[data-export-ignore]'],
  });

  if (!blob) {
    throw new Error('SnapDOM returned an empty image');
  }

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.style.display = 'none';
  anchor.setAttribute('aria-hidden', 'true');
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};
