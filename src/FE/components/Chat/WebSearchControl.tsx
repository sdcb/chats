import { useContext, useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { ChatSpanDto } from '@/types/clientApis';

import Tips from '@/components/Tips/Tips';

import HomeContext from '@/contexts/home.context';
import { setChats } from '@/actions/chat.actions';
import { putChatSpan } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface WebSearchControlProps {
  chatId: string;
  spans: ChatSpanDto[];
  modelMap: Record<string, AdminModelDto>;
  disabled?: boolean;
}

const WebSearchControl: React.FC<WebSearchControlProps> = ({
  chatId,
  spans,
  modelMap,
  disabled = false,
}) => {
  const { t } = useTranslation();
  const {
    state: { chats },
    selectedChat,
    chatDispatch,
  } = useContext(HomeContext);

  const [isUpdating, setIsUpdating] = useState(false);

  const webSearchCapableSpans = useMemo(() => {
    return spans.filter((span) => {
      const model = modelMap[span.modelId];
      return model?.allowSearch === true;
    });
  }, [spans, modelMap]);

  const hasWebSearchCapability = webSearchCapableSpans.length > 0;

  const isAnyWebSearchEnabled = useMemo(() => {
    return webSearchCapableSpans.some((span) => span.webSearchEnabled);
  }, [webSearchCapableSpans]);

  if (!hasWebSearchCapability) {
    return null;
  }

  const handleToggleWebSearch = async () => {
    if (!selectedChat || isUpdating || disabled) return;

    setIsUpdating(true);
    const newValue = !isAnyWebSearchEnabled;

    try {
      await Promise.all(
        webSearchCapableSpans.map((span) =>
          putChatSpan(span.spanId, chatId, {
            modelId: span.modelId,
            enabled: span.enabled,
            systemPrompt: span.systemPrompt,
            temperature: span.temperature,
            webSearchEnabled: newValue,
            codeExecutionEnabled: span.codeExecutionEnabled,
            maxOutputTokens: span.maxOutputTokens,
            reasoningEffort: span.reasoningEffort,
            imageSize: span.imageSize,
            format: span.format,
            compression: span.compression,
            thinkingBudget: span.thinkingBudget,
            mcps: span.mcps,
          })
        )
      );

      const updatedChat = {
        ...selectedChat,
        spans: selectedChat.spans.map((span) => {
          const model = modelMap[span.modelId];
          if (model?.allowSearch) {
            return { ...span, webSearchEnabled: newValue };
          }
          return span;
        }),
      };

      const updatedChats = chats.map((chat) =>
        chat.id === chatId ? updatedChat : chat
      );
      chatDispatch(setChats(updatedChats));
    } catch (error) {
      console.error('Failed to toggle web search:', error);
    } finally {
      setIsUpdating(false);
    }
  };

  return (
    <div className="flex items-center h-9">
      <Tips
        trigger={
          <button
            disabled={disabled || isUpdating}
            className={cn(
              'h-full px-3 rounded-md flex items-center justify-center gap-1.5 transition-colors',
              'text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed',
              isAnyWebSearchEnabled
                ? 'bg-primary text-primary-foreground hover:bg-primary/90'
                : 'bg-transparent border border-input hover:bg-accent hover:text-accent-foreground'
            )}
            onClick={handleToggleWebSearch}
          >
            <span>{t('Smart Search')}</span>
          </button>
        }
        side="top"
        content={
          isAnyWebSearchEnabled
            ? t('Smart search enabled')
            : t('Smart search disabled')
        }
      />
    </div>
  );
};

export default WebSearchControl;
