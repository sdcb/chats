import { FileDef } from '@/types/chat';

import { getApiUrl } from './common';
import { getUserSession } from './user';

export async function uploadFile(
  file: File,
  onUploading?: () => void,
  onSuccessful?: (def: FileDef) => void,
  onFailed?: (reason: string | null) => void,
) {
  onUploading && onUploading();

  try {
    const form = new FormData();
    form.append('file', file);
    const resp = await fetch(`${getApiUrl()}/api/file-service/upload`, {
      method: 'PUT',
      body: form,
      headers: {
        Authorization: `Bearer ${getUserSession()}`,
      },
    });
    if (resp.ok) {
      onSuccessful && onSuccessful(await resp.json());
    } else {
      onFailed && onFailed(await resp.text());
    }
  } catch (error) {
    onFailed && onFailed(error as any);
  }
}

export function checkFileSizeCanUpload(maxSize: number, fileSize: number) {
  return maxSize && fileSize / 1024 > maxSize;
}
