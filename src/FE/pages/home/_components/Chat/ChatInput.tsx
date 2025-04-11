import {
  KeyboardEvent,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { hasMultipleSpans } from '@/utils/chats';
import { isMobile } from '@/utils/common';
import { formatPrompt } from '@/utils/promptVariable';

import {
  ChatRole,
  ChatStatus,
  ImageDef,
  Message,
  MessageContentType,
} from '@/types/chat';
import { Prompt } from '@/types/prompt';

import {
  IconArrowDown,
  IconCircleX,
  IconLoader,
  IconPaperclip,
  IconSend,
  IconStopFilled,
} from '@/components/Icons/index';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';

import HomeContext from '../../_contexts/home.context';
import UploadButton from '../Button/UploadButton';
import PasteUpload from '../PasteUpload/PasteUpload';
import PromptList from './PromptList';
import VariableModal from './VariableModal';

import { defaultFileConfig } from '@/apis/adminApis';
import { getUserPromptDetail } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface Props {
  onSend: (message: Message) => void;
  onScrollDownClick: () => void;
  onChangePrompt: (prompt: Prompt) => void;
  showScrollDownButton: boolean;
}

const ChatInput = ({
  onSend,
  onScrollDownClick,
  onChangePrompt,
  showScrollDownButton,
}: Props) => {
  const { t } = useTranslation();

  const {
    state: { prompts, selectedChat, modelMap, selectedMessages },
    handleStopChats,
  } = useContext(HomeContext);

  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const promptListRef = useRef<HTMLUListElement | null>(null);
  const [contentText, setContentText] = useState('');
  const [contentFiles, setContentFiles] = useState<ImageDef[]>([]);

  const [isTyping, setIsTyping] = useState<boolean>(false);
  const [uploading, setUploading] = useState<boolean>(false);
  const [showPromptList, setShowPromptList] = useState(false);
  const [activePromptIndex, setActivePromptIndex] = useState(0);
  const [promptInputValue, setPromptInputValue] = useState('');
  const [variables, setVariables] = useState<string[]>([]);
  const [isModalVisible, setIsModalVisible] = useState(false);

  const isMultiSpan = hasMultipleSpans(selectedMessages);

  const filteredPrompts = prompts.filter((prompt) =>
    prompt.name.toLowerCase().includes(promptInputValue.toLowerCase()),
  );
  const updatePromptListVisibility = useCallback((text: string) => {
    const match = text.match(/\/\w*$/);
    const textLength = text.length;
    const t = textLength > 0 ? text[textLength - 1] : '';

    if (match && t === '/') {
      setShowPromptList(true);
      setPromptInputValue(match[0].slice(1));
    } else {
      setShowPromptList(false);
      setPromptInputValue('');
    }
  }, []);

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = e.target.value;
    setContentText(value);
    updatePromptListVisibility(value);
  };

  const handleSend = () => {
    if (selectedChat.status === ChatStatus.Chatting) {
      return;
    }

    if (!contentText) {
      toast.error(t('Please enter a message'));
      return;
    }
    const fileIds = contentFiles.map((f) => ({
      i: '',
      $type: MessageContentType.fileId,
      c: f.id,
    }));
    onSend({
      role: ChatRole.User,
      content: [
        ...fileIds,
        { i: '', $type: MessageContentType.text, c: contentText },
      ],
    });
    setContentText('');
    setContentFiles([]);

    if (window.innerWidth < 640 && textareaRef && textareaRef.current) {
      textareaRef.current.blur();
    }
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (showPromptList) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        setActivePromptIndex((prevIndex) =>
          prevIndex < prompts.length - 1 ? prevIndex + 1 : prevIndex,
        );
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        setActivePromptIndex((prevIndex) =>
          prevIndex > 0 ? prevIndex - 1 : prevIndex,
        );
      } else if (e.key === 'Tab') {
        e.preventDefault();
        setActivePromptIndex((prevIndex) =>
          prevIndex < prompts.length - 1 ? prevIndex + 1 : 0,
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
    } else if (e.key === 'Enter' && !isTyping && !isMobile() && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const parseVariables = (content: string) => {
    const regex = /{{(.*?)}}/g;
    const foundVariables = [];
    let match;

    while ((match = regex.exec(content)) !== null) {
      foundVariables.push(match[1]);
    }

    return foundVariables;
  };

  const handlePromptSelect = (prompt: Prompt) => {
    const formatted = formatPrompt(prompt.content);
    const parsedVariables = parseVariables(formatted);
    onChangePrompt(prompt);
    setVariables(parsedVariables);

    if (parsedVariables.length > 0) {
      setIsModalVisible(true);
    } else {
      const text = contentText?.replace(/\/\w*$/, formatted);
      setContentText(text);

      updatePromptListVisibility(formatted);
    }
  };

  const handleInitModal = () => {
    const selectedPrompt = filteredPrompts[activePromptIndex];
    selectedPrompt &&
      getUserPromptDetail(selectedPrompt.id).then((data) => {
        setContentText((prevContent) => {
          return prevContent?.replace(/\/\w*$/, data.content);
        });
        handlePromptSelect(data);
        setShowPromptList(false);
      });
  };

  const handleSubmit = (updatedVariables: string[]) => {
    const newContent = contentText?.replace(/{{(.*?)}}/g, (_, variable) => {
      const index = variables.indexOf(variable);
      return updatedVariables[index];
    });

    setContentText(newContent);

    if (textareaRef && textareaRef.current) {
      textareaRef.current.focus();
    }
  };

  const canUploadFile = () => {
    return (
      !uploading &&
      contentFiles.length <= defaultFileConfig.count &&
      selectedChat.spans.filter((x) => modelMap[x.modelId]?.allowVision)
        .length > 0
    );
  };

  const handleUploadFailed = (reason: string | null) => {
    setUploading(false);
    if (reason) {
      toast.error(t(reason));
    } else {
      toast.error(t('File upload failed'));
    }
  };

  const handleUploadSuccessful = (def: ImageDef) => {
    setContentFiles((prev) => {
      return prev.concat(def);
    });
    setUploading(false);
  };

  const handleUploading = () => {
    setUploading(true);
  };

  useEffect(() => {
    if (textareaRef && textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current?.scrollHeight}px`;
      textareaRef.current.style.overflow = `${
        textareaRef?.current?.scrollHeight > 400 ? 'auto' : 'hidden'
      }`;
    }
  }, [contentText, contentFiles]);

  return (
    <div className="absolute bottom-0 left-0 w-full border-transparent bg-background">
      <div
        className={cn(
          'stretch flex flex-row rounded-md mx-auto w-full px-2 md:px-4',
          !isMultiSpan && 'w-full lg:w-11/12',
        )}
      >
        <div className="relative flex w-full flex-grow flex-col rounded-md bg-card shadow-[0_0_10px_rgba(0,0,0,0.10)] dark:shadow-[0_0_15px_rgba(0,0,0,0.10)] p-2">
          <div className="absolute mb-1 bottom-full mx-auto flex w-full justify-start z-10">
            {contentFiles.map((file, index) => (
              <div className="relative group shadow-sm" key={index}>
                <div className="mr-1 w-[4rem] h-[4rem] rounded overflow-hidden">
                  <img
                    src={file.url}
                    alt=""
                    className="w-full h-full object-cover shadow-sm"
                  />
                  <button
                    onClick={() => {
                      setContentFiles((prev) => {
                        return prev.filter((f) => f !== file);
                      });
                    }}
                    className="absolute top-[-4px] right-[0px]"
                  >
                    <IconCircleX
                      className="bg-background rounded-full text-black/50 dark:text-white/50"
                      size={20}
                    />
                  </button>
                </div>
              </div>
            ))}
          </div>

          {showScrollDownButton && (
            <Button
              className="absolute left-1/2 -translate-x-1/2 -top-10 w-auto h-auto rounded-full bg-card hover:bg-card z-50"
              onClick={onScrollDownClick}
            >
              <IconArrowDown />
            </Button>
          )}

          <Textarea
            ref={textareaRef}
            className="m-0 w-full resize-none border-none outline-none rounded-md p-3 bg-card"
            style={{
              resize: 'none',
              bottom: `${textareaRef?.current?.scrollHeight}px`,
              maxHeight: '400px',
              minHeight: '20px',
              overflow: `${
                textareaRef.current && textareaRef.current.scrollHeight > 400
                  ? 'auto'
                  : 'hidden'
              }`,
            }}
            placeholder={
              t('Type a message or type "/" to select a prompt...') || ''
            }
            value={contentText}
            rows={1}
            onCompositionStart={() => setIsTyping(true)}
            onCompositionEnd={() => setIsTyping(false)}
            onChange={handleChange}
            onKeyDown={handleKeyDown}
          />

          <div className="flex px-3 justify-between">
            <div className="">
              {canUploadFile() && (
                <UploadButton
                  fileConfig={defaultFileConfig}
                  onUploading={handleUploading}
                  onFailed={handleUploadFailed}
                  onSuccessful={handleUploadSuccessful}
                >
                  <IconPaperclip />
                </UploadButton>
              )}
              {canUploadFile() && (
                <PasteUpload
                  fileConfig={defaultFileConfig}
                  onUploading={handleUploading}
                  onFailed={handleUploadFailed}
                  onSuccessful={handleUploadSuccessful}
                />
              )}

              {uploading && <IconLoader className="animate-spin" />}
            </div>
            <div className="">
              <Button
                className="rounded-sm p-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                onClick={handleSend}
              >
                {selectedChat.status === ChatStatus.Chatting ? (
                  <IconStopFilled
                    onClick={handleStopChats}
                    className="h-4 w-4"
                  />
                ) : (
                  <IconSend />
                )}
              </Button>
            </div>
          </div>

          {showPromptList && filteredPrompts.length > 0 && (
            <div className="absolute bottom-12 w-full">
              <PromptList
                activePromptIndex={activePromptIndex}
                prompts={filteredPrompts}
                onSelect={handleInitModal}
                onMouseOver={setActivePromptIndex}
                promptListRef={promptListRef}
              />
            </div>
          )}

          {isModalVisible && (
            <VariableModal
              prompt={filteredPrompts[activePromptIndex]}
              variables={variables}
              onSubmit={handleSubmit}
              onClose={() => setIsModalVisible(false)}
            />
          )}
        </div>
      </div>
      <div className="px-3 pt-1 pb-2 text-center text-[11px] text-black/50 dark:text-white/50 md:px-4 md:pt-2 md:pb-2">
        {t(
          'Large language models may generate misleading error messages, please validate key information.',
        )}
      </div>
    </div>
  );
};
export default ChatInput;
