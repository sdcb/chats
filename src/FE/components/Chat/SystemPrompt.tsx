import {
  FC,
  KeyboardEvent,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';

import useTranslation from '@/hooks/useTranslation';

import { Prompt, PromptSlim } from '@/types/prompt';

import { IconMessage } from '@/components/Icons';
import PromptList from './PromptList';

import { getUserPromptDetail } from '@/apis/clientApis';

const TEXTAREA_MAX_HEIGHT = 300;
const PROMPT_TRIGGER_PATTERN = /\/([^\s/]*)$/;

interface Props {
  currentPrompt: string | null;
  prompts: PromptSlim[];
  onChangePromptText: (prompt: string) => void;
  onChangePrompt: (prompt: Prompt) => void;
}

const SystemPrompt: FC<Props> = ({
  currentPrompt,
  prompts,
  onChangePromptText,
  onChangePrompt,
}) => {
  const { t } = useTranslation();

  const [rawValue, setRawValue] = useState<string>('');
  const [isEditing, setIsEditing] = useState(false); // 编辑模式状态
  const [activePromptIndex, setActivePromptIndex] = useState(0);
  const [showPromptList, setShowPromptList] = useState(false);
  const [promptInputValue, setPromptInputValue] = useState('');
  const [isScrollable, setIsScrollable] = useState(false);

  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const promptListRef = useRef<HTMLUListElement | null>(null);

  const filteredPrompts = prompts.filter((prompt) =>
    prompt.name.toLowerCase().includes(promptInputValue.toLowerCase()),
  );

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const inputValue = e.target.value;

    setRawValue(inputValue);
    updatePromptListVisibility(inputValue);

    onChangePromptText(inputValue);
  };

  const handleInitModal = (index?: number) => {
    const promptIndex = index !== undefined ? index : activePromptIndex;
    const selectedPrompt = filteredPrompts[promptIndex];
    selectedPrompt &&
      getUserPromptDetail(selectedPrompt.id).then((data) => {
        const updatedContent = rawValue.replace(
          PROMPT_TRIGGER_PATTERN,
          () => data.content,
        );
        setRawValue(updatedContent);
        onChangePromptText(updatedContent);
        onChangePrompt(data);
        setShowPromptList(false);
      });
  };

  const updatePromptListVisibility = useCallback((text: string) => {
    const match = text.match(PROMPT_TRIGGER_PATTERN);
    if (match) {
      setShowPromptList(true);
      setPromptInputValue(match[1]);
      setActivePromptIndex(0);
    } else {
      setShowPromptList(false);
      setPromptInputValue('');
      setActivePromptIndex(0);
    }
  }, []);

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (showPromptList) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        setActivePromptIndex((prevIndex) =>
          prevIndex < filteredPrompts.length - 1 ? prevIndex + 1 : prevIndex,
        );
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        setActivePromptIndex((prevIndex) =>
          prevIndex > 0 ? prevIndex - 1 : prevIndex,
        );
      } else if (e.key === 'Tab') {
        e.preventDefault();
        setActivePromptIndex((prevIndex) =>
          prevIndex < filteredPrompts.length - 1 ? prevIndex + 1 : 0,
        );
      } else if (e.key === 'Enter') {
        e.preventDefault();
        handleInitModal();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        setShowPromptList(false);
      } else {
        setActivePromptIndex(0);
      }
    }
  };

  useEffect(() => {
    if (!isEditing || !textareaRef.current) {
      if (isScrollable) {
        setIsScrollable(false);
      }
      return;
    }

    const textarea = textareaRef.current;
    textarea.style.height = 'auto';

    const { scrollHeight } = textarea;
    const clampedHeight = Math.min(scrollHeight, TEXTAREA_MAX_HEIGHT);
    textarea.style.height = `${clampedHeight}px`;

    const shouldScroll = scrollHeight > TEXTAREA_MAX_HEIGHT;
    if (isScrollable !== shouldScroll) {
      setIsScrollable(shouldScroll);
    }
  }, [isEditing, isScrollable, rawValue]);

  useEffect(() => {
    const rawContent = currentPrompt || '';
    if (rawValue !== rawContent) {
      setRawValue(rawContent);
    }
  }, [currentPrompt, rawValue]);

  useEffect(() => {
    const handleOutsideClick = (e: MouseEvent) => {
      if (
        promptListRef.current &&
        !promptListRef.current.contains(e.target as Node)
      ) {
        setShowPromptList(false);
      }
    };

    window.addEventListener('click', handleOutsideClick);

    return () => {
      window.removeEventListener('click', handleOutsideClick);
    };
  }, []);

  return (
    <div className="flex flex-col">
      <label className="mb-2 text-left text-neutral-700 dark:text-neutral-400 flex gap-1 items-center">
        <IconMessage size={20} />
        {t('System Prompt')}
      </label>
      {isEditing ? (
        <textarea
          ref={textareaRef}
          className="w-full rounded-lg border border-neutral-200 bg-transparent px-4 py-3 text-neutral-900 dark:border-neutral-600 dark:text-neutral-100"
          style={{
            resize: 'none',
            maxHeight: `${TEXTAREA_MAX_HEIGHT}px`,
            overflowY: isScrollable ? 'auto' : 'hidden',
            fontFamily: 'Consolas, "Courier New", monospace',
          }}
          placeholder={
            t(`Enter a prompt or type "/" to select a prompt...`) || ''
          }
          value={rawValue || ''}
          rows={1}
          onChange={handleChange}
          onKeyDown={handleKeyDown}
          onBlur={() => {
            setIsEditing(false);
            setShowPromptList(false);
          }}
          autoFocus
        />
      ) : (
        <div
          className="w-full rounded-lg border border-neutral-200 bg-transparent px-4 py-3 text-neutral-900 dark:border-neutral-600 dark:text-neutral-100 cursor-text min-h-[2.75rem]"
          style={{
            maxHeight: `${TEXTAREA_MAX_HEIGHT}px`,
            overflow: 'auto',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
          }}
          onClick={() => setIsEditing(true)}
        >
          {rawValue || (
            <span className="text-neutral-400">
              {t(`Enter a prompt or type "/" to select a prompt...`) || ''}
            </span>
          )}
        </div>
      )}

      {isEditing && showPromptList && filteredPrompts.length > 0 && (
        <div>
          <PromptList
            activePromptIndex={activePromptIndex}
            prompts={filteredPrompts}
            onSelect={handleInitModal}
            onMouseOver={setActivePromptIndex}
            promptListRef={promptListRef}
          />
        </div>
      )}
    </div>
  );
};

export default SystemPrompt;
