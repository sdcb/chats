import { useContext, useEffect, useMemo, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';


import { AdminModelDto } from '@/types/adminApis';
import { ChatSpanDto, McpServerListItemDto } from '@/types/clientApis';

import Tips from '@/components/Tips/Tips';

import { setChats } from '@/actions/chat.actions';
import {
  deleteChatMcp,
  getMcpServers,
  putChatMcp,
} from '@/apis/clientApis';
import HomeContext from '@/contexts/home.context';
import { cn } from '@/lib/utils';

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
  const updatingRef = useRef(false);
  const [updatingMcpId, setUpdatingMcpId] = useState<number | null>(null);

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
      .sort((a, b) => a.name.localeCompare(b.name) || a.id - b.id);
  }, [mcpServers]);

  if (toolCapableSpans.length === 0 || shortcutServers.length === 0) {
    return null;
  }

  const isMcpEnabled = (mcpId: number) => {
    return toolCapableSpans.some((span) =>
      (span.mcps || []).some((mcp) => mcp.id === mcpId),
    );
  };

  const hasNameConflict = (server: McpServerListItemDto) => {
    const normalizedName = server.name.toLowerCase();
    return toolCapableSpans.some((span) =>
      (span.mcps || []).some((mcp) => {
        if (mcp.id === server.id) return false;
        return mcpServers.find((candidate) => candidate.id === mcp.id)
          ?.name.toLowerCase() === normalizedName;
      }),
    );
  };

  const handleToggleMcp = async (mcpId: number) => {
    if (!selectedChat || disabled || updatingRef.current) return;

    updatingRef.current = true;
    setUpdatingMcpId(mcpId);
    const enable = !isMcpEnabled(mcpId);

    try {
      const updatedSpans = enable
        ? await putChatMcp(chatId, mcpId)
        : await deleteChatMcp(chatId, mcpId);

      const updatedChat = {
        ...selectedChat,
        spans: updatedSpans,
      };

      const updatedChats = chats.map((chat) =>
        chat.id === chatId ? updatedChat : chat,
      );
      chatDispatch(setChats(updatedChats));
    } catch (error) {
      console.error('Failed to toggle MCP shortcut:', error);
    } finally {
      updatingRef.current = false;
      setUpdatingMcpId(null);
    }
  };

  return (
    <div className="flex items-center gap-2 h-9">
      {shortcutServers.map((server) => {
        const enabled = isMcpEnabled(server.id);
        const nameConflict = !enabled && hasNameConflict(server);
        const displayLabel = server.displayName?.trim() || server.name;

        return (
          <Tips
            key={server.id}
            trigger={
              <button
                disabled={disabled || updatingMcpId !== null || nameConflict}
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
                : nameConflict
                ? t('An MCP server with the same name is already enabled')
                : t('MCP shortcut disabled: {{label}}', { label: displayLabel })
            }
          />
        );
      })}
    </div>
  );
};

export default McpShortcutControl;
