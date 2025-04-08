import { useEffect, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isChatting, preprocessLaTeX } from '@/utils/chats';

import { AdminModelDto } from '@/types/adminApis';
import {
  ChatSpanStatus,
  ImageDef,
  MessageContentType,
  ResponseContent,
} from '@/types/chat';
import { ReactionMessageType } from '@/types/chatMessage';

import { CodeBlock } from '@/components/Markdown/CodeBlock';
import { MemoizedReactMarkdown } from '@/components/Markdown/MemoizedReactMarkdown';

import ChatError from '../ChatError/ChatError';
import { IconDots, IconEdit } from '../Icons';
import { Button } from '../ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import CopyAction from './CopyAction';
import ResponseMessageActions from './ResponseMessageActions';
import ThinkingMessage from './ThinkingMessage';

import rehypeKatex from 'rehype-katex';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';

export interface ResponseMessage {
  id: string;
  content: ResponseContent[];
  status: ChatSpanStatus;
  spanId: number | null;
  reasoningDuration?: number;
  parentId: string | null;
  siblingIds: string[];
}

interface Props {
  readonly?: boolean;
  message: ResponseMessage;
  models: AdminModelDto[];
  onChangeChatLeafMessageId?: (messageId: string) => void;
  onRegenerate?: (spanId: number, messageId: string, modelId: number) => void;
  onReactionMessage?: (type: ReactionMessageType, messageId: string) => void;
  onEditResponseMessage?: (
    messageId: string,
    content: ResponseContent,
    isCopy?: boolean,
  ) => void;
  onDeleteMessage?: (messageId: string) => void;
}

const ResponseMessage = (props: Props) => {
  const {
    message,
    readonly,
    models,
    onChangeChatLeafMessageId,
    onRegenerate,
    onReactionMessage,
    onEditResponseMessage,
    onDeleteMessage,
  } = props;
  const { t } = useTranslation();

  const { id: messageId, status: chatStatus, content } = message;
  const [isTyping, setIsTyping] = useState<boolean>(false);
  const [editId, setEditId] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [messageContent, setMessageContent] = useState(message.content);
  const [contentText, setContentText] = useState('');

  const handleEditMessage = (isCopyAndSave: boolean = false) => {
    const newContent = messageContent.find((c) => c.i === editId)!;
    newContent.c = contentText;
    onEditResponseMessage &&
      onEditResponseMessage(messageId, newContent, isCopyAndSave);
    setEditId('');
  };

  const handleInputChange = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    setContentText(event.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  };

  const handlePressEnter = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !isTyping && !e.shiftKey) {
      e.preventDefault();
      handleEditMessage();
    }
  };

  const handleToggleEditing = (id: string, text: string) => {
    setContentText(text);
    setEditId(id);
  };

  useEffect(() => {
    setMessageContent(content);
  }, [content]);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  }, [editId]);

  return (
    <>
      {chatStatus === ChatSpanStatus.Pending && (
        <span className="animate-pulse">▍</span>
      )}
      {message.content.map((c) => {
        if (c.$type === MessageContentType.reasoning) {
          return (
            <ThinkingMessage
              key={c.i}
              content={c.c}
              chatStatus={message.status}
              reasoningDuration={message.reasoningDuration}
            />
          );
        } else if (c.$type === MessageContentType.fileId) {
          return (
            <img
              key={c.i}
              className="w-full md:w-1/2 rounded-md"
              src={(c.c as ImageDef).url}
            />
          );
        } else if (c.$type === MessageContentType.text) {
          return editId === c.i ? (
            <div className="flex relative" key={c.i}>
              <div className="flex w-full flex-col flex-wrap rounded-md bg-muted">
                <textarea
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
                      setEditId('');
                      e.stopPropagation();
                    }}
                  >
                    {t('Cancel')}
                  </Button>
                </div>
              </div>
            </div>
          ) : (
            <div key={c.i} className="relative group/item">
              <MemoizedReactMarkdown
                remarkPlugins={[remarkMath, remarkGfm]}
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

                      children[0] = (children[0] as string).replace('▍', '▍');
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
                  chatStatus === ChatSpanStatus.Chatting ? '▍' : ''
                }`}
              </MemoizedReactMarkdown>
              <div className="absolute -bottom-0.5 right-0">
                <DropdownMenu>
                  <DropdownMenuTrigger
                    disabled={isChatting(chatStatus)}
                    className="focus:outline-none invisible group-hover/item:visible"
                  >
                    <IconDots
                      className="rotate-90 hover:opacity-50"
                      size={16}
                    />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent className="w-42 border-none">
                    <DropdownMenuItem>
                      <CopyAction
                        text={c.c}
                        content={t('Copy')}
                        triggerClassName="w-full gap-3 justify-start py-2"
                      />
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      className="flex justify-start gap-3 ml-1"
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
              </div>
            </div>
          );
        } else if (c.$type === MessageContentType.error) {
          return (
            message.status === ChatSpanStatus.Failed && (
              <ChatError key={c.i} error={c.c} />
            )
          );
        } else {
          console.warn(c + ' not processed');
          return <></>;
        }
      })}
      <ResponseMessageActions
        key={'response-actions-' + message.id}
        readonly={readonly}
        models={models}
        chatStatus={message.status}
        message={message as any}
        onChangeMessage={onChangeChatLeafMessageId}
        onReactionMessage={onReactionMessage}
        onRegenerate={(messageId: string, modelId: number) => {
          onRegenerate && onRegenerate(message.spanId!, messageId, modelId);
        }}
        onDeleteMessage={onDeleteMessage}
      />
    </>
  );
};

export default ResponseMessage;
