import { FC, memo, useState } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

interface Props {
  language: string;
  value: string;
}

export const CodeBlockCore: FC<Props> = memo(({ language, value }) => {
  const { t } = useTranslation();
  const [isCopied, setIsCopied] = useState<boolean>(false);

  const copyToClipboard = (e: React.MouseEvent) => {
    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      return;
    }

    navigator.clipboard.writeText(value).then(() => {
      setIsCopied(true);

      setTimeout(() => {
        setIsCopied(false);
      }, 2000);
    });
    e.stopPropagation();
  };

  return (
    <div className="codeblock relative font-sans text-[16px]">
      <div className="flex items-center justify-between w-full py-[6px] px-3 bg-[#3d3d3d]">
        <span className="text-xs lowercase text-white">{language}</span>

        <div className="flex items-center">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  className="flex items-center rounded bg-none p-1 text-xs text-white hover:bg-white/10"
                  onClick={copyToClipboard}
                >
                  {isCopied ? (
                    <IconCheck stroke={'white'} size={16} />
                  ) : (
                    <IconClipboard stroke={'white'} size={16} />
                  )}
                </button>
              </TooltipTrigger>
              <TooltipContent>
                {isCopied ? t('Copied') : t('Click Copy')}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>
      </div>

      <SyntaxHighlighter
        language={language}
        style={oneDark}
        customStyle={{
          margin: 0,
          borderBottomRightRadius: 12,
          borderBottomLeftRadius: 12,
        }}
      >
        {value}
      </SyntaxHighlighter>
    </div>
  );
});

CodeBlockCore.displayName = 'CodeBlockCore';
