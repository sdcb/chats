import { isChatting } from '@/utils/chats';
import { useMemo } from 'react';

import { AdminModelDto } from '@/types/adminApis';
import { ChatSpanDto } from '@/types/clientApis';
import { ChatStatus, IChat, MessageContentType, TextContent } from '@/types/chat';
import { IChatMessage, ReactionMessageType } from '@/types/chatMessage';

import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import EditStatusAction from './EditStatusAction';
import GenerateInformationAction from './GenerateInformationAction';
import PaginationAction from './PaginationAction';
import ReactionAction from './ReactionAction';
import RegenerateWithModelAction from './RegenerateWithModelAction';

interface Props {
  models: AdminModelDto[];
  message: IChatMessage;
  chatStatus: ChatStatus;
  selectedChat: IChat;
  readonly?: boolean;
  chatShareId?: string;
  isAdminView?: boolean;
  onChangeMessage?: (messageId: string) => void;
  onRegenerate?: (messageId: string, modelId: number) => void;
  onReactionMessage?: (type: ReactionMessageType, messageId: string) => void;
  onDeleteMessage?: (messageId: string) => void;
}

const ResponseMessageActions = (props: Props) => {
  const {
    models,
    message,
    chatStatus,
    selectedChat,
    readonly,
    chatShareId,
    isAdminView,
    onChangeMessage,
    onRegenerate,
    onReactionMessage,
    onDeleteMessage,
  } = props;

  const {
    id: messageId,
    siblingIds,
    modelId,
    modelName,
    parentId,
    status: messageStatus,
  } = message;
  const currentMessageIndex = siblingIds.findIndex((x) => x === messageId);

  const chatting = isChatting(chatStatus);
  const messageReceiving = isChatting(messageStatus);

  // 根据"当前位置对应的 span（顶部设置）"确定重新生成所用模型；
  // 若无法对应（例如 span 被删），则禁用重新生成按钮。
  const { spanId } = message;
  const spans = selectedChat?.spans;
  const spanModel = useMemo(() => {
    if (!spans) return null;
    const s = spans.find((x: ChatSpanDto) => x.spanId === spanId);
    if (!s) return null;
    const m = models.find((mm) => mm.modelId === s.modelId);
    return {
      modelId: s.modelId,
      modelName: s.modelName || m?.name || modelName,
    } as { modelId: number; modelName?: string };
  }, [spanId, spans, models, modelName]);

  // 如果对应的 span 被删除了，则禁用重新生成功能
  const isSpanDeleted = !spanModel;
  const regenerateModelId = spanModel?.modelId ?? modelId;
  const regenerateModelName = spanModel?.modelName ?? modelName;

  const handleReactionMessage = (type: ReactionMessageType) => {
    onReactionMessage && onReactionMessage(type, messageId);
  };

  return (
    <div className="flex gap-1 flex-wrap">
      <PaginationAction
        hidden={siblingIds.length <= 1 || chatting}
        disabledPrev={currentMessageIndex === 0}
        disabledNext={currentMessageIndex === siblingIds.length - 1}
        messageIds={siblingIds}
        currentSelectIndex={currentMessageIndex}
        onChangeMessage={onChangeMessage}
      />
      <div className="flex gap-0 items-center">
        <CopyAction
          text={message.content
            .filter((x) => x.$type === MessageContentType.text)
            .map((x) => (x as TextContent).c)
            .join('')}
        />

        {message.steps.some(step => step.edited) && <EditStatusAction />}

        <DeleteAction
          hidden={chatting}
          onDelete={() => {
            onDeleteMessage && onDeleteMessage(messageId);
          }}
        />

        <GenerateInformationAction
          hidden={message.steps.some(step => step.edited)}
          disabled={messageReceiving}
          message={message}
          chatId={selectedChat.id}
          chatShareId={chatShareId}
          isAdminView={isAdminView}
        />

        <ReactionAction
          disabled={chatting}
          value={message.reaction}
          onReactionMessage={handleReactionMessage}
        />

        <RegenerateWithModelAction
          hidden={readonly}
          disabled={chatting || isSpanDeleted}
          models={models}
          regenerateModelName={regenerateModelName}
          onRegenerate={() => {
            onRegenerate && onRegenerate(parentId!, regenerateModelId);
          }}
          onChangeModel={(model) => {
            onRegenerate && onRegenerate(parentId!, model.modelId);
          }}
        />
      </div>
    </div>
  );
};

export default ResponseMessageActions;
