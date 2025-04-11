import { FC, memo, useState } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/cjs/styles/prism';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconClipboard } from '@/components/Icons/index';

interface Props {
  language: string;
  value: string;
}

export const CodeBlock: FC<Props> = memo(({ language, value }) => {
  const { t } = useTranslation();
  const [isCopied, setIsCopied] = useState<Boolean>(false);

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
          <button
            className="flex gap-1.5 items-center rounded bg-none p-1 text-xs text-white"
            onClick={copyToClipboard}
          >
            {isCopied ? (
              <IconCheck stroke={'white'} size={18} />
            ) : (
              <IconClipboard stroke={'white'} size={18} />
            )}
            {isCopied ? t('Copied') : t('Click Copy')}
          </button>
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
CodeBlock.displayName = 'CodeBlock';
