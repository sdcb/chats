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
  chatShareId?: string;
  isAdminView?: boolean;
  responseMessageMinHeight?: string;
  responseMessageMinHeightGroupIndex?: number;
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
    chatShareId,
    isAdminView,
    responseMessageMinHeight,
    responseMessageMinHeightGroupIndex,
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
          'w-full m-auto p-2 md:p-4 overflow-x-hidden',
          className,
        )}
      >
        {selectedMessages.map((messages, groupIndex) => {
          const isUserMessageGroup = messages.find((x) => x.role === ChatRole.User);
          const shouldRenderResponseSpacer =
            !!responseMessageMinHeight &&
            responseMessageMinHeightGroupIndex === groupIndex &&
            !isUserMessageGroup;
          return (
            <div
              key={'message-group-' + groupIndex}
              className={cn(
                isUserMessageGroup
                  ? 'flex w-full justify-end'
                  : '',
              )}
            >
              {isUserMessageGroup ? (
                messages.map((message, index) => (
                  <div key={`message-${message.id}`} data-message-id={message.id} data-message-role={message.role}>
                    {message.role === ChatRole.User && (
                      <div
                        key={'user-message-' + index}
                        className={cn(
                          'prose w-full dark:prose-invert rounded-r-md group',
                          'sm:w-[50vw] xl:w-[50vw]',
                          index > 0 && 'mt-4',
                        )}
                        data-user-message-id={message.id}
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
                  </div>
                ))
              ) : (
                <>
                  <div
                    className="md:grid md:grid-cols-[repeat(auto-fit,minmax(375px,1fr))] gap-4"
                    data-response-content="true"
                    data-response-group-index={groupIndex}
                  >
                    {messages.map((message, index) => (
                      <div key={`message-${message.id}`} data-message-id={message.id} data-message-role={message.role}>
                        {message.role === ChatRole.Assistant && (
                          <div>
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
                                'border-[1px] border-background rounded-md flex w-full bg-card mb-1 chat-message-bg',
                                isMultiSpan &&
                                  message.isActive &&
                                  'border-primary/50 border-gray-300 dark:border-gray-600',
                                isMultiSpan && 'p-1 md:p-2',
                                !isMultiSpan && 'border-none',
                              )}
                            >
                              <div className="rounded-r-md flex-1 overflow-auto leading-4 font-normal py-2 px-3">
                                <ResponseMessage
                                  key={'response-message-' + message.id + '-' + message.spanId}
                                  chatStatus={selectedChat.status}
                                  message={message}
                                  readonly={readonly}
                                  chatId={selectedChat.id}
                                  chatShareId={chatShareId}
                                  onEditResponseMessage={onEditResponseMessage}
                                />
                              </div>
                            </div>
                            <ResponseMessageActions
                              key={'response-actions-' + message.id}
                              readonly={readonly}
                              models={models}
                              chatStatus={selectedChat.status}
                              selectedChat={selectedChat}
                              message={message}
                              chatShareId={chatShareId}
                              isAdminView={isAdminView}
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
                    ))}
                  </div>
                  {shouldRenderResponseSpacer && (
                    <div style={{ height: responseMessageMinHeight }} />
                  )}
                </>
              )}
            </div>
          );
        })}
        <div ref={messagesEndRef} />
      </div>
    );
  },
);

ChatMessage.displayName = 'ChatMessage';
