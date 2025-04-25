import { isChatting } from '@/utils/chats';

import { AdminModelDto } from '@/types/adminApis';
import { ChatStatus, MessageContentType } from '@/types/chat';
import { IChatMessage, ReactionMessageType } from '@/types/chatMessage';

import ChangeModelAction from './ChangeModelAction';
import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import EditStatusAction from './EditStatusAction';
import GenerateInformationAction from './GenerateInformationAction';
import PaginationAction from './PaginationAction';
import ReactionBadResponseAction from './ReactionBadResponseAction';
import ReactionGoodResponseAction from './ReactionGoodResponseAction';
import RegenerateAction from './RegenerateAction';

interface Props {
  models: AdminModelDto[];
  message: IChatMessage;
  chatStatus: ChatStatus;
  readonly?: boolean;
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
    readonly,
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
            .map((x) => x.c)
            .join('')}
        />

        {message.edited && <EditStatusAction />}

        <DeleteAction
          hidden={siblingIds.length <= 1 || chatting}
          onDelete={() => {
            onDeleteMessage && onDeleteMessage(messageId);
          }}
        />

        <GenerateInformationAction
          hidden={message.edited}
          disabled={messageReceiving}
          message={message}
        />

        <ReactionGoodResponseAction
          disabled={chatting}
          value={message.reaction}
          onReactionMessage={handleReactionMessage}
        />
        <ReactionBadResponseAction
          disabled={chatting}
          value={message.reaction}
          onReactionMessage={handleReactionMessage}
        />

        <RegenerateAction
          hidden={readonly}
          disabled={chatting}
          onRegenerate={() => {
            onRegenerate && onRegenerate(parentId!, modelId);
          }}
        />
        <ChangeModelAction
          readonly={readonly || chatting}
          models={models}
          onChangeModel={(model) => {
            onRegenerate && onRegenerate(parentId!, model.modelId);
          }}
          showRegenerate={models.length > 0}
          modelName={modelName!}
          modelId={modelId}
        />
      </div>
    </div>
  );
};

export default ResponseMessageActions;
