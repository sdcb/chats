import { FC, memo } from 'react';

import { AdminModelDto } from '@/types/adminApis';
import { ChatRole, IChat, Message, ResponseContent } from '@/types/chat';
import { IChatMessage, ReactionMessageType } from '@/types/chatMessage';

import { IconRobot } from '../Icons';
import ResponseMessage from './ResponseMessage';
import UserMessage from './UserMessage';

import { cn } from '@/lib/utils';

export interface Props {
  selectedMessages: IChatMessage[][];
  selectedChat: IChat;
  models?: AdminModelDto[];
  messagesEndRef: any;
  readonly?: boolean;
  className?: string;
  onChangeChatLeafMessageId?: (messageId: string) => void;
  onEditAndSendMessage?: (editedMessage: Message, parentId?: string) => void;
  onRegenerate?: (spanId: number, messageId: string, modelId: number) => void;
  onReactionMessage?: (type: ReactionMessageType, messageId: string) => void;
  onEditResponseMessage?: (
    messageId: string,
    content: ResponseContent,
    isCopy?: boolean,
  ) => void;
  onEditUserMessage?: (messageId: string, content: ResponseContent) => void;
  onDeleteMessage?: (messageId: string) => void;
}

export const ChatMessage: FC<Props> = memo(
  ({
    selectedMessages,
    selectedChat,
    models = [],
    messagesEndRef,
    readonly,
    className,
    onChangeChatLeafMessageId,
    onEditAndSendMessage,
    onRegenerate,
    onReactionMessage,
    onEditResponseMessage,
    onEditUserMessage,
    onDeleteMessage,
  }) => {
    const hasMultipleSpan = selectedMessages.find((x) => x.length > 1);
    return (
      <div
        className={cn(
          'w-11/12 m-auto p-0 md:p-4',
          !hasMultipleSpan && 'w-full md:w-4/5',
          className,
        )}
      >
        {selectedMessages.map((messages, index) => {
          return (
            <div
              key={'message-group-' + index}
              className={cn(
                messages.find((x) => x.role === ChatRole.User)
                  ? 'flex w-full justify-end'
                  : 'md:grid md:grid-cols-[repeat(auto-fit,minmax(375px,1fr))] gap-4',
              )}
            >
              {messages.map((message) => {
                return (
                  <>
                    {message.role === ChatRole.User && (
                      <div
                        key={'user-message-' + message.id}
                        className={cn(
                          'prose w-full dark:prose-invert rounded-r-md group sm:w-[50vw] xl:w-[50vw]',
                          index > 0 && 'mt-6',
                        )}
                      >
                        <UserMessage
                          selectedChat={selectedChat}
                          message={message}
                          onChangeMessage={onChangeChatLeafMessageId}
                          onEditAndSendMessage={onEditAndSendMessage}
                          onEditUserMessage={onEditUserMessage}
                          onDeleteMessage={onDeleteMessage}
                        />
                      </div>
                    )}
                    {message.role === ChatRole.Assistant && (
                      <div
                        onClick={() =>
                          hasMultipleSpan &&
                          onChangeChatLeafMessageId &&
                          onChangeChatLeafMessageId(message.id)
                        }
                        key={'response-message-' + message.id}
                        className={cn(
                          'border-[1px] border-background rounded-md flex w-full group bg-card mt-4',
                          hasMultipleSpan &&
                            message.isActive &&
                            'border-primary/50 border-gray-400',
                          hasMultipleSpan && 'p-1 md:p-2',
                          !hasMultipleSpan && 'border-none',
                        )}
                      >
                        <div className="prose dark:prose-invert rounded-r-md flex-1 overflow-auto text-base pb-4 px-1">
                          <ResponseMessage
                            key={'response-message-' + message.id}
                            message={message}
                            readonly={readonly}
                            models={models}
                            onRegenerate={onRegenerate}
                            onReactionMessage={onReactionMessage}
                            onEditResponseMessage={onEditResponseMessage}
                            onChangeChatLeafMessageId={
                              onChangeChatLeafMessageId
                            }
                            onDeleteMessage={onDeleteMessage}
                          />
                        </div>
                      </div>
                    )}
                  </>
                );
              })}
            </div>
          );
        })}
        <div className="h-[162px] bg-background" ref={messagesEndRef} />
      </div>
    );
  },
);

ChatMessage.displayName = 'ChatMessage';
