import { isChatting } from '@/utils/chats';
import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { ChatSpanDto } from '@/types/clientApis';
import { ChatStatus, IChat, MessageContentType } from '@/types/chat';
import { IChatMessage, ReactionMessageType, getMessageContents, isAllStepsEdited } from '@/types/chatMessage';

import CopyAction from './CopyAction';
import DeleteAction from './DeleteAction';
import TurnInfoBubble from './TurnInfoBubble';
import PaginationAction from './PaginationAction';
import ReactionAction from './ReactionAction';
import RegenerateWithModelAction from './RegenerateWithModelAction';
import { IconDownload, IconLoader } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { downloadResponsePng } from '@/utils/downloadResponsePng';

const getCopyableMessageText = (message: IChatMessage): string => {
  const contents = getMessageContents(message);
  const textContent = contents
    .filter((content) => content.$type === MessageContentType.text)
    .map((content) => content.c)
    .join('');
  const errorContent = contents
    .filter((content) => content.$type === MessageContentType.error)
    .map((content) => content.c)
    .join('\n');

  return [textContent, errorContent].filter(Boolean).join('\n\n');
};

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
  onDeleteMessage?: (messageId: string) => Promise<void>;
  exportTargetId?: string;
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
    exportTargetId,
  } = props;
  const { t } = useTranslation();

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
  const [isDownloading, setIsDownloading] = useState(false);

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

  const handleDownload = async () => {
    if (!exportTargetId || isDownloading) return;

    const target = document.getElementById(exportTargetId);
    if (!target) {
      toast.error(t('Download failed'));
      return;
    }

    setIsDownloading(true);
    try {
      await downloadResponsePng(target, `chats-response-${messageId}.png`);
      toast.success(t('Download'));
    } catch (error) {
      console.error('Failed to download response PNG', error);
      toast.error(t('Download failed'));
    } finally {
      setIsDownloading(false);
    }
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
          text={getCopyableMessageText(message)}
        />

        <Tips
          side="bottom"
          content={t('Download')}
          trigger={
            <Button
              variant="ghost"
              className="p-1 m-0 h-7 w-7"
              disabled={isDownloading || messageReceiving || chatting}
              onClick={(event) => {
                event.stopPropagation();
                void handleDownload();
              }}
            >
              {isDownloading ? (
                <IconLoader />
              ) : (
                <IconDownload />
              )}
            </Button>
          }
        />

        <DeleteAction
          hidden={chatting}
          onDelete={async () => {
            await onDeleteMessage?.(messageId);
          }}
        />

        <TurnInfoBubble
          hidden={isAllStepsEdited(message)}
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
