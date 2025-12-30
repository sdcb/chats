import { FC, memo, useState, useEffect, useRef, useLayoutEffect } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';
import CopyButton from '@/components/Button/CopyButton';
import { ChatSpanStatus, ToolCallContent, ToolResponseContent } from '@/types/chat';
import { IconCheck, IconChevronRight, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

interface ToolCallBlockProps {
    toolCall: ToolCallContent;
    toolResponse?: ToolResponseContent;
    chatStatus?: ChatSpanStatus;
}

interface WebSearchResult {
    type?: string;
    title?: string;
    url?: string;
    page_age?: string;
}

export const ToolCallBlock: FC<ToolCallBlockProps> = memo(({ toolCall, toolResponse, chatStatus }) => {
    const { t } = useTranslation();
    const [isParamsCopied, setIsParamsCopied] = useState<boolean>(false);
    const [isResponseCopied, setIsResponseCopied] = useState<boolean>(false);
    // 计算 finished 状态：有 toolResponse 或者 聊天状态不是 Chatting (即已结束或失败)
    const finished = !!toolResponse || (chatStatus !== ChatSpanStatus.Chatting);

    const [isOpen, setIsOpen] = useState<boolean>(!finished);
    const [isManuallyToggled, setIsManuallyToggled] = useState<boolean>(false);
    const headerMeasureRef = useRef<HTMLDivElement>(null);
    const [collapsedWidth, setCollapsedWidth] = useState<number | null>(null);

    useLayoutEffect(() => {
        if (typeof ResizeObserver === 'undefined' || !headerMeasureRef.current) {
            return;
        }

        const observer = new ResizeObserver((entries) => {
            const entry = entries[0];
            if (!entry) return;
            const width = entry.borderBoxSize?.[0]?.inlineSize ?? entry.contentRect.width;
            setCollapsedWidth(Math.ceil(width));
        });

        observer.observe(headerMeasureRef.current);
        return () => observer.disconnect();
    }, []);

    // 自动开合逻辑（不覆盖用户手动动作）- 仅依赖 finished，类似 ThinkingMessage
    useEffect(() => {
        if (isManuallyToggled) return;
        setIsOpen(!finished);
    }, [finished, isManuallyToggled]);

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

    const getHeaderTitle = (): string => {
        if (
            toolCall.n !== 'run_command' &&
            toolCall.n !== 'write_file' &&
            toolCall.n !== 'patch_file' &&
            toolCall.n !== 'read_file' &&
            toolCall.n !== 'destroy_session'
        ) {
            return toolCall.n;
        }

        const obj = getToolCallJsonObject();
        if (!obj) {
            return toolCall.n;
        }

        // run_command: JSON 且包含 sessionId
        if (toolCall.n === 'run_command') {
            if (!hasSessionId(obj)) {
                return toolCall.n;
            }
            const command = obj.command;
            if (typeof command === 'string' && command.trim().length > 0) {
                return `${toolCall.n}: ${command}`;
            }
            return toolCall.n;
        }

        // write_file/patch_file: JSON 且包含 sessionId
        if (!hasSessionId(obj)) {
            return toolCall.n;
        }

        // destroy_session: JSON 且包含 sessionId
        if (toolCall.n === 'destroy_session') {
            const sessionId = obj.sessionId;
            if (typeof sessionId === 'string' && sessionId.trim().length > 0) {
                return `${toolCall.n}: ${sessionId}`;
            }
            if (typeof sessionId === 'number') {
                return `${toolCall.n}: ${sessionId}`;
            }
            return toolCall.n;
        }

        // read_file: JSON 且包含 sessionId
        if (toolCall.n === 'read_file') {
            const path = obj.path;
            if (typeof path === 'string' && path.trim().length > 0) {
                return `${toolCall.n}: ${path}`;
            }
            return toolCall.n;
        }

        const path = obj.path;
        if (typeof path === 'string' && path.trim().length > 0) {
            return `${toolCall.n}: ${path}`;
        }

        return toolCall.n;
    };

    const headerTitle = getHeaderTitle();

    const getDisplayParams = (): string => {
        if (toolCall.n !== 'write_file' && toolCall.n !== 'patch_file') {
            return toolCall.p;
        }

        const obj = getToolCallJsonObject();
        if (!hasSessionId(obj)) {
            return toolCall.p;
        }

        if (toolCall.n === 'write_file') {
            const text = obj?.text;
            return typeof text === 'string' ? text : toolCall.p;
        }

        const patch = obj?.patch;
        return typeof patch === 'string' ? patch : toolCall.p;
    };

    const displayParams = getDisplayParams();

    const toggleOpen = () => {
        setIsOpen(!isOpen);
        setIsManuallyToggled(true);
    };

    return (
        <div className="codeblock relative font-sans text-[16px]">
            {/* Tool header - 统一的标题栏 */}
            <div
                className="flex items-center gap-2 py-[6px] px-3 bg-gray-200 dark:bg-gray-700 cursor-pointer hover:bg-gray-300 dark:hover:bg-gray-600 transition-all duration-200 ease-in-out"
                style={{
                    width: isOpen ? '100%' : collapsedWidth ? `${collapsedWidth}px` : 'fit-content',
                    maxWidth: '100%',
                    justifyContent: isOpen ? 'space-between' : 'flex-start',
                    borderTopLeftRadius: 12,
                    borderTopRightRadius: 12,
                    borderBottomLeftRadius: isOpen ? 0 : 12,
                    borderBottomRightRadius: isOpen ? 0 : 12,
                }}
                onClick={toggleOpen}
            >
                <div className="flex items-center gap-2">
                    <span>🔧</span>
                    <span className="text-sm text-gray-800 dark:text-white">{headerTitle}</span>
                </div>
                <div
                    className="flex items-center transition-transform duration-300 ease-in-out"
                    style={{ transform: isOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
                >
                    <IconChevronRight size={18} className="stroke-gray-500" />
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
                        <SyntaxHighlighter
                            language="text"
                            style={oneDark}
                            customStyle={{
                                margin: 0,
                                borderTopLeftRadius: 0,
                                borderTopRightRadius: 0,
                                borderBottomRightRadius: toolResponse ? 0 : 12,
                                borderBottomLeftRadius: toolResponse ? 0 : 12,
                            }}
                        >
                            {code}
                        </SyntaxHighlighter>
                        
                        {/* 代码区域的复制按钮 */}
                        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            className="flex items-center rounded bg-none p-1 text-xs hover:bg-white/10"
                                            onClick={copyToClipboard(code, true)}
                                        >
                                            {isParamsCopied ? (
                                                <IconCheck stroke="white" size={16} />
                                            ) : (
                                                <IconClipboard stroke="white" size={16} />
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
                            className="whitespace-pre-wrap break-words font-mono text-sm p-4 bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300"
                            style={{
                                borderBottomRightRadius: toolResponse ? 0 : 12,
                                borderBottomLeftRadius: toolResponse ? 0 : 12,
                            }}
                        >
                            {displayParams}
                        </div>

                        {/* 参数区域的复制按钮 */}
                        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            className="flex items-center rounded bg-none p-1 text-xs hover:bg-black/10 dark:hover:bg-white/10"
                                            onClick={copyToClipboard(displayParams, true)}
                                        >
                                            {isParamsCopied ? (
                                                <IconCheck className="stroke-gray-600 dark:stroke-gray-300" size={16} />
                                            ) : (
                                                <IconClipboard className="stroke-gray-600 dark:stroke-gray-300" size={16} />
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
                    <div className="bg-gray-300 dark:bg-gray-600 h-[1px]" />

                    {/* Response content */}
                    <div
                        className={`relative group text-sm bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 ${webSearchResults ? 'p-2' : 'p-4'}`}
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
                                            className="flex items-center rounded bg-none p-1 text-xs hover:bg-black/10 dark:hover:bg-white/10"
                                            onClick={copyToClipboard(toolResponse.r, false)}
                                        >
                                            {isResponseCopied ? (
                                                <IconCheck className="stroke-gray-600 dark:stroke-gray-300" size={16} />
                                            ) : (
                                                <IconClipboard className="stroke-gray-600 dark:stroke-gray-300" size={16} />
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
                                    <tr className="border-b border-gray-300 dark:border-gray-600">
                                        <th className="py-1 pr-3 font-medium">{t('Title')}</th>
                                        <th className="py-1 px-3 font-medium whitespace-nowrap">{t('Age')}</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {webSearchResults.map((result, index) => (
                                        <tr key={index} className="border-b border-gray-300 dark:border-gray-600 last:border-b-0 hover:bg-gray-200 dark:hover:bg-gray-700">
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
                            <div className="whitespace-pre-wrap break-words">
                                {toolResponse.r}
                            </div>
                        )}
                    </div>
                </div>
            )}
            <div
                ref={headerMeasureRef}
                aria-hidden="true"
                className="absolute -z-10 inline-flex items-center gap-2 py-[6px] px-3"
                style={{ visibility: 'hidden', pointerEvents: 'none', whiteSpace: 'nowrap' }}
            >
                <span>🔧</span>
                <span className="text-sm">{headerTitle}</span>
                <IconChevronRight size={18} className="stroke-gray-500" />
            </div>
        </div>
    );
});

ToolCallBlock.displayName = 'ToolCallBlock';

export default ToolCallBlock;