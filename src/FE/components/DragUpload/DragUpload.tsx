import React, { useEffect, useRef, useState } from 'react';

import { checkFileSizeCanUpload, uploadFile } from '@/utils/uploadFile';

import { FileDef } from '@/types/chat';
import { ChatModelFileConfig } from '@/types/model';

interface IDragUploadProps {
  fileConfig: ChatModelFileConfig;
  allowAllFiles?: boolean; // 是否允许所有类型的文件（用于code execution）
  onUploading?: () => void;
  onSuccessful?: (def: FileDef) => void;
  onFailed?: (reason: string | null) => void;
  containerRef?: React.RefObject<HTMLElement>; // 拖拽区域容器的ref
}

const DragUpload = (props: IDragUploadProps) => {
  const { fileConfig, allowAllFiles = false, onUploading, onSuccessful, onFailed, containerRef } = props;
  const [isDragging, setIsDragging] = useState(false);
  const dragCounter = useRef(0);

  useEffect(() => {
    const container = containerRef?.current || document.body;

    const handleDragEnter = (event: DragEvent) => {
      event.preventDefault();
      event.stopPropagation();
      dragCounter.current++;
      
      // 检查是否有文件
      if (event.dataTransfer?.types.includes('Files')) {
        setIsDragging(true);
      }
    };

    const handleDragLeave = (event: DragEvent) => {
      event.preventDefault();
      event.stopPropagation();
      dragCounter.current--;
      
      if (dragCounter.current === 0) {
        setIsDragging(false);
      }
    };

    const handleDragOver = (event: DragEvent) => {
      event.preventDefault();
      event.stopPropagation();
    };

    const handleDrop = (event: DragEvent) => {
      event.preventDefault();
      event.stopPropagation();
      setIsDragging(false);
      dragCounter.current = 0;

      const files = event.dataTransfer?.files;
      if (files && files.length > 0) {
        // 处理所有拖拽的文件
        Array.from(files).forEach((file) => {
          // 如果启用了allowAllFiles，处理所有文件；否则只处理图片
          const isImage = file.type.startsWith('image/');
          if (isImage || allowAllFiles) {
            handleFileUpload(file);
          }
        });
      }
    };

    container.addEventListener('dragenter', handleDragEnter);
    container.addEventListener('dragleave', handleDragLeave);
    container.addEventListener('dragover', handleDragOver);
    container.addEventListener('drop', handleDrop);

    return () => {
      container.removeEventListener('dragenter', handleDragEnter);
      container.removeEventListener('dragleave', handleDragLeave);
      container.removeEventListener('dragover', handleDragOver);
      container.removeEventListener('drop', handleDrop);
    };
  }, [containerRef, allowAllFiles]);

  const handleFileUpload = (file: File) => {
    const { maxSize } = fileConfig || { maxSize: 0 };
    if (checkFileSizeCanUpload(maxSize, file.size)) {
      onFailed && onFailed('File is too large.');
      return;
    }
    uploadFile(file, onUploading, onSuccessful, onFailed);
  };

  return (
    <>
      {isDragging && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm pointer-events-none">
          <div className="bg-card border-2 border-dashed border-primary rounded-lg p-8 shadow-lg">
            <div className="text-center">
              <div className="text-4xl mb-4">📁</div>
              <div className="text-xl font-semibold text-primary">拖放文件到此处上传</div>
              <div className="text-sm text-muted-foreground mt-2">
                {allowAllFiles ? '支持所有文件类型' : '支持图片文件'}
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default DragUpload;
