import { FC, memo, useEffect, useRef, useState } from 'react';
import { useTheme } from 'next-themes';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark, oneLight } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';
import { ChatSpanStatus, ToolCallContent, ToolResponseContent, ToolProgressDelta } from '@/types/chat';
import { IconCheck, IconChevronRight, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { isChatting } from '@/utils/chats';
import { copyTextToClipboard } from '@/utils/clipboard';

interface ToolCallBlockProps {
    toolCall: ToolCallContent;
    toolResponse?: ToolResponseContent;
    chatStatus?: ChatSpanStatus;
}

const COMPLETED_AUTO_CLOSE_DELAY_MS = 1000;

interface WebSearchResult {
    type?: string;
    title?: string;
    url?: string;
    page_age?: string;
}

interface WebSearchCallAction {
    type?: string;
    query?: string;
    queries?: string[];
    url?: string;
    pattern?: string;
}

export const ToolCallBlock: FC<ToolCallBlockProps> = memo(({ toolCall, toolResponse, chatStatus }) => {
    const { t } = useTranslation();
    const { resolvedTheme } = useTheme();
    const [isParamsCopied, setIsParamsCopied] = useState<boolean>(false);
    const [isResponseCopied, setIsResponseCopied] = useState<boolean>(false);
    const isLive = chatStatus !== undefined && isChatting(chatStatus);
    const hasCompleted = toolCall.completed === true
        || (toolResponse !== undefined && toolResponse.progress === undefined);
    const isActive = isLive && !hasCompleted;

    const [isOpen, setIsOpen] = useState<boolean>(isActive);
    const [isManuallyToggled, setIsManuallyToggled] = useState<boolean>(false);
    const autoCloseTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const hasBeenLiveRef = useRef(isLive);

    useEffect(() => {
        if (isLive) {
            hasBeenLiveRef.current = true;
        }
    }, [isLive]);

    // 每个工具块按自身生命周期独立开合，并始终尊重用户的手动选择。
    useEffect(() => {
        if (autoCloseTimerRef.current !== null) {
            clearTimeout(autoCloseTimerRef.current);
            autoCloseTimerRef.current = null;
        }

        if (isManuallyToggled) return;

        if (isActive) {
            setIsOpen(true);
        } else if (hasCompleted && hasBeenLiveRef.current) {
            // 保留最终结果一小段时间，然后自动收起。
            setIsOpen(true);
            autoCloseTimerRef.current = setTimeout(() => {
                autoCloseTimerRef.current = null;
                setIsOpen(false);
            }, COMPLETED_AUTO_CLOSE_DELAY_MS);
        } else {
            setIsOpen(false);
        }

        return () => {
            if (autoCloseTimerRef.current !== null) {
                clearTimeout(autoCloseTimerRef.current);
                autoCloseTimerRef.current = null;
            }
        };
    }, [hasCompleted, isActive, isManuallyToggled]);

    const baseTheme = resolvedTheme === 'dark' ? oneDark : oneLight;

    // 检查是否应该只显示code，并返回code内容
    const getCodeIfAvailable = (): string | null => {
        try {
            const parsedParams = JSON.parse(toolCall.p);
            // 检查第一个属性是否为"code"
            const keys = Object.keys(parsedParams);
            if (keys.length > 0 && keys[0] === 'code') {
                return parsedParams.code;
            }
        } catch (error) {
            // 如果解析失败，说明不是合法的JSON，不应该显示特殊处理
            return null;
        }

        return null;
    };

    const getResponseWebSearchResults = (): WebSearchResult[] | null => {
        if (toolCall.n !== 'web_search_call' || !toolResponse) {
            return null;
        }
        try {
            const parsed = JSON.parse(toolResponse.r);
            if (Array.isArray(parsed) && parsed.every(item => item?.type === 'web_search_result')) {
                return parsed as WebSearchResult[];
            }
        } catch {
            return null;
        }
        return null;
    };

    const getToolProgressDeltas = (): ToolProgressDelta[] | null => {
        const deltas = toolResponse?.progress;
        return deltas && deltas.length > 0 ? deltas : null;
    };

    const copyToClipboard = (text: string, isParams: boolean) => (e: React.MouseEvent) => {
        copyTextToClipboard(text).then((copied) => {
            if (!copied) return;

            if (isParams) {
                setIsParamsCopied(true);
                setTimeout(() => setIsParamsCopied(false), 2000);
            } else {
                setIsResponseCopied(true);
                setTimeout(() => setIsResponseCopied(false), 2000);
            }
        });
        e.stopPropagation();
    };

    const code = getCodeIfAvailable();
    const responseWebSearchResults = getResponseWebSearchResults();
    const toolProgressDeltas = getToolProgressDeltas();

    const deltaToText = (delta: ToolProgressDelta): string => {
        if (delta.kind === 'stdout') return delta.stdOutput;
        if (delta.kind === 'stderr') return delta.stdError;
        return '';
    };

    const parseToolCallJson = (): unknown | null => {
        try {
            return JSON.parse(toolCall.p);
        } catch {
            return null;
        }
    };

    const getToolCallJsonObject = (): Record<string, unknown> | null => {
        const parsed = parseToolCallJson();
        const obj = Array.isArray(parsed) ? parsed[0] : parsed;
        if (!obj || typeof obj !== 'object' || Array.isArray(obj)) {
            return null;
        }
        return obj as Record<string, unknown>;
    };

    const getWebSearchCallAction = (): WebSearchCallAction | null => {
        if (toolCall.n !== 'web_search_call') {
            return null;
        }
        const obj = getToolCallJsonObject();
        const action = obj?.action;
        if (!action || typeof action !== 'object' || Array.isArray(action)) {
            return null;
        }
        return action as WebSearchCallAction;
    };

    const hasSessionId = (obj: Record<string, unknown> | null): boolean => {
        return !!obj && Object.prototype.hasOwnProperty.call(obj, 'sessionId');
    };

    const getDisplayInfo = (): { 
        header: string; 
        headerIcon: string; 
        metadataLine: React.ReactNode | null; 
        displayParams: string 
    } => {
        const obj = getToolCallJsonObject();
        const baseDisplayName = toolCall.d ?? toolCall.n;
        
        // 根据工具名称选择图标
        let headerIcon = '🔧'; // 默认图标
        switch (toolCall.n) {
            case 'web_search_call':
                headerIcon = '🔎';
                break;
            case 'create_docker_session':
                headerIcon = '🐳';
                break;
            case 'destroy_session':
                headerIcon = '🗑️';
                break;
            case 'run_command':
                headerIcon = '⚡';
                break;
            case 'write_file':
                headerIcon = '✏️';
                break;
            case 'download_chat_files':
                headerIcon = '📥';
                break;
        }

        if (toolCall.n === 'web_search_call') {
            const action = getWebSearchCallAction();
            const status = typeof obj?.status === 'string' ? obj.status : null;
            const actionType = action?.type ?? 'web_search_call';
            const metadataLine = status ? (
                <span className='text-foreground font-sans font-semibold text-gray-600 dark:text-gray-100 text-sm'>
                    {t('Status')}: <span className='font-normal'>{status}</span>
                </span>
            ) : null;

            let header = `web_search_call: ${actionType}`;
            const searchQuery = action?.query
                ?? action?.queries?.find(query => !query.startsWith('ws_call_id='))
                ?? action?.queries?.[0];
            if (actionType === 'search' && searchQuery) {
                header = `${t('Web Search')}: ${searchQuery}`;
            } else if (actionType === 'open_page' && action?.url) {
                header = `${t('Open Page')}: ${action.url}`;
            } else if (actionType === 'find_in_page') {
                header = `${t('Find in Page')}: ${action?.pattern ?? action?.url ?? ''}`.trim();
            }

            const displayParams = JSON.stringify(action ?? obj ?? {}, null, 2);
            return { header, headerIcon, metadataLine, displayParams };
        }
        
        // run_command: 提取 header, metadata 和 command
        if (toolCall.n === 'run_command') {
            let header = toolCall.n;
            let metadataLine: React.ReactNode | null = null;
            let displayParams = toolCall.p;

            if (obj) {
                // 提取 header (command 本身)
                const command = obj.command;
                if (typeof command === 'string' && command.trim().length > 0) {
                    header = command;
                    displayParams = command;
                }

                // 构建 metadata line
                const parts: React.ReactNode[] = [];
                if (obj.sessionId !== undefined) {
                    parts.push(
                        <span key="sessionId" className='text-foreground font-sans font-semibold text-gray-600 dark:text-gray-100 text-sm'>
                            SessionId: <span className='font-normal'>{String(obj.sessionId)}</span>
                        </span>
                    );
                }
                if (obj.timeout !== undefined) {
                    parts.push(
                        <span key="timeout" className='text-foreground font-sans font-semibold text-gray-600 dark:text-gray-100 text-sm'>
                            Timeout: <span>{String(obj.timeout)}ms</span>
                        </span>
                    );
                }
                if (parts.length > 0) {
                    metadataLine = (
                        <>
                            {parts.map((part, index) => (
                                <span key={index}>
                                    {index > 0 && ', '}
                                    {part}
                                </span>
                            ))}
                        </>
                    );
                }
            }

            return { header, headerIcon, metadataLine, displayParams };
        }

        // write_file: 提取 header, metadata 和内容
        if (toolCall.n === 'write_file') {
            let header = toolCall.n;
            let metadataLine: React.ReactNode | null = null;
            let displayParams = toolCall.p;

            if (hasSessionId(obj)) {
                // 提取 header (path)
                const path = obj!.path;
                if (typeof path === 'string' && path.trim().length > 0) {
                    header = `${toolCall.n}: ${path}`;
                }

                // 构建 metadata line
                if (obj!.sessionId !== undefined) {
                    metadataLine = (
                        <div className='text-foreground font-sans font-semibold text-gray-600 dark:text-gray-100 text-sm'>
                            SessionId: <span className='font-normal'>{String(obj!.sessionId)}</span>
                        </div>
                    );
                }

                const text = obj?.text;
                displayParams = typeof text === 'string' ? text : toolCall.p;
            }

            return { header, headerIcon, metadataLine, displayParams };
        }

        // destroy_session: 提取 header
        if (toolCall.n === 'destroy_session') {
            let header = toolCall.n;
            
            if (obj) {
                const sessionId = obj.sessionId;
                if (typeof sessionId === 'string' && sessionId.trim().length > 0) {
                    header = `${toolCall.n}: ${sessionId}`;
                } else if (typeof sessionId === 'number') {
                    header = `${toolCall.n}: ${sessionId}`;
                }
            }

            return { header, headerIcon, metadataLine: null, displayParams: toolCall.p };
        }

        // 其他工具：检查是否有 path 字段
        if (obj) {
            const path = obj.path;
            if (typeof path === 'string' && path.trim().length > 0) {
                return { 
                    header: `${baseDisplayName}: ${path}`,
                    headerIcon, 
                    metadataLine: null, 
                    displayParams: toolCall.p 
                };
            }
        }

        // 默认情况
        return { header: baseDisplayName, headerIcon, metadataLine: null, displayParams: toolCall.p };
    };

    const { header, headerIcon, metadataLine, displayParams } = getDisplayInfo();

    const toggleOpen = () => {
        if (autoCloseTimerRef.current !== null) {
            clearTimeout(autoCloseTimerRef.current);
            autoCloseTimerRef.current = null;
        }
        setIsManuallyToggled(true);
        setIsOpen((currentlyOpen) => !currentlyOpen);
    };

    const renderWebSearchResultsTable = (results: WebSearchResult[]) => {
        const includeAge = results.some(result => !!result.page_age);
        return (
        <table className="w-full border-collapse text-left m-0">
            <thead>
                <tr className="border-b border-border">
                    <th className="py-1 pr-3 font-medium">{t('Title')}</th>
                    {includeAge && (
                        <th className="py-1 px-3 font-medium whitespace-nowrap">{t('Age')}</th>
                    )}
                </tr>
            </thead>
            <tbody>
                {results.length === 0 ? (
                    <tr>
                        <td className="py-1 pr-3 text-muted-foreground" colSpan={includeAge ? 2 : 1}>
                            {t('No sources')}
                        </td>
                    </tr>
                ) : (
                    results.map((result, index) => (
                        <tr key={index} className="border-b border-border last:border-b-0 hover:bg-muted/60">
                            <td className="py-1 pr-3" title={result.url}>
                                {result.url ? (
                                    <a
                                        href={result.url}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="text-blue-600 dark:text-blue-400 hover:underline"
                                        onClick={(e) => e.stopPropagation()}
                                    >
                                        {result.title || result.url}
                                    </a>
                                ) : (result.title || '-')}
                            </td>
                            {includeAge && (
                                <td className="py-1 px-3 whitespace-nowrap">
                                    {result.page_age || '-'}
                                </td>
                            )}
                        </tr>
                    ))
                )}
            </tbody>
        </table>
        );
    };

    return (
        <div className="codeblock relative font-sans text-base">
            {/* Tool header - 统一的标题栏 */}
            <div
                className="flex items-center gap-2 px-2 h-8 bg-muted cursor-pointer transition-all duration-200 ease-in-out"
                style={{
                    width: isOpen ? '100%' : 'fit-content',
                    maxWidth: '100%',
                    justifyContent: isOpen ? 'space-between' : 'flex-start',
                    borderTopLeftRadius: 12,
                    borderTopRightRadius: 12,
                    borderBottomLeftRadius: isOpen ? 0 : 12,
                    borderBottomRightRadius: isOpen ? 0 : 12,
                }}
                onClick={toggleOpen}
            >
                <div className="flex items-center gap-2 min-w-0">
                    <span>{headerIcon}</span>
                    <span className="text-sm truncate">{header}</span>
                </div>
                <div
                    className="flex items-center transition-transform duration-300 ease-in-out"
                    style={{ transform: isOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
                >
                    <IconChevronRight size={18} className="stroke-muted-foreground" />
                </div>
            </div>

            {/* Parameters content - 根据是否有code选择不同的渲染方式 */}
            <div 
                className="overflow-hidden transition-all duration-300 ease-in-out"
                style={{
                    maxHeight: isOpen ? '2000px' : '0',
                    opacity: isOpen ? 1 : 0,
                }}
            >
                {code !== null ? (
                    // 特殊的代码显示
                    <div className="relative group">
                        <div
                            className="bg-muted"
                            style={{
                                borderTopLeftRadius: 0,
                                borderTopRightRadius: 0,
                                borderBottomRightRadius: toolResponse ? 0 : 12,
                                borderBottomLeftRadius: toolResponse ? 0 : 12,
                                overflow: 'hidden',
                            }}
                        >
                            <SyntaxHighlighter
                                language="text"
                                style={baseTheme}
                                customStyle={{
                                    margin: 0,
                                    fontFamily: 'var(--font-mono)',
                                    background: 'transparent',
                                    borderRadius: 0,
                                }}
                                codeTagProps={{
                                    style: { background: 'transparent' },
                                }}
                                useInlineStyles
                            >
                                {code}
                            </SyntaxHighlighter>
                        </div>
                        
                        {/* 代码区域的复制按钮 */}
                        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            className="flex items-center rounded bg-none p-1 text-xs text-muted-foreground"
                                            onClick={copyToClipboard(code, true)}
                                        >
                                            {isParamsCopied ? (
                                                <IconCheck stroke="currentColor" size={20} />
                                            ) : (
                                                <IconClipboard stroke="currentColor" size={20} />
                                            )}
                                        </button>
                                    </TooltipTrigger>
                                    <TooltipContent>
                                        {isParamsCopied ? t('Copied') : t('Click Copy')}
                                    </TooltipContent>
                                </Tooltip>
                            </TooltipProvider>
                        </div>
                    </div>
                ) : (
                    // 普通的参数显示
                    <div className="relative group">
                        <div
                            className="whitespace-pre-wrap break-words text-sm p-4 bg-muted text-foreground font-mono"
                            style={{
                                borderBottomRightRadius: toolResponse ? 0 : 12,
                                borderBottomLeftRadius: toolResponse ? 0 : 12,
                            }}
                        >
                            {metadataLine && (
                                <div className="text-blue-600 dark:text-blue-400 mb-1 text-xs [&_strong]:text-blue-600 dark:[&_strong]:text-blue-400">
                                    {metadataLine}
                                </div>
                            )}
                            {displayParams}
                        </div>

                        {/* 参数区域的复制按钮 */}
                        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            className="flex items-center rounded bg-none p-1 text-xs text-muted-foreground"
                                            onClick={copyToClipboard(displayParams, true)}
                                        >
                                            {isParamsCopied ? (
                                                <IconCheck className="stroke-gray-600 dark:stroke-gray-300" size={20} />
                                            ) : (
                                                <IconClipboard className="stroke-gray-600 dark:stroke-gray-300" size={20} />
                                            )}
                                        </button>
                                    </TooltipTrigger>
                                    <TooltipContent>
                                        {isParamsCopied ? t('Copied') : t('Click Copy')}
                                    </TooltipContent>
                                </Tooltip>
                            </TooltipProvider>
                        </div>
                    </div>
                )}
            </div>

            {/* Tool response - 统一的响应区域 */}
            {toolResponse && (
                <div
                    className="overflow-hidden transition-all duration-300 ease-in-out"
                    style={{
                        maxHeight: isOpen ? '2000px' : '0',
                        opacity: isOpen ? 1 : 0,
                    }}
                >
                    {/* Separator line */}
                    <div className="bg-muted-foreground/20 h-[1px]" />

                    {/* Response content */}
                    <div
                        className="relative group text-sm bg-muted text-foreground p-2"
                        style={{
                            borderBottomRightRadius: 12,
                            borderBottomLeftRadius: 12,
                        }}
                    >
                        {/* 右上角的复制按钮 */}
                        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            className="flex items-center rounded bg-none p-1 text-xs text-muted-foreground"
                                            onClick={copyToClipboard(toolResponse.r, false)}
                                        >
                                            {isResponseCopied ? (
                                                <IconCheck className="stroke-muted-foreground" size={20} />
                                            ) : (
                                                <IconClipboard className="stroke-muted-foreground" size={20} />
                                            )}
                                        </button>
                                    </TooltipTrigger>
                                    <TooltipContent>
                                        {isResponseCopied ? t('Copied') : t('Click Copy')}
                                    </TooltipContent>
                                </Tooltip>
                            </TooltipProvider>
                        </div>
                        {responseWebSearchResults ? (
                            renderWebSearchResultsTable(responseWebSearchResults)
                        ) : (
                            toolProgressDeltas ? (
                                <pre className="not-prose whitespace-pre-wrap break-words font-mono">
                                    {toolProgressDeltas.map((d, idx) => (
                                        <span
                                            key={idx}
                                            className={
                                                d.kind === 'stderr'
                                                    ? 'text-red-600 dark:text-red-400'
                                                    : undefined
                                            }
                                        >
                                            {deltaToText(d)}
                                        </span>
                                    ))}
                                </pre>
                            ) : (
                                <div className="whitespace-pre-wrap break-words font-mono">
                                    {toolResponse.r}
                                </div>
                            )
                        )}
                    </div>
                </div>
            )}
        </div>
    );
});

ToolCallBlock.displayName = 'ToolCallBlock';

export default ToolCallBlock;
