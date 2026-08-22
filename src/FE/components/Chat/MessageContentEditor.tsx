import { KeyboardEvent, useContext, useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { isMobile } from '@/utils/common';
import HomeContext from '@/contexts/home.context';
import { defaultFileConfig } from '@/apis/adminApis';
import {
  ChatRole,
  FileDef,
  IChat,
  MessageContentType,
  ResponseContent,
} from '@/types/chat';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { SendButton } from '@/components/ui/send-button';
import { IconFolder, IconLoader, IconPaperclip, IconPlus } from '@/components/Icons';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import UploadButton from '../Button/UploadButton';
import PasteUpload from '../PasteUpload/PasteUpload';
import DragUpload from '../DragUpload/DragUpload';
import FilesPopover from '../Popover/FilesPopover';
import FilePreview from '@/components/FilePreview/FilePreview';

interface Props {
  selectedChat: IChat;
  initialContent: ResponseContent[];
  onSave: (content: ResponseContent[]) => Promise<void> | void;
  onSend: (content: ResponseContent[]) => Promise<void> | void;
  onCancel: () => void;
}

const MessageContentEditor = ({ selectedChat, initialContent, onSave, onSend, onCancel }: Props) => {
  const { t } = useTranslation();
  const { state: { modelMap } } = useContext(HomeContext);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const inputContainerRef = useRef<HTMLDivElement>(null);
  const [text, setText] = useState(() => {
    const textContent = initialContent.find(
      (item) => item.$type === MessageContentType.text,
    );
    return textContent && textContent.$type === MessageContentType.text
      ? textContent.c
      : '';
  });
  const [files, setFiles] = useState<FileDef[]>(() =>
    initialContent
      .filter((item) => item.$type === MessageContentType.fileId)
      .map((item) => item.c)
      .filter((file): file is FileDef => typeof file !== 'string'),
  );
  const [uploading, setUploading] = useState(false);
  const [isTyping, setIsTyping] = useState(false);

  const hasVisionUploadCapability = selectedChat.spans.some(
    (span) => modelMap[span.modelId]?.allowVision,
  );
  useEffect(() => {
    if (!textareaRef.current) return;
    textareaRef.current.style.height = 'auto';
    textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
  }, [text]);

  const canUpload = hasVisionUploadCapability && !uploading && files.length < defaultFileConfig.count;

  const handleUploadFailed = (reason: string | null) => {
    setUploading(false);
    toast.error(t(reason || 'File upload failed'));
  };

  const handleUploadSuccessful = (file: FileDef) => {
    setFiles((previous) => previous.some((item) => item.id === file.id) ? previous : [...previous, file]);
    setUploading(false);
  };

  const handleUploading = () => setUploading(true);

  const buildContent = (): ResponseContent[] => [
    ...files.map((file) => ({ i: '', $type: MessageContentType.fileId as const, c: file })),
    { i: '', $type: MessageContentType.text as const, c: text },
  ];

  const submit = async (send: boolean) => {
    if (!text.trim()) {
      toast.error(t('Please enter a message'));
      return;
    }
    const content = buildContent();
    if (send) await onSend(content);
    else await onSave(content);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (isMobile() && event.key === 'Enter' && !event.shiftKey) return;
    if (event.key === 'Enter' && !event.shiftKey && !event.ctrlKey && !isTyping) {
      event.preventDefault();
      void submit(true);
    }
  };

  const handleContainerKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.altKey && event.key.toLowerCase() === 's') {
      event.preventDefault();
      event.stopPropagation();
      void submit(true);
    }
  };

  return (
    <div ref={inputContainerRef} onKeyDown={handleContainerKeyDown} className="flex w-full flex-col rounded-md bg-muted shadow-sm mb-3">
      {files.length > 0 && (
        <div className="flex flex-row gap-2 border-b border-border/40 px-3 py-2">
          {files.map((file) => (
            <FilePreview
              key={file.id}
              file={file}
              maxWidth={80}
              maxHeight={80}
              showDelete
              onDelete={() => setFiles((previous) => previous.filter((item) => item.id !== file.id))}
            />
          ))}
        </div>
      )}
      <Textarea
        ref={textareaRef}
        className="w-full resize-none whitespace-pre-wrap border-none rounded-md bg-muted outline-none"
        value={text}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={handleKeyDown}
        onCompositionStart={() => setIsTyping(true)}
        onCompositionEnd={() => setIsTyping(false)}
        placeholder={t('Type a message or type "/" to select a prompt...') || ''}
        style={{ fontFamily: 'inherit', fontSize: 'inherit', lineHeight: 'inherit', padding: '10px', margin: 0, overflow: 'hidden' }}
      />
      <div className="flex items-center justify-between gap-2 border-t border-border/40 p-2">
        <div className="flex items-center gap-1">
          {hasVisionUploadCapability && (
            <Popover>
              <PopoverTrigger asChild>
                <Button size="xs" className="h-8 w-8 rounded-full bg-muted/60 p-0" disabled={!canUpload}>
                  <IconPlus size={18} />
                </Button>
              </PopoverTrigger>
              <PopoverContent side="top" align="start" className="w-56 p-2">
                <div className="flex flex-col gap-1">
                  <UploadButton
                    fileConfig={defaultFileConfig}
                    onUploading={handleUploading}
                    onFailed={handleUploadFailed}
                    onSuccessful={handleUploadSuccessful}
                    accept="image/*"
                    capture={false}
                    inputId={`edit-upload-${selectedChat.id}`}
                    buttonProps={{ size: 'sm', variant: 'ghost', className: 'm-0 h-9 w-full justify-start gap-2 px-2 py-1' }}
                  >
                    <IconPaperclip size={18} />
                    <span className="text-sm">{t('Upload from device')}</span>
                  </UploadButton>
                  <FilesPopover
                    onSelect={(file) => {
                      if (files.some((item) => item.id === file.id)) {
                        setFiles((previous) => previous.filter((item) => item.id !== file.id));
                      } else if (files.length < defaultFileConfig.count) {
                        setFiles((previous) => [...previous, file]);
                      }
                    }}
                    selectedFiles={files}
                    contentTypePrefix="image/"
                    trigger={<Button size="sm" variant="ghost" className="m-0 h-9 w-full justify-start gap-2 px-2 py-1"><IconFolder size={18} /><span className="text-sm">{t('Select remote files')}</span></Button>}
                  />
                </div>
              </PopoverContent>
            </Popover>
          )}
          {uploading && <Button disabled size="xs" className="h-8 w-8 bg-transparent p-0"><IconLoader size={20} /></Button>}
          {canUpload && <PasteUpload fileConfig={defaultFileConfig} containerRef={inputContainerRef} onUploading={handleUploading} onFailed={handleUploadFailed} onSuccessful={handleUploadSuccessful} />}
          {canUpload && <DragUpload fileConfig={defaultFileConfig} onUploading={handleUploading} onFailed={handleUploadFailed} onSuccessful={handleUploadSuccessful} containerRef={inputContainerRef as React.RefObject<HTMLElement>} />}
        </div>
        <div className="flex justify-end gap-3">
          <Button variant="link" className="rounded-md px-4 py-1 text-sm font-medium" onClick={() => void submit(false)} disabled={!text.trim() || uploading}>{t('Save')}</Button>
          <SendButton
            onSend={() => void submit(true)}
            disabled={!text.trim() || uploading}
            className="h-9 py-1"
            size="sm"
          />
          <Button variant="outline" className="rounded-md px-4 py-1 text-sm font-medium" onClick={onCancel}>{t('Cancel')}</Button>
        </div>
      </div>
    </div>
  );
};

export default MessageContentEditor;
