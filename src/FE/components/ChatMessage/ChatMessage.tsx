import { FC, memo } from 'react';

import { hasMultipleSpans } from '@/utils/chats';

import { AdminModelDto } from '@/types/adminApis';
import { ChatRole, IChat, Message, ResponseContent } from '@/types/chat';
import { IChatMessage, MessageDisplayType, ReactionMessageType } from '@/types/chatMessage';

import ChatMessageHeader from './ChatMessageHeader';
import ResponseMessage from './ResponseMessage';
import ResponseMessageActions from './ResponseMessageActions';
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
  onChangeDisplayType?: (messageId: string, type: MessageDisplayType) => void;
  onRegenerateAllAssistant?: (messageId: string, modelId: number) => void;
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
    onChangeDisplayType,
    onRegenerateAllAssistant,
  }) => {
    const isMultiSpan = hasMultipleSpans(selectedMessages);
    return (
      <div
        className={cn(
          'w-full m-auto p-2 md:p-4',
          !isMultiSpan && 'w-full lg:w-11/12',
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
              {messages.map((message, index) => {
                return (
                  <div key={`message-${message.id}`}>
                    {message.role === ChatRole.User && (
                      <div
                        key={'user-message-' + index}
                        className={cn(
                          'prose w-full dark:prose-invert rounded-r-md group sm:w-[50vw] xl:w-[50vw]',
                          index > 0 && 'mt-4',
                        )}
                      >
                        <UserMessage
                          readonly={readonly}
                          selectedChat={selectedChat}
                          message={message}
                          onChangeMessage={onChangeChatLeafMessageId}
                          onEditAndSendMessage={onEditAndSendMessage}
                          onEditUserMessage={onEditUserMessage}
                          onDeleteMessage={onDeleteMessage}
                          onRegenerateAllAssistant={onRegenerateAllAssistant}
                        />
                      </div>
                    )}
                    {message.role === ChatRole.Assistant && (
                      <div className="group/item">
                        <ChatMessageHeader
                          readonly={readonly}
                          onChangeDisplayType={onChangeDisplayType}
                          message={message}
                        />
                        <div
                          onClick={() =>
                            isMultiSpan &&
                            onChangeChatLeafMessageId &&
                            onChangeChatLeafMessageId(message.id)
                          }
                          key={'response-group-message-' + index}
                          className={cn(
                            'border-[1px] border-background rounded-md flex w-full bg-card mb-1',
                            isMultiSpan &&
                              message.isActive &&
                              'border-primary/50 border-gray-300 dark:border-gray-600',
                            isMultiSpan && 'p-1 md:p-2',
                            !isMultiSpan && 'border-none',
                          )}
                        >
                          <div className="prose dark:prose-invert rounded-r-md flex-1 overflow-auto text-base py-1 px-2">
                            <ResponseMessage
                              key={'response-message-' + message.id + '-' + message.spanId}
                              chatStatus={selectedChat.status}
                              message={message}
                              readonly={readonly}
                              onEditResponseMessage={onEditResponseMessage}
                            />
                          </div>
                        </div>
                        <ResponseMessageActions
                          key={'response-actions-' + message.id}
                          readonly={readonly}
                          models={models}
                          chatStatus={selectedChat.status}
                          message={message}
                          onChangeMessage={onChangeChatLeafMessageId}
                          onReactionMessage={onReactionMessage}
                          onRegenerate={(
                            messageId: string,
                            modelId: number,
                          ) => {
                            onRegenerate &&
                              onRegenerate(message.spanId!, messageId, modelId);
                          }}
                          onDeleteMessage={onDeleteMessage}
                        />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          );
        })}
        <div ref={messagesEndRef} />
      </div>
    );
  },
);

ChatMessage.displayName = 'ChatMessage';
