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

const MAX_RASTER_SIDE = 16384;
const MAX_RASTER_AREA = 268_000_000;

const getCaptureDpr = (element: HTMLElement) => {
  const width = Math.max(
    1,
    Math.ceil(element.scrollWidth || element.getBoundingClientRect().width),
  );
  const height = Math.max(
    1,
    Math.ceil(element.scrollHeight || element.getBoundingClientRect().height),
  );
  const requestedDpr =
    typeof window !== 'undefined' && Number.isFinite(window.devicePixelRatio)
      ? window.devicePixelRatio
      : 1;
  const maxDprBySide = Math.min(
    MAX_RASTER_SIDE / width,
    MAX_RASTER_SIDE / height,
  );
  const maxDprByArea = Math.sqrt(MAX_RASTER_AREA / (width * height));

  return Math.max(
    0.01,
    Math.min(requestedDpr > 0 ? requestedDpr : 1, maxDprBySide, maxDprByArea),
  );
};

const validateRasterSize = (element: HTMLElement, dpr: number) => {
  const width = Math.ceil(element.scrollWidth || element.getBoundingClientRect().width) * dpr;
  const height = Math.ceil(element.scrollHeight || element.getBoundingClientRect().height) * dpr;

  if (width > MAX_RASTER_SIDE || height > MAX_RASTER_SIDE || width * height > MAX_RASTER_AREA) {
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
  const dpr = getCaptureDpr(element);
  validateRasterSize(element, dpr);

  const { snapdom } = await loadSnapdom();
  const blob = await snapdom.toBlob(element, {
    type: 'png',
    dpr,
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
