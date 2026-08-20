import { useEffect, useMemo, useRef, useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';

import useTranslation from '@/hooks/useTranslation';

import { isChatting } from '@/utils/chats';

import {
  IChat,
  Message,
  MessageContentType,
  ResponseContent,
} from '@/types/chat';
import { IChatMessage, getMessageContents } from '@/types/chatMessage';

import { Button } from '@/components/ui/button';
import ImagePreview from '@/components/ImagePreview/ImagePreview';
import FilePreview from '@/components/FilePreview/FilePreview';
import MessageContentEditor from '@/components/Chat/MessageContentEditor';

import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import EditAction from './EditAction';
import PaginationAction from './PaginationAction';
import RegenerateAction from './RegenerateAction';
import { ANIMATION_DURATION_MS } from '@/constants/animation';

interface Props {
  message: IChatMessage;
  selectedChat: IChat;
  readonly?: boolean;
  onChangeMessage?: (messageId: string) => void;
  onEditAndSendMessage?: (editedMessage: Message, parentId?: string) => void;
  onEditUserMessage?: (messageId: string, content: ResponseContent[]) => Promise<void> | void;
  onDeleteMessage?: (messageId: string) => Promise<void>;
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
  const [isEditing, setIsEditing] = useState<boolean>(false);
  const { id: messageId, siblingIds, parentId } = message;
  const content = getMessageContents(message);
  const fileContents = useMemo(
    () => content.filter((item) => item.$type === MessageContentType.fileId),
    [content],
  );
  const defaultText = useMemo(() => {
    const textContent = content.find((x) => x.$type === MessageContentType.text);
    return textContent && textContent.$type === MessageContentType.text ? textContent.c : '';
  }, [content]);
  const [previewImages, setPreviewImages] = useState<string[]>([]);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [sourceImageElement, setSourceImageElement] = useState<HTMLImageElement | null>(null);
  const { status: chatStatus } = selectedChat;
  const currentMessageIndex = siblingIds.findIndex((x) => x === messageId);
  const COLLAPSED_VISIBLE_LINES = 5.5;
  const [isTextExpanded, setIsTextExpanded] = useState(false);
  const [isTextOverflowing, setIsTextOverflowing] = useState(false);
  const [collapsedMaxHeight, setCollapsedMaxHeight] = useState<number | null>(null);
  const [textMaxHeight, setTextMaxHeight] = useState<number | null>(null);
  const [isTextAnimating, setIsTextAnimating] = useState(false);
  const toggleAnimationTimerRef = useRef<number | null>(null);
  const textContentRef = useRef<HTMLDivElement>(null);
  const showInlineExpandToggle = !isEditing && isTextOverflowing && !isTextExpanded;
  const showInlineCollapseToggle = !isEditing && isTextOverflowing && isTextExpanded;
  const showBelowTextToggle = showInlineExpandToggle || showInlineCollapseToggle;

  const handleToggleEditing = () => {
    if (isEditing) {
      setIsEditing(false);
      return;
    }
    setIsEditing(true);
  };

  const handleImageClick = (imageUrl: string, allImages: string[], event: React.MouseEvent<HTMLImageElement>) => {
    setSourceImageElement(event.currentTarget);
    setPreviewImages(allImages);
    setPreviewIndex(allImages.indexOf(imageUrl));
    setIsPreviewOpen(true);
  };

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
      const maxHeight = resolvedLineHeight * COLLAPSED_VISIBLE_LINES;
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
  }, [isEditing, defaultText, isTextExpanded, isTextAnimating]);

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

      <div className={`flex flex-row-reverse relative`}>
        {isEditing ? (
          <MessageContentEditor
            key={messageId}
            selectedChat={selectedChat}
            initialContent={content}
            onSave={async (editedContent) => {
              await onEditUserMessage?.(message.id, editedContent);
              setIsEditing(false);
            }}
            onSend={async (editedContent) => {
              if (selectedChat.id && onEditAndSendMessage) {
                onEditAndSendMessage(
                  { ...message, content: editedContent },
                  parentId || undefined,
                );
              }
              setIsEditing(false);
            }}
            onCancel={() => setIsEditing(false)}
          />
        ) : (
          <div className="ml-auto bg-card py-2 px-3 rounded-md overflow-visible chat-message-bg">
            <div className="flex flex-wrap gap-2 justify-end text-right">
              {fileContents.map((file: any, index) => {
                return (
                  <FilePreview
                    key={'user-file-' + index}
                    file={file.c}
                    onImageClick={handleImageClick}
                  />
                );
              })}
            </div>
            <div className={`relative group/user-message-text ${fileContents.length > 0 ? 'mt-2' : ''}`}>
              <div
                ref={textContentRef}
                className="max-w-full whitespace-pre-wrap break-words text-sm leading-[1.6]"
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
                {defaultText}
              </div>

              {showInlineExpandToggle && (
                <div className="pointer-events-none absolute inset-x-0 -bottom-6 z-10 flex justify-center">
                  <Button
                    type="button"
                    variant="ghost"
                    className="pointer-events-auto h-7 w-7 rounded-full border border-border/70 bg-card/95 p-0 text-foreground shadow-sm hover:bg-accent hover:text-accent-foreground"
                    aria-label={t('Expand text') || 'Expand text'}
                    aria-expanded={isTextExpanded}
                    title={t('Expand text') || 'Expand text'}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleToggleTextExpanded();
                    }}
                  >
                    <ChevronDown className="h-4 w-4" />
                  </Button>
                </div>
              )}

              {showInlineCollapseToggle && (
                <div className="pointer-events-none absolute inset-x-0 -bottom-6 z-10 flex justify-center opacity-0 transition-opacity duration-200 group-hover/user-message-text:pointer-events-auto group-hover/user-message-text:opacity-100 focus-within:pointer-events-auto focus-within:opacity-100">
                  <Button
                    type="button"
                    variant="ghost"
                    className="h-7 w-7 rounded-full border border-border/70 bg-card/95 p-0 text-foreground shadow-sm hover:bg-accent hover:text-accent-foreground"
                    aria-label={t('Collapse text') || 'Collapse text'}
                    aria-expanded={isTextExpanded}
                    title={t('Collapse text') || 'Collapse text'}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleToggleTextExpanded();
                    }}
                  >
                    <ChevronUp className="h-4 w-4" />
                  </Button>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      <div className="flex my-1 justify-end">
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
            text={defaultText}
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
                onDelete={async () => {
                  await onDeleteMessage?.(messageId);
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
