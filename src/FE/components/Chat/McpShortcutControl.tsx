import { useContext, useEffect, useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { getMcpDisplayLabel } from '@/utils/mcp';

import { AdminModelDto } from '@/types/adminApis';
import {
  ChatSpanDto,
  ChatSpanMcp,
  McpServerListItemDto,
} from '@/types/clientApis';

import Tips from '@/components/Tips/Tips';

import { setChats } from '@/actions/chat.actions';
import { getMcpServers, putChatSpan } from '@/apis/clientApis';
import HomeContext from '@/contexts/home.context';
import { cn } from '@/lib/utils';

const getNextMcps = (
  currentMcps: ChatSpanMcp[],
  mcpId: number,
  enable: boolean,
): ChatSpanMcp[] => {
  if (!enable) {
    return currentMcps.filter((mcp) => mcp.id !== mcpId);
  }

  return currentMcps.some((mcp) => mcp.id === mcpId)
    ? currentMcps
    : [...currentMcps, { id: mcpId }];
};

interface McpShortcutControlProps {
  chatId: string;
  spans: ChatSpanDto[];
  modelMap: Record<string, AdminModelDto>;
  disabled?: boolean;
}

const McpShortcutControl: React.FC<McpShortcutControlProps> = ({
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

  const [mcpServers, setMcpServers] = useState<McpServerListItemDto[]>([]);
  const [updatingMcpIds, setUpdatingMcpIds] = useState<Set<number>>(
    () => new Set(),
  );

  useEffect(() => {
    let cancelled = false;

    const loadMcpServers = async () => {
      try {
        const servers = await getMcpServers();
        if (!cancelled) {
          setMcpServers(servers || []);
        }
      } catch (error) {
        console.error('Failed to load MCP servers for shortcuts:', error);
        if (!cancelled) {
          setMcpServers([]);
        }
      }
    };

    loadMcpServers();

    return () => {
      cancelled = true;
    };
  }, []);

  const toolCapableSpans = useMemo(() => {
    return spans.filter((span) => {
      const model = modelMap[span.modelId];
      return model?.allowToolCall === true;
    });
  }, [spans, modelMap]);

  const shortcutServers = useMemo(() => {
    return mcpServers
      .filter((server) => server.showShortcut)
      .slice()
      .sort((a, b) => a.label.localeCompare(b.label) || a.id - b.id);
  }, [mcpServers]);

  if (toolCapableSpans.length === 0 || shortcutServers.length === 0) {
    return null;
  }

  const isMcpEnabled = (mcpId: number) => {
    return toolCapableSpans.some((span) =>
      (span.mcps || []).some((mcp) => mcp.id === mcpId),
    );
  };

  const handleToggleMcp = async (mcpId: number) => {
    if (!selectedChat || disabled || updatingMcpIds.has(mcpId)) return;

    setUpdatingMcpIds((current) => new Set(current).add(mcpId));
    const enable = !isMcpEnabled(mcpId);

    try {
      await Promise.all(
        toolCapableSpans.map((span) => {
          const currentMcps = span.mcps || [];
          const nextMcps = getNextMcps(currentMcps, mcpId, enable);

          return putChatSpan(span.spanId, chatId, {
            modelId: span.modelId,
            enabled: span.enabled,
            systemPrompt: span.systemPrompt,
            temperature: span.temperature,
            webSearchEnabled: span.webSearchEnabled,
            codeExecutionEnabled: span.codeExecutionEnabled,
            maxOutputTokens: span.maxOutputTokens,
            reasoningEffort: span.reasoningEffort,
            imageSize: span.imageSize,
            format: span.format,
            compression: span.compression,
            thinkingBudget: span.thinkingBudget,
            mcps: nextMcps,
          });
        }),
      );

      const updatedChat = {
        ...selectedChat,
        spans: selectedChat.spans.map((span) => {
          const model = modelMap[span.modelId];
          if (!model?.allowToolCall) {
            return span;
          }

          const currentMcps = span.mcps || [];
          const nextMcps = getNextMcps(currentMcps, mcpId, enable);

          return { ...span, mcps: nextMcps };
        }),
      };

      const updatedChats = chats.map((chat) =>
        chat.id === chatId ? updatedChat : chat,
      );
      chatDispatch(setChats(updatedChats));
    } catch (error) {
      console.error('Failed to toggle MCP shortcut:', error);
    } finally {
      setUpdatingMcpIds((current) => {
        const next = new Set(current);
        next.delete(mcpId);
        return next;
      });
    }
  };

  return (
    <div className="flex items-center gap-2 h-9">
      {shortcutServers.map((server) => {
        const enabled = isMcpEnabled(server.id);
        const isUpdating = updatingMcpIds.has(server.id);
        const displayLabel = getMcpDisplayLabel(server, shortcutServers);

        return (
          <Tips
            key={server.id}
            trigger={
              <button
                disabled={disabled || isUpdating}
                className={cn(
                  'h-full px-3 rounded-md flex items-center justify-center gap-1.5 transition-colors',
                  'text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed',
                  enabled
                    ? 'bg-primary text-primary-foreground hover:bg-primary/90'
                    : 'bg-transparent border border-input hover:bg-accent hover:text-accent-foreground',
                )}
                onClick={() => handleToggleMcp(server.id)}
              >
                <span>{displayLabel}</span>
              </button>
            }
            side="top"
            content={
              enabled
                ? t('MCP shortcut enabled: {{label}}', { label: displayLabel })
                : t('MCP shortcut disabled: {{label}}', { label: displayLabel })
            }
          />
        );
      })}
    </div>
  );
};

export default McpShortcutControl;
