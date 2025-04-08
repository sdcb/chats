import { isChatting } from '@/utils/chats';

import { AdminModelDto } from '@/types/adminApis';
import {
  ChatRole,
  ChatSpanStatus,
  MessageContentType,
  ResponseContent,
} from '@/types/chat';
import { ReactionMessageType } from '@/types/chatMessage';

import ChangeModelAction from './ChangeModelAction';
import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import EditAction from './EditAction';
import GenerateInformationAction from './GenerateInformationAction';
import PaginationAction from './PaginationAction';
import ReactionBadResponseAction from './ReactionBadResponseAction';
import ReactionGoodResponseAction from './ReactionGoodResponseAction';
import RegenerateAction from './RegenerateAction';

export interface ResponseMessage {
  id: string;
  siblingIds: string[];
  parentId: string | null;
  role: ChatRole;
  content: ResponseContent[];
  inputTokens: number;
  outputTokens: number;
  reasoningTokens: number;
  reasoningDuration: number;
  inputPrice: number;
  outputPrice: number;
  duration: number;
  firstTokenLatency: number;
  modelId: number;
  modelName: string;
  modelProviderId: number;
  reaction: boolean | null;
  edited?: boolean;
}

interface Props {
  models: AdminModelDto[];
  message: ResponseMessage;
  chatStatus: ChatSpanStatus;
  readonly?: boolean;
  onToggleEditingMessage?: (messageId: string) => void;
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
    onToggleEditingMessage,
    onChangeMessage,
    onRegenerate,
    onReactionMessage,
    onDeleteMessage,
  } = props;

  const { id: messageId, siblingIds, modelId, modelName, parentId } = message;
  const currentMessageIndex = siblingIds.findIndex((x) => x === messageId);

  const handleReactionMessage = (type: ReactionMessageType) => {
    onReactionMessage && onReactionMessage(type, messageId);
  };

  return (
    <>
      {isChatting(chatStatus) ? (
        <div className="h-9"></div>
      ) : (
        <div className="flex gap-1 flex-wrap mt-1">
          <PaginationAction
            hidden={siblingIds.length <= 1}
            disabledPrev={currentMessageIndex === 0}
            disabledNext={currentMessageIndex === siblingIds.length - 1}
            messageIds={siblingIds}
            currentSelectIndex={currentMessageIndex}
            onChangeMessage={onChangeMessage}
          />
          <div className="visible flex gap-0 items-center">
            <CopyAction
              text={message.content
                .filter((x) => x.$type === MessageContentType.text)
                .map((x) => x.c)
                .join('')}
            />

            <DeleteAction
              hidden={siblingIds.length <= 1}
              onDelete={() => {
                onDeleteMessage && onDeleteMessage(messageId);
              }}
            />

            <GenerateInformationAction
              hidden={message.edited}
              message={message}
            />

            <ReactionGoodResponseAction
              value={message.reaction}
              onReactionMessage={handleReactionMessage}
            />
            <ReactionBadResponseAction
              value={message.reaction}
              onReactionMessage={handleReactionMessage}
            />

            <RegenerateAction
              hidden={readonly}
              onRegenerate={() => {
                onRegenerate && onRegenerate(parentId!, modelId);
              }}
            />
            <ChangeModelAction
              readonly={readonly}
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
      )}
    </>
  );
};

export default ResponseMessageActions;
