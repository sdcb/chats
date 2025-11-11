import { useEffect, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isChatting, preprocessLaTeX } from '@/utils/chats';

import {
  ChatSpanStatus,
  ChatStatus,
  EMPTY_ID,
  FileDef,
  getFileUrl,
  MessageContentType,
  ResponseContent,
  ToolCallContent,
  ToolResponseContent,
} from '@/types/chat';
import { IChatMessage, IStepGenerateInfo, MessageDisplayType } from '@/types/chatMessage';

import { CodeBlock } from '@/components/Markdown/CodeBlock';
import { MemoizedReactMarkdown } from '@/components/Markdown/MemoizedReactMarkdown';
import ToolCallBlock from '@/components/Markdown/ToolCallBlock';
import ImagePreview from '@/components/ImagePreview/ImagePreview';
import FilePreview from '@/components/FilePreview/FilePreview';

import ChatError from '../ChatError/ChatError';
import { IconCopy, IconDots, IconEdit } from '../Icons';
import { Button } from '../ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import { Textarea } from '../ui/textarea';
import ThinkingMessage from './ThinkingMessage';

import { cn } from '@/lib/utils';
import rehypeKatex from 'rehype-katex';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';
import remarkBreaks from 'remark-breaks';

// 骨架动画组件
const SkeletonLine = ({ width = '100%', height = '1rem', delay = '0s' }: { width?: string; height?: string; delay?: string }) => (
  <div 
    className="animate-pulse bg-muted rounded" 
    style={{ 
      width, 
      height,
      animationDelay: delay,
      animationDuration: '1.5s'
    }}
  />
);

const MessageSkeleton = () => (
  <div className="space-y-3 py-2">
    {/* 第一行较短，立即显示 */}
    <SkeletonLine width="75%" delay="0s" />
    {/* 第二行完整，稍微延迟 */}
    <SkeletonLine width="100%" delay="0.1s" />
    {/* 第三行中等长度 */}
    <SkeletonLine width="60%" delay="0.2s" />
    {/* 第四行较短，模拟段落结束 */}
    <SkeletonLine width="40%" delay="0.3s" />
  </div>
);

interface Props {
  message: IChatMessage;
  chatStatus: ChatStatus;
  readonly?: boolean;
  chatId?: string;
  chatShareId?: string;
  onEditResponseMessage?: (
    messageId: string,
    content: ResponseContent,
    isCopy?: boolean,
  ) => void;
}

const ResponseMessage = (props: Props) => {
  const { message, chatStatus, readonly, chatId, chatShareId, onEditResponseMessage } = props;
  const { t } = useTranslation();

  const { id: messageId, status: messageStatus, content } = message;
  const [isTyping, setIsTyping] = useState<boolean>(false);
  const [editId, setEditId] = useState(EMPTY_ID);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [messageContent, setMessageContent] = useState(message.content);
  const [contentText, setContentText] = useState('');
  const [previewImages, setPreviewImages] = useState<string[]>([]);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [sourceImageElement, setSourceImageElement] = useState<HTMLImageElement | null>(null);

  const handleEditMessage = (isCopyAndSave: boolean = false) => {
    const newContent = messageContent.find((c) => c.i === editId)!;
    // Only text content can be edited
    if (newContent.$type === MessageContentType.text) {
      newContent.c = contentText;
      onEditResponseMessage && onEditResponseMessage(messageId, newContent, isCopyAndSave);
    }
    setEditId(EMPTY_ID);
  };

  const handleInputChange = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    setContentText(event.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  };

  const handlePressEnter = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !isTyping && e.ctrlKey) {
      e.preventDefault();
      handleEditMessage();
    }
  };

  const handleToggleEditing = (id: string, text: string) => {
    setContentText(text);
    setEditId(id);
  };

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text || '');
  };

  const handleImageClick = (imageUrl: string, allImages: string[], event: React.MouseEvent<HTMLImageElement>) => {
    setSourceImageElement(event.currentTarget);
    setPreviewImages(allImages);
    setPreviewIndex(allImages.indexOf(imageUrl));
    setIsPreviewOpen(true);
  };

  // UI层的DTO，用于组合工具调用和响应
  interface ToolGroupContent {
    $type: 'toolGroup';
    toolCall: ToolCallContent;
    toolResponse?: ToolResponseContent;
    originalIndex: number;
  }

  type ProcessedContent = ResponseContent | ToolGroupContent;

  // 按原始顺序处理内容，将工具调用和响应组合
  const processContentInOrder = (content: ResponseContent[]): ProcessedContent[] => {
    const toolResponseMap: { [toolCallId: string]: ToolResponseContent } = {};
    const processedContent: ProcessedContent[] = [];
    
    // 首先收集所有工具响应
    content.forEach((c) => {
      if (c.$type === MessageContentType.toolResponse) {
        const toolResponse = c as ToolResponseContent;
        toolResponseMap[toolResponse.u] = toolResponse;
      }
    });

    // 按原始顺序处理内容
    content.forEach((c, index) => {
      if (c.$type === MessageContentType.toolCall) {
        const toolCall = c as ToolCallContent;
        const toolResponse = toolResponseMap[toolCall.u];
        processedContent.push({
          $type: 'toolGroup',
          toolCall,
          toolResponse,
          originalIndex: index
        });
      } else if (c.$type !== MessageContentType.toolResponse) {
        // 跳过工具响应，因为它们已经被组合到工具调用中了
        processedContent.push(c);
      }
    });

    return processedContent;
  };

  const renderToolGroup = (toolGroup: ToolGroupContent, index: number) => {
    const { toolCall, toolResponse } = toolGroup;

    return (
      <ToolCallBlock
        key={`tool-group-${index}`}
        toolCall={toolCall}
        toolResponse={toolResponse}
        chatStatus={messageStatus}
      />
    );
  };

  useEffect(() => {
    setMessageContent(structuredClone(content));
  }, [content]);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  }, [editId]);

  // 使用最新的内容进行处理，但在编辑模式时使用本地状态
  const contentToProcess = editId !== EMPTY_ID ? messageContent : content;
  const processedContent = processContentInOrder(contentToProcess);

  // 收集所有图片URL用于预览（类型守卫确保 c 上有属性）
  const imageContents = contentToProcess.filter(
    (c): c is ResponseContent & { c: FileDef } =>
      c.$type === MessageContentType.fileId || c.$type === MessageContentType.tempFileId,
  );
  const allImageUrls = imageContents.map((c) => getFileUrl(c.c as FileDef));

  // 将连续的图片内容分组
  const groupedContent: (ProcessedContent | ProcessedContent[])[] = [];
  let currentImageGroup: ProcessedContent[] = [];
  
  processedContent.forEach((c) => {
    if (c.$type === MessageContentType.fileId || c.$type === MessageContentType.tempFileId) {
      currentImageGroup.push(c);
    } else {
      if (currentImageGroup.length > 0) {
        groupedContent.push(currentImageGroup);
        currentImageGroup = [];
      }
      groupedContent.push(c);
    }
  });
  
  // 处理最后一组图片
  if (currentImageGroup.length > 0) {
    groupedContent.push(currentImageGroup);
  }

  // 判断是否应该显示骨架动画
  const shouldShowSkeleton = 
    (messageStatus === ChatSpanStatus.Pending || messageStatus === ChatSpanStatus.Chatting) &&
    (!contentToProcess || contentToProcess.length === 0 || 
     (contentToProcess.length === 1 && contentToProcess[0].$type === MessageContentType.text && !contentToProcess[0].c));

  // 如果应该显示骨架动画，则直接返回骨架
  if (shouldShowSkeleton) {
    return (
      <div className="space-y-4">
        <MessageSkeleton />
      </div>
    );
  }

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

      {/* Render content in original order */}
      {groupedContent.map((item, groupIndex) => {
        // 如果是文件数组，用容器包裹并横向排列
        if (Array.isArray(item)) {
          return (
            <div key={`file-group-${groupIndex}`} className="flex flex-wrap gap-2">
              {item.map((c, index) => {
                if (c.$type === MessageContentType.fileId) {
                  return (
                    <FilePreview
                      key={'file-' + groupIndex + '-' + index}
                      file={c.c as FileDef}
                      onImageClick={handleImageClick}
                    />
                  );
                } else if (c.$type === MessageContentType.tempFileId) {
                  // 临时文件显示加载效果
                  const imageUrl = getFileUrl(c.c as FileDef);
                  const fileDef = c.c as FileDef;
                  const isImage = fileDef.contentType.startsWith('image/');
                  
                  if (isImage) {
                    return (
                      <div key={'temp-file-' + groupIndex + '-' + index} className="relative rounded-md overflow-hidden" style={{ maxWidth: 300, maxHeight: 300 }}>
                        <img
                          alt={t('Loading...')}
                          className="w-full h-full object-cover rounded-md cursor-pointer hover:opacity-90 transition-opacity"
                          src={imageUrl}
                          onClick={(e) => handleImageClick(imageUrl, allImageUrls, e)}
                        />
                        {/* 蓝色激光扫描效果 */}
                        <div className="absolute inset-0 pointer-events-none">
                          <div 
                            className="absolute w-full h-1 bg-gradient-to-r from-transparent via-blue-500 to-transparent shadow-[0_0_20px_rgba(59,130,246,0.8)]"
                            style={{
                              animation: 'scan 2s linear infinite',
                            }}
                          />
                        </div>
                        <style jsx>{`
                          @keyframes scan {
                            0% {
                              top: -4px;
                              opacity: 0;
                            }
                            10% {
                              opacity: 1;
                            }
                            90% {
                              opacity: 1;
                            }
                            100% {
                              top: 100%;
                              opacity: 0;
                            }
                          }
                        `}</style>
                      </div>
                    );
                  } else {
                    // 非图片临时文件显示普通加载状态
                    return (
                      <div key={'temp-file-' + groupIndex + '-' + index} className="relative">
                        <FilePreview
                          file={fileDef}
                          onImageClick={handleImageClick}
                          className="opacity-60 animate-pulse"
                        />
                      </div>
                    );
                  }
                }
                return null;
              })}
            </div>
          );
        }
        
        // 处理非图片内容
        const c = item as ProcessedContent;
        const index = groupIndex;
        
        if (c.$type === 'toolGroup') {
          return renderToolGroup(c, index);
        } else if (c.$type === MessageContentType.reasoning) {
          const finished = (c as any).finished as boolean | undefined;
          return (
            <ThinkingMessage
              key={'reasoning-' + index}
              content={c.c}
              finished={finished}
              reasoningDuration={message.reasoningDuration}
              messageId={message.id}
              chatId={chatId}
              chatShareId={chatShareId}
              chatStatus={chatStatus}
            />
          );
        } else if (c.$type === MessageContentType.text) {
          return editId === c.i ? (
            <div className="flex relative" key={'edit-text-' + c.i}>
              <div className="flex w-full flex-col flex-wrap rounded-md bg-muted">
                <Textarea
                  ref={textareaRef}
                  className="w-full h-auto outline-none resize-none whitespace-pre-wrap border-none rounded-md bg-muted"
                  value={contentText}
                  onChange={handleInputChange}
                  onKeyDown={handlePressEnter}
                  onCompositionStart={() => setIsTyping(true)}
                  onCompositionEnd={() => setIsTyping(false)}
                  style={{
                    fontFamily: 'inherit',
                    fontSize: 'inherit',
                    lineHeight: 'inherit',
                    padding: '10px',
                    paddingBottom: '60px',
                    margin: '0',
                    overflow: 'hidden',
                  }}
                />
                <div className="w-full flex justify-end p-3 gap-3">
                  <Button
                    variant="link"
                    className="rounded-md px-4 py-1 text-sm font-medium"
                    onClick={() => {
                      handleEditMessage(true);
                    }}
                    disabled={(contentText || '')?.trim().length <= 0}
                  >
                    {t('Save As Copy')}
                  </Button>
                  <Button
                    variant="default"
                    className="rounded-md px-4 py-1 text-sm font-medium"
                    onClick={() => {
                      handleEditMessage();
                    }}
                    disabled={(contentText || '')?.trim().length <= 0}
                  >
                    {t('Save')}
                  </Button>
                  <Button
                    variant="outline"
                    className="rounded-md border border-neutral-300 px-4 py-1 text-sm font-medium text-neutral-700 hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800"
                    onClick={(e) => {
                      setContentText('');
                      setEditId(EMPTY_ID);
                      e.stopPropagation();
                    }}
                  >
                    {t('Cancel')}
                  </Button>
                </div>
              </div>
            </div>
          ) : (
            <div key={'text-' + index} className="relative group/item">
              {message.displayType === 'Raw' ? (
                <div className="prose dark:prose-invert rounded-r-md flex-1 overflow-auto text-base py-2 px-3 group/item">
                  <div className="whitespace-pre-wrap font-mono">{c.c}</div>
                </div>
              ) : (
                <MemoizedReactMarkdown
                  // 顺序：math -> gfm -> breaks，确保数学与 GFM 处理后，再将 softbreak 转为 <br/>
                  remarkPlugins={[remarkMath, remarkGfm, remarkBreaks]}
                  rehypePlugins={[rehypeKatex as any]}
                  components={{
                    code({ node, className, inline, children, ...props }) {
                      if (children.length) {
                        if (children[0] == '▍') {
                          return (
                            <span className="animate-pulse cursor-default mt-1">
                              ▍
                            </span>
                          );
                        }
                      }

                      const match = /language-(\w+)/.exec(className || '');

                      return !inline ? (
                        <CodeBlock
                          key={Math.random()}
                          language={(match && match[1]) || ''}
                          value={String(children).replace(/\n$/, '')}
                          {...props}
                        />
                      ) : (
                        <code className={className} {...props}>
                          {children}
                        </code>
                      );
                    },
                    p({ children }) {
                      return <p className="md-p">{children}</p>;
                    },
                    table({ children }) {
                      return (
                        <table className="border-collapse border border-black px-3 py-1 dark:border-white">
                          {children}
                        </table>
                      );
                    },
                    th({ children }) {
                      return (
                        <th className="break-words border border-black bg-gray-500 px-3 py-1 text-white dark:border-white">
                          {children}
                        </th>
                      );
                    },
                    td({ children }) {
                      return (
                        <td className="break-words border border-black px-3 py-1 dark:border-white">
                          {children}
                        </td>
                      );
                    },
                  }}
                >
                  {`${preprocessLaTeX(c.c!)}${
                    (messageStatus === ChatSpanStatus.Pending || messageStatus === ChatSpanStatus.Chatting) && 
                    index === processedContent.length - 1 && 
                    c.$type === MessageContentType.text ? '▍' : ''
                  }`}
                </MemoizedReactMarkdown>
              )}
              <div className="absolute -bottom-0.5 right-0 z-10">
                {!isChatting(chatStatus) && (
                  <DropdownMenu>
                    <DropdownMenuTrigger
                      disabled={isChatting(messageStatus)}
                      className={cn(
                        'focus:outline-none invisible group-hover/item:visible bg-card rounded-full p-1',
                        readonly && 'hidden',
                      )}
                    >
                      <IconDots
                        className="rotate-90 hover:opacity-50"
                        size={16}
                      />
                    </DropdownMenuTrigger>
                    <DropdownMenuContent className="w-42 border-none">
                      <DropdownMenuItem
                        className="flex justify-start gap-3"
                        onClick={(e) => {
                          handleCopy(c.c);
                          e.stopPropagation();
                        }}
                      >
                        <IconCopy />
                        {t('Copy')}
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        className="flex justify-start gap-3"
                        onClick={(e) => {
                          handleToggleEditing(c.i, c.c);
                          e.stopPropagation();
                        }}
                      >
                        <IconEdit />
                        {t('Edit')}
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                )}
              </div>
            </div>
          );
        } else if (c.$type === MessageContentType.error) {
          return (
            message.status === ChatSpanStatus.Failed && (
              <ChatError key={'error-' + index} error={c.c} />
            )
          );
        } else {
          console.warn(c + ' not processed');
          return <></>;
        }
      })}
    </>
  );
};

export default ResponseMessage;
