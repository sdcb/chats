import { useState, useMemo, useContext } from 'react';
import { createPortal } from 'react-dom';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { ChatSpanDto } from '@/types/clientApis';

import { IconDocker } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import ChatSessionManagerWindow from '@/components/ChatSessionManager/ChatSessionManagerWindow';

import HomeContext from '@/contexts/home.context';
import { setChats } from '@/actions/chat.actions';
import { putChatSpan } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface CodeExecutionControlProps {
  chatId: string;
  spans: ChatSpanDto[];
  modelMap: Record<string, AdminModelDto>;
  disabled?: boolean;
}

/**
 * 代码执行控制组件 - Agent 样式
 * - 显示条件：至少一个 span 的 model 支持代码执行
 * - Agent 文字：未启用时线框，启用时填充
 * - 启用后展开显示 Docker 图标，点击可打开沙盒管理器
 */
const CodeExecutionControl: React.FC<CodeExecutionControlProps> = ({
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

  const [isSessionManagerOpen, setIsSessionManagerOpen] = useState(false);
  const [isUpdating, setIsUpdating] = useState(false);

  // 获取支持代码执行的 spans
  const codeExecutionCapableSpans = useMemo(() => {
    return spans.filter((span) => {
      const model = modelMap[span.modelId];
      return model?.allowCodeExecution === true;
    });
  }, [spans, modelMap]);

  // 是否有任何 span 支持代码执行
  const hasCodeExecutionCapability = codeExecutionCapableSpans.length > 0;

  // 是否有任何 span 的代码执行已启用
  const isAnyCodeExecutionEnabled = useMemo(() => {
    return codeExecutionCapableSpans.some((span) => span.codeExecutionEnabled);
  }, [codeExecutionCapableSpans]);

  // 如果没有支持代码执行的模型，不渲染
  if (!hasCodeExecutionCapability) {
    return null;
  }

  // 切换所有 span 的代码执行状态
  const handleToggleCodeExecution = async () => {
    if (!selectedChat || isUpdating || disabled) return;

    setIsUpdating(true);
    const newValue = !isAnyCodeExecutionEnabled;

    try {
      // 批量更新所有支持代码执行的 spans
      await Promise.all(
        codeExecutionCapableSpans.map((span) =>
          putChatSpan(span.spanId, chatId, {
            modelId: span.modelId,
            enabled: span.enabled,
            systemPrompt: span.systemPrompt,
            temperature: span.temperature,
            webSearchEnabled: span.webSearchEnabled,
            codeExecutionEnabled: newValue,
            maxOutputTokens: span.maxOutputTokens,
            reasoningEffort: span.reasoningEffort,
            thinkingBudget: span.thinkingBudget,
            mcps: span.mcps,
          })
        )
      );

      // 更新本地状态
      const updatedChat = {
        ...selectedChat,
        spans: selectedChat.spans.map((span) => {
          const model = modelMap[span.modelId];
          if (model?.allowCodeExecution) {
            return { ...span, codeExecutionEnabled: newValue };
          }
          return span;
        }),
      };

      const updatedChats = chats.map((chat) =>
        chat.id === chatId ? updatedChat : chat
      );
      chatDispatch(setChats(updatedChats));
    } catch (error) {
      console.error('Failed to toggle code execution:', error);
    } finally {
      setIsUpdating(false);
    }
  };

  // 打开沙盒管理器
  const handleOpenSessionManager = () => {
    setIsSessionManagerOpen(true);
  };

  return (
    <>
      <div className="flex items-center h-9">
        <div
          className={cn(
            'flex items-center h-full rounded-md overflow-hidden transition-all duration-300 ease-out',
            isAnyCodeExecutionEnabled
              ? 'bg-primary text-primary-foreground'
              : 'bg-transparent border border-input hover:bg-accent hover:text-accent-foreground'
          )}
        >
          {/* Agent 按钮 */}
          <Tips
            trigger={
              <button
                disabled={disabled || isUpdating}
                className={cn(
                  'h-full px-3 flex items-center justify-center transition-colors',
                  'font-mono text-sm font-medium',
                  'disabled:opacity-50 disabled:cursor-not-allowed',
                  isAnyCodeExecutionEnabled
                    ? 'hover:bg-primary/90'
                    : 'hover:bg-accent'
                )}
                onClick={handleToggleCodeExecution}
              >
                Agent
              </button>
            }
            side="top"
            content={
              isAnyCodeExecutionEnabled
                ? t('Code execution enabled')
                : t('Code execution disabled')
            }
          />

          {/* Docker 图标 - 只在启用时显示，带动画 */}
          <div
            className={cn(
              'flex items-center overflow-hidden transition-all duration-300 ease-out',
              isAnyCodeExecutionEnabled ? 'max-w-[40px] opacity-100' : 'max-w-0 opacity-0'
            )}
          >
            <div className="w-px h-5 bg-primary-foreground/30" />
            <Tips
              trigger={
                <button
                  disabled={disabled}
                  className={cn(
                    'h-9 w-9 flex items-center justify-center transition-colors',
                    'hover:bg-primary/90',
                    'disabled:opacity-50 disabled:cursor-not-allowed'
                  )}
                  onClick={handleOpenSessionManager}
                >
                  <IconDocker size={18} />
                </button>
              }
              side="top"
              content={t('Sandbox Manager')}
            />
          </div>
        </div>
      </div>

      {/* 沙盒管理窗口 - 使用 Portal 渲染到 body，避免被父容器限制 */}
      {typeof document !== 'undefined' &&
        createPortal(
          <ChatSessionManagerWindow
            chatId={chatId}
            open={isSessionManagerOpen}
            onOpenChange={setIsSessionManagerOpen}
          />,
          document.body
        )}
    </>
  );
};

export default CodeExecutionControl;
