import { FC, memo, useState, useEffect } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';
import CopyButton from '@/components/Button/CopyButton';
import { ChatSpanStatus, ToolCallContent, ToolResponseContent } from '@/types/chat';
import { IconCheck, IconChevronDown, IconChevronRight, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

interface ToolCallBlockProps {
    toolCall: ToolCallContent;
    toolResponse?: ToolResponseContent;
    chatStatus?: ChatSpanStatus;
}

export const ToolCallBlock: FC<ToolCallBlockProps> = memo(({ toolCall, toolResponse, chatStatus }) => {
    const { t } = useTranslation();
    const [isParamsCopied, setIsParamsCopied] = useState<boolean>(false);
    const [isResponseCopied, setIsResponseCopied] = useState<boolean>(false);
    const [isOpen, setIsOpen] = useState<boolean>(true);

    // 根据 chatStatus 控制展开/收起状态
    useEffect(() => {
        if (chatStatus === ChatSpanStatus.Chatting) {
            // 流式输出时，保持展开
            setIsOpen(true);
        } else if (chatStatus === ChatSpanStatus.None || chatStatus === ChatSpanStatus.Failed) {
            // 流式输出完毕后，默认收起
            setIsOpen(false);
        }
    }, [chatStatus]);

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

    const toggleOpen = () => {
        setIsOpen(!isOpen);
    };

    return (
        <div className="codeblock relative font-sans text-[16px]">
            {/* Tool header - 统一的标题栏 */}
            <div 
                className={`flex items-center gap-2 py-[6px] px-3 bg-[#3d3d3d] cursor-pointer hover:bg-[#454545] transition-colors ${isOpen ? 'w-full justify-between' : 'inline-flex'}`}
                style={{ 
                    borderTopLeftRadius: 12, 
                    borderTopRightRadius: 12,
                    borderBottomLeftRadius: isOpen ? 0 : 12,
                    borderBottomRightRadius: isOpen ? 0 : 12,
                }}
                onClick={toggleOpen}
            >
                <div className="flex items-center gap-2">
                    <span className="text-blue-400">🔧</span>
                    <span className="text-sm text-white">{toolCall.n}</span>
                </div>
                <div className="flex items-center">
                    {isOpen ? (
                        <IconChevronDown size={18} stroke="#9ca3af" />
                    ) : (
                        <IconChevronRight size={18} stroke="#9ca3af" />
                    )}
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
                                            className="flex items-center rounded bg-none p-1 text-xs text-white hover:bg-white/10"
                                            onClick={copyToClipboard(code, true)}
                                        >
                                            {isParamsCopied ? (
                                                <IconCheck stroke={'white'} size={16} />
                                            ) : (
                                                <IconClipboard stroke={'white'} size={16} />
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
                            className="whitespace-pre-wrap break-words font-mono text-sm p-4 bg-[#282c34] text-[#abb2bf]"
                            style={{
                                borderBottomRightRadius: toolResponse ? 0 : 12,
                                borderBottomLeftRadius: toolResponse ? 0 : 12,
                            }}
                        >
                            {toolCall.p}
                        </div>
                        
                        {/* 参数区域的复制按钮 */}
                        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            className="flex items-center rounded bg-none p-1 text-xs text-white hover:bg-white/10"
                                            onClick={copyToClipboard(toolCall.p, true)}
                                        >
                                            {isParamsCopied ? (
                                                <IconCheck stroke={'white'} size={16} />
                                            ) : (
                                                <IconClipboard stroke={'white'} size={16} />
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
                    <div className="bg-[#3d3d3d] h-[1px]" />

                    {/* Response content */}
                    <div 
                        className="relative group whitespace-pre-wrap break-words text-sm p-4 bg-[#282c34] text-[#abb2bf]"
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
                                            className="flex items-center rounded bg-none p-1 text-xs text-white hover:bg-white/10"
                                            onClick={copyToClipboard(toolResponse.r, false)}
                                        >
                                            {isResponseCopied ? (
                                                <IconCheck stroke={'white'} size={16} />
                                            ) : (
                                                <IconClipboard stroke={'white'} size={16} />
                                            )}
                                        </button>
                                    </TooltipTrigger>
                                    <TooltipContent>
                                        {isResponseCopied ? t('Copied') : t('Click Copy')}
                                    </TooltipContent>
                                </Tooltip>
                            </TooltipProvider>
                        </div>
                        {toolResponse.r}
                    </div>
                </div>
            )}
        </div>
    );
});

ToolCallBlock.displayName = 'ToolCallBlock';

export default ToolCallBlock;