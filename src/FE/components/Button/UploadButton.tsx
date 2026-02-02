import { useCallback, useEffect, useRef } from 'react';

import { checkFileSizeCanUpload, uploadFile } from '@/utils/uploadFile';

import { FileDef } from '@/types/chat';
import { ChatModelFileConfig } from '@/types/model';

import { Button, type ButtonProps } from '@/components/ui/button';

interface Props {
  onSuccessful?: (def: FileDef) => void;
  onUploading?: () => void;
  onFailed?: (reason: string | null) => void;
  children?: React.ReactNode;
  fileConfig: ChatModelFileConfig;
  maxFileSize?: number;
  // 是否启用相机拍照，移动端设置为 true 会优先调起相机；为 false 则仅选择相册（不拍照）
  capture?: boolean;
  // 可选的 input 元素 id，避免多个按钮时冲突
  inputId?: string;
  buttonProps?: ButtonProps;
}

const UploadButton: React.FunctionComponent<Props> = ({
  onSuccessful,
  onUploading,
  onFailed,
  fileConfig,
  children,
  capture = true,
  inputId = 'upload',
  buttonProps,
}: Props) => {
  const uploadRef = useRef<HTMLInputElement>(null);
  const { maxSize } = fileConfig || { maxSize: 0 };
  const changeFile = useCallback(async (event: any) => {
    const file = event?.target?.files[0];
    if (checkFileSizeCanUpload(maxSize, file.size)) {
      onFailed && onFailed('File is too large.');
      return;
    }

    try {
      if (file) {
        uploadFile(file, onUploading, onSuccessful, onFailed);
      }
    } catch (error) {
      console.error(error);
    }
  }, [maxSize, onFailed, onSuccessful, onUploading]);

  useEffect(() => {
    const fileInput = uploadRef.current;
    if (!fileInput) return;
    fileInput.removeEventListener('change', changeFile as any);
    fileInput.addEventListener('change', changeFile as any);
    return () => {
      fileInput.removeEventListener('change', changeFile as any);
    };
  }, [changeFile]);

  const Btn = (
    <Button
      onClick={() => {
        uploadRef.current?.click();
      }}
      {...buttonProps}
    >
      {children}
    </Button>
  );

  return (
    <div>
      {Btn}
      <input
        ref={uploadRef}
        style={{ display: 'none' }}
        id={inputId}
        type="file"
        accept="image/*"
        {...(capture ? { capture: true } : {})}
      />
    </div>
  );
};

export default UploadButton;
