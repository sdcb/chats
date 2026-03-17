import { FC, memo, useState, useEffect } from 'react';
import { useTheme } from 'next-themes';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark, oneLight } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';
import { ChatSpanStatus, ToolCallContent, ToolResponseContent, ToolProgressDelta } from '@/types/chat';
import { IconCheck, IconChevronRight, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

interface ToolCallBlockProps {
    toolCall: ToolCallContent;
    toolResponse?: ToolResponseContent;
    chatStatus?: ChatSpanStatus;
    /**
     * 当后续有任何内容（包括另一个 tool call）开始输出后，自动收起。
     * 注意：不会覆盖用户手动展开/收起。
     */
    nextMessageContentStarted?: boolean;
}

interface WebSearchResult {
    type?: string;
    title?: string;
    url?: string;
    page_age?: string;
}

export const ToolCallBlock: FC<ToolCallBlockProps> = memo(({ toolCall, toolResponse, chatStatus, nextMessageContentStarted }) => {
    const { t } = useTranslation();
    const { resolvedTheme } = useTheme();
    const [isParamsCopied, setIsParamsCopied] = useState<boolean>(false);
    const [isResponseCopied, setIsResponseCopied] = useState<boolean>(false);
    // 计算 finished 状态：有 toolResponse 或者 聊天状态不是 Chatting (即已结束或失败)
    const finished = !!toolResponse || (chatStatus !== ChatSpanStatus.Chatting);

    const [isOpen, setIsOpen] = useState<boolean>(!(nextMessageContentStarted ?? false));
    const [isManuallyToggled, setIsManuallyToggled] = useState<boolean>(false);

    // 自动开合逻辑（不覆盖用户手动动作）
    // 目标：在下一个 message content（非 tool）开始前保持展开。
    useEffect(() => {
        if (isManuallyToggled) return;
        setIsOpen(!(nextMessageContentStarted ?? false));
    }, [nextMessageContentStarted, isManuallyToggled]);

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

    // 检查是否为web_search工具的结果数组
    const getWebSearchResults = (): WebSearchResult[] | null => {
        if (toolCall.n !== 'web_search' || !toolResponse) {
            return null;
        }
        try {
            const parsed = JSON.parse(toolResponse.r);
            if (Array.isArray(parsed) && parsed.length > 0 && parsed[0].type === 'web_search_result') {
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
        if (!navigator.clipboard || !navigator.clipboard.writeText) {
            return;
        }

        navigator.clipboard.writeText(text).then(() => {
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
    const webSearchResults = getWebSearchResults();
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
        
        // 根据工具名称选择图标
        let headerIcon = '🔧'; // 默认图标
        switch (toolCall.n) {
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
                    header: `${toolCall.n}: ${path}`, 
                    headerIcon, 
                    metadataLine: null, 
                    displayParams: toolCall.p 
                };
            }
        }

        // 默认情况
        return { header: toolCall.n, headerIcon, metadataLine: null, displayParams: toolCall.p };
    };

    const { header, headerIcon, metadataLine, displayParams } = getDisplayInfo();

    const toggleOpen = () => {
        setIsOpen(!isOpen);
        setIsManuallyToggled(true);
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
                        {webSearchResults ? (
                            <table className="w-full border-collapse text-left m-0">
                                <thead>
                                    <tr className="border-b border-border">
                                        <th className="py-1 pr-3 font-medium">{t('Title')}</th>
                                        <th className="py-1 px-3 font-medium whitespace-nowrap">{t('Age')}</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {webSearchResults.map((result, index) => (
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
                                            <td className="py-1 px-3 whitespace-nowrap">
                                                {result.page_age || '-'}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
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