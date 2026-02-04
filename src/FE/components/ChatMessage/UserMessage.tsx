import { useEffect, useMemo, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isChatting } from '@/utils/chats';

import {
  IChat,
  Message,
  MessageContentType,
  ResponseContent,
  TextContent,
} from '@/types/chat';
import { IChatMessage, getMessageContents } from '@/types/chatMessage';

import { Button } from '@/components/ui/button';
import { useSendKeyHandler } from '@/components/ui/send-button';
import ImagePreview from '@/components/ImagePreview/ImagePreview';
import FilePreview from '@/components/FilePreview/FilePreview';

import { Textarea } from '../ui/textarea';
import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import EditAction from './EditAction';
import ExpandTextAction from './ExpandTextAction';
import PaginationAction from './PaginationAction';
import RegenerateAction from './RegenerateAction';
import { ANIMATION_DURATION_MS } from '@/constants/animation';

interface Props {
  message: IChatMessage;
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
  const { id: messageId, siblingIds, parentId } = message;
  const content = getMessageContents(message);
  const defaultText = useMemo(() => {
    const textContent = content.find((x) => x.$type === MessageContentType.text) as TextContent | undefined;
    return textContent?.c || '';
  }, [content]);
  const [editedText, setEditedText] = useState<string | null>(null);
  const contentText = editedText ?? defaultText;
  const [previewImages, setPreviewImages] = useState<string[]>([]);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [sourceImageElement, setSourceImageElement] = useState<HTMLImageElement | null>(null);
  const { status: chatStatus } = selectedChat;
  const currentMessageIndex = siblingIds.findIndex((x) => x === messageId);
  const COLLAPSED_MAX_LINES = 5;
  const [isTextExpanded, setIsTextExpanded] = useState(false);
  const [isTextOverflowing, setIsTextOverflowing] = useState(false);
  const [collapsedMaxHeight, setCollapsedMaxHeight] = useState<number | null>(null);
  const [textMaxHeight, setTextMaxHeight] = useState<number | null>(null);
  const [isTextAnimating, setIsTextAnimating] = useState(false);
  const toggleAnimationTimerRef = useRef<number | null>(null);
  const textContentRef = useRef<HTMLDivElement>(null);

  const handleEditMessage = (isOnlySave: boolean = false) => {
    if (isOnlySave) {
      let msgContent = content.find(
        (x) => x.$type === MessageContentType.text,
      )! as TextContent;
      msgContent.c = contentText;
      onEditUserMessage && onEditUserMessage(message.id, msgContent);
    } else {
      if (selectedChat.id && onEditAndSendMessage) {
        const messageContent = structuredClone(content).map(
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
    setEditedText(null);
    setIsEditing(false);
  };

  // 使用发送键盘处理 hook
  const { handleKeyDown: handleSendKeyDown } = useSendKeyHandler(
    () => handleEditMessage(false),
    isTyping,
    !contentText?.trim()
  );

  const handleInputChange = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    setEditedText(event.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    // 使用新的发送键盘处理逻辑
    handleSendKeyDown(e);
  };

  const handleToggleEditing = () => {
    if (isEditing) {
      setEditedText(null);
      setIsEditing(false);
      return;
    }
    setEditedText(defaultText);
    setIsEditing(true);
  };

  const handleImageClick = (imageUrl: string, allImages: string[], event: React.MouseEvent<HTMLImageElement>) => {
    setSourceImageElement(event.currentTarget);
    setPreviewImages(allImages);
    setPreviewIndex(allImages.indexOf(imageUrl));
    setIsPreviewOpen(true);
  };

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  }, [isEditing]);

  useEffect(() => {
    setIsTextExpanded(false);
    setIsTextAnimating(false);
    if (toggleAnimationTimerRef.current) {
      window.clearTimeout(toggleAnimationTimerRef.current);
      toggleAnimationTimerRef.current = null;
    }
  }, [messageId]);

  useEffect(() => {
    if (isEditing) return;
    const el = textContentRef.current;
    if (!el) return;

    const compute = () => {
      const lineHeight = Number.parseFloat(window.getComputedStyle(el).lineHeight || '');
      const fallbackLineHeight = 20;
      const resolvedLineHeight = Number.isFinite(lineHeight) ? lineHeight : fallbackLineHeight;
      const maxHeight = resolvedLineHeight * COLLAPSED_MAX_LINES;
      setCollapsedMaxHeight(maxHeight);
      if (!isTextExpanded && !isTextAnimating) {
        setTextMaxHeight(maxHeight);
      }

      requestAnimationFrame(() => {
        const nextEl = textContentRef.current;
        if (!nextEl) return;
        setIsTextOverflowing(nextEl.scrollHeight > maxHeight + 1);
      });
    };

    compute();

    const resizeObserver = new ResizeObserver(() => compute());
    resizeObserver.observe(el);
    return () => resizeObserver.disconnect();
  }, [isEditing, contentText, isTextExpanded, isTextAnimating]);

  useEffect(() => {
    return () => {
      if (toggleAnimationTimerRef.current) {
        window.clearTimeout(toggleAnimationTimerRef.current);
        toggleAnimationTimerRef.current = null;
      }
    };
  }, []);

  const handleToggleTextExpanded = () => {
    if (isEditing) return;
    const el = textContentRef.current;
    if (!el || !collapsedMaxHeight) {
      setIsTextExpanded((v) => !v);
      return;
    }

    if (toggleAnimationTimerRef.current) {
      window.clearTimeout(toggleAnimationTimerRef.current);
      toggleAnimationTimerRef.current = null;
    }

    const fullHeight = el.scrollHeight;
    setIsTextAnimating(true);

    if (!isTextExpanded) {
      setIsTextExpanded(true);
      setTextMaxHeight(collapsedMaxHeight);
      requestAnimationFrame(() => {
        setTextMaxHeight(fullHeight);
      });
      toggleAnimationTimerRef.current = window.setTimeout(() => {
        setTextMaxHeight(null);
        setIsTextAnimating(false);
        toggleAnimationTimerRef.current = null;
      }, ANIMATION_DURATION_MS);
      return;
    }

    setIsTextExpanded(false);
    setTextMaxHeight(fullHeight);
    requestAnimationFrame(() => {
      setTextMaxHeight(collapsedMaxHeight);
    });
    toggleAnimationTimerRef.current = window.setTimeout(() => {
      setIsTextAnimating(false);
      toggleAnimationTimerRef.current = null;
    }, ANIMATION_DURATION_MS);
  };

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

      <div className={'flex flex-row-reverse relative'}>
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
              <Button
                variant="default"
                className="rounded-md px-4 py-1 text-sm font-medium active:bg-primary/80"
                onClick={() => handleEditMessage(false)}
                disabled={(contentText || '')?.trim().length <= 0}
              >
                {t('Send')}
              </Button>
              <Button
                variant="outline"
                className="rounded-md border border-neutral-300 px-4 py-1 text-sm font-medium text-neutral-700 hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800"
                onClick={() => {
                  setEditedText(defaultText);
                  setIsEditing(false);
                }}
              >
                {t('Cancel')}
              </Button>
            </div>
          </div>
        ) : (
          <div className="bg-card py-2 px-3 rounded-md overflow-hidden chat-message-bg">
            <div className="flex flex-wrap gap-2 justify-end text-right">
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
              ref={textContentRef}
              className={`prose whitespace-pre-wrap dark:prose-invert text-sm ${
                content.filter((x) => x.$type === MessageContentType.fileId).length > 0 ? 'mt-2' : ''
              }`}
              style={
                collapsedMaxHeight
                  ? {
                      ...(textMaxHeight != null
                        ? { maxHeight: `${textMaxHeight}px`, overflow: 'hidden' }
                        : !isTextExpanded
                          ? { maxHeight: `${collapsedMaxHeight}px`, overflow: 'hidden' }
                          : { overflow: 'visible' }),
                      transition: `max-height ${ANIMATION_DURATION_MS}ms ease`,
                      willChange: 'max-height',
                    }
                  : undefined
              }
            >
              {contentText}
            </div>
          </div>
        )}
      </div>

      <div className="flex my-1 justify-end">
        {!isEditing && (
          <>
            <ExpandTextAction
              expanded={isTextExpanded}
              hidden={!isTextOverflowing && !isTextExpanded}
              isHoverVisible
              onToggle={handleToggleTextExpanded}
            />
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
