import { type RefObject, useEffect } from 'react';

import { checkFileSizeCanUpload, uploadFile } from '@/utils/uploadFile';

import { FileDef } from '@/types/chat';
import { ChatModelFileConfig } from '@/types/model';

interface IPasteUploadProps {
  fileConfig: ChatModelFileConfig;
  containerRef: RefObject<HTMLElement | null>;
  allowAllFiles?: boolean; // 是否允许粘贴所有类型的文件（用于code execution）
  onUploading?: () => void;
  onSuccessful?: (def: FileDef) => void;
  onFailed?: (reason: string | null) => void;
}

const PasteUpload = (props: IPasteUploadProps) => {
  const {
    fileConfig,
    containerRef,
    allowAllFiles = false,
    onUploading,
    onSuccessful,
    onFailed,
  } = props;

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const handleFileUpload = (file: File) => {
      const { maxSize } = fileConfig || { maxSize: 0 };
      if (checkFileSizeCanUpload(maxSize, file.size)) {
        onFailed?.('File is too large.');
        return;
      }
      uploadFile(file, onUploading, onSuccessful, onFailed);
    };

    const handlePaste = (event: ClipboardEvent) => {
      const items = event.clipboardData?.items;
      if (items) {
        const itemsArray = Array.from(items);
        for (const item of itemsArray) {
          // 如果启用了allowAllFiles，处理所有文件；否则只处理图片
          const isImage = item.type.startsWith('image/');
          if (isImage || allowAllFiles) {
            const file = item.getAsFile();
            if (file) {
              event.stopPropagation();
              // 如果是image content类型的图片（默认文件名为image.png），重命名为时间格式
              let processedFile = file;
              if (file.name === 'image.png') {
                const now = new Date();
                const hours = String(now.getHours()).padStart(2, '0');
                const minutes = String(now.getMinutes()).padStart(2, '0');
                const seconds = String(now.getSeconds()).padStart(2, '0');
                const newFileName = `pasted_${hours}${minutes}${seconds}.png`;
                processedFile = new File([file], newFileName, { type: file.type });
              }
              handleFileUpload(processedFile);
            }
          }
        }
      }
    };

    container.addEventListener('paste', handlePaste);

    return () => {
      container.removeEventListener('paste', handlePaste);
    };
  }, [
    allowAllFiles,
    containerRef,
    fileConfig,
    onFailed,
    onSuccessful,
    onUploading,
  ]);

  return null;
};

export default PasteUpload;
