import { FC, memo, useState } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';
import CopyButton from '@/components/Button/CopyButton';
import { ToolCallContent, ToolResponseContent } from '@/types/chat';
import { IconCheck, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

interface ToolCallBlockProps {
    toolCall: ToolCallContent;
    toolResponse?: ToolResponseContent;
}

export const ToolCallBlock: FC<ToolCallBlockProps> = memo(({ toolCall, toolResponse }) => {
    const { t } = useTranslation();
    const [isParamsCopied, setIsParamsCopied] = useState<boolean>(false);
    const [isResponseCopied, setIsResponseCopied] = useState<boolean>(false);

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

    return (
        <div className="codeblock relative font-sans text-[16px]">
            {/* Tool header - 统一的标题栏 */}
            <div className="flex items-center justify-between w-full py-[6px] px-3 bg-[#3d3d3d]" 
                 style={{ borderTopLeftRadius: 12, borderTopRightRadius: 12 }}>
                <div className="flex items-center gap-2">
                    <span className="text-blue-400">🔧</span>
                    <span className="text-sm text-white">{toolCall.n}</span>
                </div>
            </div>

            {/* Parameters content - 根据是否有code选择不同的渲染方式 */}
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

            {/* Tool response - 统一的响应区域 */}
            {toolResponse && (
                <>
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
                </>
            )}
        </div>
    );
});

ToolCallBlock.displayName = 'ToolCallBlock';

export default ToolCallBlock;