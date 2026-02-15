import { FC, memo, useState } from 'react';
import { useTheme } from 'next-themes';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark, oneLight } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconClipboard } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

interface Props {
  language: string;
  value: string;
}

export const CodeBlockCore: FC<Props> = memo(({ language, value }) => {
  const { t } = useTranslation();
  const { resolvedTheme } = useTheme();
  const [isCopied, setIsCopied] = useState<boolean>(false);
  const baseTheme = resolvedTheme === 'dark' ? oneDark : oneLight;

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
    <div className="codeblock relative font-sans text-base group">
      <div
        className="relative bg-muted border"
        style={{
          overflow: 'hidden',
        }}
      >
        <div className="absolute right-2 top-2 flex items-center opacity-0 pointer-events-none transition-opacity duration-150 group-hover:opacity-100 group-hover:pointer-events-auto">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  className="flex items-center rounded bg-none p-1 text-xs text-muted-foreground"
                  onClick={copyToClipboard}
                >
                  {isCopied ? (
                    <IconCheck stroke={'currentColor'} size={20} />
                  ) : (
                    <IconClipboard stroke={'currentColor'} size={20} />
                  )}
                </button>
              </TooltipTrigger>
              <TooltipContent>
                {isCopied ? t('Copied') : t('Click Copy')}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>

        <div className="absolute right-2 bottom-2 text-xs text-muted-foreground opacity-0 pointer-events-none transition-opacity duration-150 group-hover:opacity-100 group-hover:pointer-events-auto">
          {language}
        </div>

        <SyntaxHighlighter
          language={language}
          style={baseTheme}
          customStyle={{
            margin: 0,
            background: 'transparent',
            borderRadius: '2px',
            padding:12,
          }}
          codeTagProps={{
            style: { background: 'transparent' },
          }}
          useInlineStyles
        >
          {value}
        </SyntaxHighlighter>
      </div>
    </div>
  );
});

CodeBlockCore.displayName = 'CodeBlockCore';
