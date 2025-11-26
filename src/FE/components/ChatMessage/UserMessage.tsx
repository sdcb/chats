import { useEffect, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isChatting } from '@/utils/chats';

import {
  ChatRole,
  ChatSpanStatus,
  getFileUrl,
  IChat,
  Message,
  MessageContentType,
  ResponseContent,
  TextContent,
} from '@/types/chat';

import { Button } from '@/components/ui/button';
import { SendButton, useSendKeyHandler } from '@/components/ui/send-button';
import ImagePreview from '@/components/ImagePreview/ImagePreview';
import FilePreview from '@/components/FilePreview/FilePreview';

import { Textarea } from '../ui/textarea';
import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import EditAction from './EditAction';
import PaginationAction from './PaginationAction';
import RegenerateAction from './RegenerateAction';

export interface UserMessage {
  id: string;
  role: ChatRole;
  content: ResponseContent[];
  status: ChatSpanStatus;
  parentId: string | null;
  siblingIds: string[];
}

interface Props {
  message: UserMessage;
  selectedChat: IChat;
  readonly?: boolean;
  onChangeMessage?: (messageId: string) => void;
  onEditAndSendMessage?: (editedMessage: Message, parentId?: string) => void;
  onEditUserMessage?: (messageId: string, content: ResponseContent) => void;
  onDeleteMessage?: (messageId: string) => void;
  onRegenerateAllAssistant?: (messageId: string, modelId: number) => void;
}

const UserMessage = (props: Props) => {
  const { t } = useTranslation();

  const {
    message,
    selectedChat,
    readonly,
    onChangeMessage,
    onEditAndSendMessage,
    onEditUserMessage,
    onDeleteMessage,
    onRegenerateAllAssistant,
  } = props;
  const [isTyping, setIsTyping] = useState<boolean>(false);
  const [isEditing, setIsEditing] = useState<boolean>(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [contentText, setContentText] = useState('');
  const [previewImages, setPreviewImages] = useState<string[]>([]);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [sourceImageElement, setSourceImageElement] = useState<HTMLImageElement | null>(null);
  const { id: messageId, siblingIds, parentId, content } = message;
  const { status: chatStatus } = selectedChat;
  const currentMessageIndex = siblingIds.findIndex((x) => x === messageId);

  const handleEditMessage = (isOnlySave: boolean = false) => {
    if (isOnlySave) {
      let msgContent = message.content.find(
        (x) => x.$type === MessageContentType.text,
      )! as TextContent;
      msgContent.c = contentText;
      onEditUserMessage && onEditUserMessage(message.id, msgContent);
    } else {
      if (selectedChat.id && onEditAndSendMessage) {
        const messageContent = structuredClone(message.content).map(
          (x: any) => {
            if (x.$type === MessageContentType.text) {
              x.c = contentText;
            }
            return x;
          },
        );
        onEditAndSendMessage(
          { ...message, content: messageContent },
          parentId || undefined,
        );
      }
    }
    setIsEditing(false);
  };

  // 使用发送键盘处理 hook
  const { handleKeyDown: handleSendKeyDown } = useSendKeyHandler(
    () => handleEditMessage(false),
    isTyping,
    !contentText?.trim()
  );

  const handleInputChange = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    setContentText(event.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    // 使用新的发送键盘处理逻辑
    handleSendKeyDown(e);
  };

  const handleToggleEditing = () => {
    setIsEditing(!isEditing);
  };

  const handleImageClick = (imageUrl: string, allImages: string[], event: React.MouseEvent<HTMLImageElement>) => {
    setSourceImageElement(event.currentTarget);
    setPreviewImages(allImages);
    setPreviewIndex(allImages.indexOf(imageUrl));
    setIsPreviewOpen(true);
  };

  const init = () => {
    const textContent = content.find((x) => x.$type === MessageContentType.text) as TextContent | undefined;
    const text = textContent?.c || '';
    setContentText(text as string);
  };

  useEffect(() => {
    init();
  }, [content]);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  }, [isEditing]);

  // 收集所有图片URL用于预览
  const allImageUrls = content
    .filter((x) => x.$type === MessageContentType.fileId)
    .map((img: any) => getFileUrl(img.c));

  return (
    <>
      {/* 图片预览组件 */}
      <ImagePreview
        images={previewImages}
        initialIndex={previewIndex}
        isOpen={isPreviewOpen}
        onClose={() => setIsPreviewOpen(false)}
        sourceElement={sourceImageElement}
      />

      <div className="flex flex-row-reverse relative">
        {isEditing ? (
          <div className="flex w-full flex-col flex-wrap rounded-md bg-muted shadow-sm mb-3">
            <Textarea
              ref={textareaRef}
              className="w-full outline-none resize-none whitespace-pre-wrap border-none rounded-md bg-muted"
              value={contentText}
              onChange={handleInputChange}
              onKeyDown={handleKeyDown}
              onCompositionStart={() => setIsTyping(true)}
              onCompositionEnd={() => setIsTyping(false)}
              style={{
                fontFamily: 'inherit',
                fontSize: 'inherit',
                lineHeight: 'inherit',
                padding: '10px',
                margin: '0',
                overflow: 'hidden',
              }}
            />

            <div className="flex justify-end p-3 gap-3">
              <Button
                variant="link"
                className="rounded-md px-4 py-1 text-sm font-medium"
                onClick={() => {
                  handleEditMessage(true);
                }}
                disabled={(contentText || '')?.trim().length <= 0}
              >
                {t('Save')}
              </Button>
              <SendButton
                onSend={() => handleEditMessage(false)}
                disabled={!contentText?.trim()}
                size="sm"
              />
              <Button
                variant="outline"
                className="rounded-md border border-neutral-300 px-4 py-1 text-sm font-medium text-neutral-700 hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800"
                onClick={() => {
                  init();
                  setIsEditing(false);
                }}
              >
                {t('Cancel')}
              </Button>
            </div>
          </div>
        ) : (
          <div className="bg-card py-2 px-3 rounded-md overflow-hidden">
            <div className="flex flex-wrap justify-end text-right gap-2">
              {content
                .filter((x) => x.$type === MessageContentType.fileId)
                .map((file: any, index) => {
                  return (
                    <FilePreview
                      key={'user-file-' + index}
                      file={file.c}
                      onImageClick={handleImageClick}
                    />
                  );
                })}
            </div>
            <div
              className={`prose whitespace-pre-wrap dark:prose-invert text-base ${
                content.filter((x) => x.$type === MessageContentType.fileId)
                  .length > 0
                  ? 'mt-2'
                  : ''
              }`}
            >
              {contentText}
            </div>
          </div>
        )}
      </div>

      <div className="flex justify-end my-1">
        {!isEditing && (
          <>
            {!readonly && (
              <EditAction
                isHoverVisible
                disabled={isChatting(chatStatus)}
                onToggleEditing={handleToggleEditing}
              />
            )}
            <CopyAction
              triggerClassName="invisible group-hover:visible focus:visible"
              text={contentText}
            />
            {!readonly && (
              <RegenerateAction
                hidden={!onRegenerateAllAssistant}
                disabled={isChatting(chatStatus)}
                isHoverVisible
                onRegenerate={() => {
                  if (onRegenerateAllAssistant && selectedChat.spans && selectedChat.spans.length > 0) {
                    // 使用第一个启用的 span 的 modelId，如果没有启用的就使用第一个
                    const enabledSpan = selectedChat.spans.find(s => s.enabled) || selectedChat.spans[0];
                    onRegenerateAllAssistant(messageId, enabledSpan.modelId);
                  }
                }}
              />
            )}
            {!readonly && (
              <DeleteAction
                hidden={isChatting(chatStatus)}
                isHoverVisible
                onDelete={() => {
                  onDeleteMessage && onDeleteMessage(messageId);
                }}
              />
            )}
            <PaginationAction
              hidden={siblingIds.length <= 1}
              disabledPrev={currentMessageIndex === 0 || isChatting(chatStatus)}
              disabledNext={
                currentMessageIndex === siblingIds.length - 1 ||
                isChatting(chatStatus)
              }
              currentSelectIndex={currentMessageIndex}
              messageIds={siblingIds}
              onChangeMessage={onChangeMessage}
            />
          </>
        )}
      </div>
    </>
  );
};

export default UserMessage;
