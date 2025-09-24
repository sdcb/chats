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

import { isMobile } from '@/utils/common';
import { formatPrompt } from '@/utils/promptVariable';

import {
  ChatRole,
  ChatStatus,
  FileDef,
  Message,
  MessageContentType,
  getFileUrl,
} from '@/types/chat';
import { Prompt } from '@/types/prompt';

import {
  IconArrowCompactDown,
  IconArrowDown,
  IconArrowDoubleUp,
  IconArrowUp,
  IconArrowsDiagonal,
  IconArrowsDiagonalMinimize,
  IconCircleX,
  IconCamera,
  IconLoader,
  IconPaperclip,
  IconStopFilled,
} from '@/components/Icons/index';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import Tips from '@/components/Tips/Tips';

import { setShowChatInput } from '@/actions/setting.actions';
import HomeContext from '@/contexts/home.context';
import UploadButton from '../Button/UploadButton';
import PasteUpload from '../PasteUpload/PasteUpload';
import FilesPopover from '../Popover/FilesPopover';
import PromptList from './PromptList';
import VariableModal from './VariableModal';

import { defaultFileConfig } from '@/apis/adminApis';
import { getUserPromptDetail } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface Props {
  onSend: (message: Message) => void;
  onScrollDownClick: () => void;
  onScrollToTopClick: () => void;
  onScrollToPrevUserMessageClick: () => void;
  onChangePrompt: (prompt: Prompt) => void;
  showScrollDownButton: boolean;
  showScrollToTopButton: boolean;
  showScrollToPrevUserMessageButton: boolean;
}

const ChatInput = ({
  onSend,
  onScrollDownClick,
  onScrollToTopClick,
  onScrollToPrevUserMessageClick,
  onChangePrompt,
  showScrollDownButton,
  showScrollToTopButton,
  showScrollToPrevUserMessageButton,
}: Props) => {
  const { t } = useTranslation();

  const {
    state: { prompts, modelMap, showChatInput },
    selectedChat,
    handleStopChats,
    settingDispatch,
  } = useContext(HomeContext);

  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const promptListRef = useRef<HTMLUListElement | null>(null);
  const prevChatStatusRef = useRef<ChatStatus>(selectedChat?.status || ChatStatus.None);
  const [contentText, setContentText] = useState('');
  const [contentFiles, setContentFiles] = useState<FileDef[]>([]);

  const [isTyping, setIsTyping] = useState<boolean>(false);
  const [uploading, setUploading] = useState<boolean>(false);
  const [showPromptList, setShowPromptList] = useState(false);
  const [activePromptIndex, setActivePromptIndex] = useState(0);
  const [promptInputValue, setPromptInputValue] = useState('');
  const [variables, setVariables] = useState<string[]>([]);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [isFullWriting, setIsFullWriting] = useState(false);
  const [isCollapsedByChat, setIsCollapsedByChat] = useState(false);

  // 定义所有需要在hooks规则下的callbacks和effects
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

  useEffect(() => {
    if (isFullWriting) return;
    if (textareaRef && textareaRef.current) {
      textareaRef.current.style.height = 'inherit';
      textareaRef.current.style.height = `${textareaRef.current?.scrollHeight}px`;
      textareaRef.current.style.overflow = `${
        textareaRef?.current?.scrollHeight > 96 ? 'auto' : 'hidden'
      }`;
    }
  }, [contentText, contentFiles, isFullWriting]);

  // 监听聊天状态变化，实现自动收起/展开抽屉
  useEffect(() => {
    if (!selectedChat) return;

    const prevStatus = prevChatStatusRef.current;
    const currentStatus = selectedChat.status;

    // 从“非聊天” -> “聊天” 开始：标记本轮会话需要在结束时自动展开；如当前是展开，则先自动收起
    if (
      prevStatus !== ChatStatus.Chatting &&
      currentStatus === ChatStatus.Chatting
    ) {
      // 会话上下文开始：预期结束时自动展开，除非期间被用户手动修改
      setIsCollapsedByChat(true);
      if (showChatInput) {
        settingDispatch(setShowChatInput(false));
      }
    }

    // 从“聊天” -> “非聊天”（完成/失败）结束：若本轮是因聊天自动收起，且期间未被用户修改，则自动展开
    if (
      prevStatus === ChatStatus.Chatting &&
      currentStatus !== ChatStatus.Chatting
    ) {
      if (isCollapsedByChat && !showChatInput) {
        settingDispatch(setShowChatInput(true));
      }
      // 无论是否展开，结束后重置标记，新的聊天重新计算
      setIsCollapsedByChat(false);
    }

    // 更新上一次的状态
    prevChatStatusRef.current = currentStatus;
  }, [selectedChat?.status, showChatInput, settingDispatch, isCollapsedByChat]);

  // 如果没有选中的聊天，不渲染ChatInput
  if (!selectedChat) {
    return null;
  }

  const filteredPrompts = prompts.filter((prompt) =>
    prompt.name.toLowerCase().includes(promptInputValue.toLowerCase()),
  );

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
      $type: MessageContentType.fileId as const,
      c: f.id,
    }));
    onSend({
      role: ChatRole.User,
      content: [
        ...fileIds,
        { i: '', $type: MessageContentType.text as const, c: contentText },
      ],
    });
    setContentText('');
    setContentFiles([]);

    if (window.innerWidth < 640 && textareaRef && textareaRef.current) {
      textareaRef.current.blur();
    }
    handleFullWriting(false);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.ctrlKey && (e.key === 'F' || e.key === 'f' || e.code === 'KeyF')) {
      handleFullWriting(!isFullWriting);
      e.preventDefault();
      e.stopPropagation();
      return;
    }

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

  const handleFullWriting = (value: boolean) => {
    if (!textareaRef.current) return;
    if (textareaRef.current?.style?.minHeight) {
      textareaRef.current.style.maxHeight = value
        ? 'calc(100vh - 178px)'
        : '196px';
      textareaRef.current.style.height = value ? 'calc(100vh - 178px)' : '48px';
      textareaRef.current.style.overflow = `${
        textareaRef?.current?.scrollHeight > 48 ? 'auto' : 'hidden'
      }`;
    }
    if (!value) {
      textareaRef.current.scrollTop = textareaRef.current.scrollHeight;
    }
    setIsFullWriting(value);
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
      contentFiles.length < defaultFileConfig.count &&
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

  const handleUploadSuccessful = (def: FileDef) => {
    setContentFiles((prev) => {
      return prev.concat(def);
    });
    setUploading(false);
  };

  const handleUploading = () => {
    setUploading(true);
  };

  const handleFileSelect = (file: FileDef) => {
    if (!canUploadFile()) {
      toast.error(
        t('The number of attachments sent has reached the maximum limit'),
      );
      return;
    }
    if (contentFiles.some((f) => f.id === file.id)) {
      setContentFiles((prev) => {
        return prev.filter((f) => f.id !== file.id);
      });
    } else {
      setContentFiles((prev) => {
        return prev.concat(file);
      });
    }
  };

  const handleToggleVisibility = () => {
    // 用户手动切换时，重置因聊天而收起的标记
    setIsCollapsedByChat(false);
    settingDispatch(setShowChatInput(!showChatInput));
  };

  return (
    <div>
      {showChatInput ? (
        <div
          className={cn(
            'absolute bottom-0 left-0 w-full border-transparent bg-background',
          )}
        >
          <div
            className={cn(
              'stretch flex flex-row rounded-md mx-auto w-full px-2 md:px-4',
            )}
          >
            <div className="relative flex w-full flex-grow flex-col rounded-md bg-card shadow-[0_0_10px_rgba(0,0,0,0.10)] dark:shadow-[0_0_15px_rgba(0,0,0,0.10)] pl-0 pt-0 pr-0 pb-1">
              <div className="absolute mb-1 bottom-full mx-auto flex w-full justify-start z-10">
                {!isFullWriting &&
                  contentFiles.map((file, index) => (
                    <div className="relative group shadow-sm" key={index}>
                      <div className="mr-1 w-[4rem] h-[4rem] rounded overflow-hidden">
                        <img
                          src={getFileUrl(file)}
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

              {/* 滚动按钮组 - 水平排列 */}
              {/* 移除原来的位置，现在放到收起按钮同一排 */}
              <div className="flex px-1 items-center gap-1 md:gap-2 bg-muted/60 md:bg-muted rounded-t-md border-b border-border/40">
                <div
                  className={cn(
                    'flex items-center',
                    isFullWriting ? 'visible' : 'visible',
                  )}
                >
                  <div className="flex items-center">
                    {canUploadFile() && (
                      <UploadButton
                        fileConfig={defaultFileConfig}
                        onUploading={handleUploading}
                        onFailed={handleUploadFailed}
                        onSuccessful={handleUploadSuccessful}
                        capture={false}
                        inputId="upload-device"
                        tip={t('Upload from device')}
                        tipSide="top"
                      >
                        <IconPaperclip size={22} />
                      </UploadButton>
                    )}
                    {canUploadFile() && isMobile() && (
                      <UploadButton
                        fileConfig={defaultFileConfig}
                        onUploading={handleUploading}
                        onFailed={handleUploadFailed}
                        onSuccessful={handleUploadSuccessful}
                        capture={true}
                        inputId="upload-camera"
                        tip={t('Take photo')}
                        tipSide="top"
                      >
                        <IconCamera size={22} />
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

                    {uploading && (
                      <Button
                        disabled
                        className="rounded-sm m-1 h-9 w-9 p-0 bg-transparent hover:bg-muted flex items-center justify-center"
                      >
                        <IconLoader className="animate-spin" size={22} />
                      </Button>
                    )}
                  </div>
                  <div className="flex items-center">
                    <FilesPopover
                      onSelect={handleFileSelect}
                      selectedFiles={contentFiles}
                    />
                  </div>
                </div>
                <div className="flex flex-1" />
                <div className="flex items-center gap-1 md:gap-2">
                  {!isFullWriting && (
                    <div className="flex items-center gap-1 md:gap-2">
                      {/* 滚动到顶部按钮 */}
                      {showScrollToTopButton && (
                        <Tips
                          trigger={
                            <Button
                              className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                              onClick={onScrollToTopClick}
                            >
                              <IconArrowDoubleUp size={22} className="text-foreground/80" />
                            </Button>
                          }
                          side="bottom"
                          content={t('Scroll to top')}
                        />
                      )}

                      {/* 滚动到上一条用户消息按钮 */}
                      {showScrollToPrevUserMessageButton && (
                        <Tips
                          trigger={
                            <Button
                              className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                              onClick={onScrollToPrevUserMessageClick}
                            >
                              <IconArrowUp size={22} className="text-foreground/80" />
                            </Button>
                          }
                          side="bottom"
                          content={t('Scroll to previous user message')}
                        />
                      )}

                      {/* 滚动到底部按钮 */}
                      {showScrollDownButton && (
                        <Tips
                          trigger={
                            <Button
                              className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                              onClick={onScrollDownClick}
                            >
                              <IconArrowDown size={22} className="text-foreground/80" />
                            </Button>
                          }
                          side="bottom"
                          content={t('Scroll to bottom')}
                        />
                      )}

                      {/* 收起抽屉按钮 */}
                      <Tips
                        trigger={
                          <Button
                            className={cn(
                              'rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto'
                            )}
                            onClick={handleToggleVisibility}
                          >
                            <IconArrowCompactDown size={22} className="text-foreground/70" />
                          </Button>
                        }
                        side="bottom"
                        content={t('Collapse input')}
                      />
                    </div>
                  )}
                  {isFullWriting ? (
                    <Tips
                      trigger={
                        <Button
                          className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                          onClick={() => handleFullWriting(false)}
                        >
                          <IconArrowsDiagonalMinimize size={22} />
                        </Button>
                      }
                      side="bottom"
                      content={t('Exit fullscreen writing (Ctrl + F)')}
                    />
                  ) : (
                    <Tips
                      trigger={
                        <Button
                          className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                          onClick={() => handleFullWriting(true)}
                        >
                          <IconArrowsDiagonal size={22} />
                        </Button>
                      }
                      side="bottom"
                      content={t('Enter fullscreen writing (Ctrl + F)')}
                    />
                  )}
                </div>
              </div>
              <Textarea
                ref={textareaRef}
                className="m-0 w-full resize-none border-none outline-none rounded-md bg-transparent"
                style={{
                  bottom: `${textareaRef?.current?.scrollHeight}px`,
                  maxHeight: '196px',
                  minHeight: '48px',
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

              <div
                className={cn(
                  'flex p-1 justify-between items-end',
                  isFullWriting ? 'h-[4rem]' : 'h-auto',
                )}
              >
                <div className="flex flex-row px-3 pt-2 gap-1">
                  {isFullWriting &&
                    contentFiles.map((file, index) => (
                      <div className="relative group shadow-sm" key={index}>
                        <div className="mr-1 w-[4rem] h-[4rem] rounded overflow-hidden">
                          <img
                            src={getFileUrl(file)}
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
                <div className="flex flex-row gap-3 items-center">
                  <Tips
                    trigger={
                      <Button
                        className="rounded-sm w-20 h-9 mr-1"
                        onClick={
                          selectedChat.status === ChatStatus.Chatting
                            ? handleStopChats
                            : handleSend
                        }
                      >
                        {selectedChat.status === ChatStatus.Chatting ? (
                          <IconStopFilled className="h-4 w-4" />
                        ) : (
                          t('Send')
                        )}
                      </Button>
                    }
                    side="top"
                    content={`Enter ${t('Send')} / Ctrl Enter ${t('Line break')}`}
                  />
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
        </div>
      ) : (
        <div className="absolute bottom-0 left-0 w-full border-transparent bg-background">
          <div className="absolute right-2 md:right-4 -top-8 flex gap-1 md:gap-2 items-center z-10">
            {/* 滚动到顶部按钮 */}
            {showScrollToTopButton && (
              <Tips
                trigger={
                  <Button
                    className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                    onClick={onScrollToTopClick}
                  >
                    <IconArrowDoubleUp size={22} className="text-foreground/80" />
                  </Button>
                }
                side="bottom"
                content={t('Scroll to top')}
              />
            )}
            
            {/* 滚动到上一条用户消息按钮 */}
            {showScrollToPrevUserMessageButton && (
              <Tips
                trigger={
                  <Button
                    className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                    onClick={onScrollToPrevUserMessageClick}
                  >
                    <IconArrowUp size={22} className="text-foreground/80" />
                  </Button>
                }
                side="bottom"
                content={t('Scroll to previous user message')}
              />
            )}
            
            {/* 滚动到底部按钮 */}
            {showScrollDownButton && (
              <Tips
                trigger={
                  <Button
                    className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                    onClick={onScrollDownClick}
                  >
                    <IconArrowDown size={22} className="text-foreground/80" />
                  </Button>
                }
                side="bottom"
                content={t('Scroll to bottom')}
              />
            )}
            
            {/* 展开抽屉按钮 */}
            <Tips
              trigger={
                <Button
                  className="rounded-sm p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted w-auto h-auto"
                  onClick={handleToggleVisibility}
                >
                  <IconArrowCompactDown size={22} className={'rotate-180 text-foreground/70'} />
                </Button>
              }
              side="bottom"
              content={t('Expand input')}
            />

            {selectedChat.status === ChatStatus.Chatting && (
              <Tips
                trigger={
                  <Button
                    variant="destructive"
                    size="icon"
                    className="m-0.5 sm:m-1 h-8 w-8 rounded-sm"
                    onClick={handleStopChats}
                  >
                    <IconStopFilled size={16} />
                  </Button>
                }
                side="bottom"
                content={t('Stop Generating')}
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
};
export default ChatInput;
