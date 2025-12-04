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
  IconCamera,
  IconLoader,
  IconPaperclip,
  IconStopFilled,
} from '@/components/Icons/index';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { SendButton } from '@/components/ui/send-button';
import { useSendMode } from '@/hooks/useSendMode';
import Tips from '@/components/Tips/Tips';

import { setShowChatInput } from '@/actions/setting.actions';
import HomeContext from '@/contexts/home.context';
import UploadButton from '../Button/UploadButton';
import PasteUpload from '../PasteUpload/PasteUpload';
import FilesPopover from '../Popover/FilesPopover';
import FilePreview from '@/components/FilePreview/FilePreview';
import PromptList from './PromptList';
import VariableModal from './VariableModal';

import { defaultFileConfig } from '@/apis/adminApis';
import { getUserPromptDetail } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

// 动画时长配置（与全屏动画一致）
const ANIMATION_DURATION_MS = 200;

// 文本框配置
const TEXTAREA_LINE_HEIGHT = 24;
const TEXTAREA_MIN_ROWS = 3;
const TEXTAREA_MAX_ROWS = 10;
const TEXTAREA_MIN_HEIGHT = TEXTAREA_LINE_HEIGHT * TEXTAREA_MIN_ROWS; // 72px
const TEXTAREA_MAX_HEIGHT = TEXTAREA_LINE_HEIGHT * TEXTAREA_MAX_ROWS; // 240px

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
  const [textareaHeight, setTextareaHeight] = useState<number | 'full'>(TEXTAREA_MIN_HEIGHT);
  // 动画状态：
  // 'idle' - 无动画
  // 'pre-expanding' - 准备展开（ChatInput在屏幕外，按钮可见）
  // 'expanding' - 展开中（ChatInput飞入，按钮飞出）
  // 'pre-collapsing' - 准备收起（ChatInput可见，按钮在屏幕外）
  // 'collapsing' - 收起中（ChatInput飞出，按钮飞入）
  const [animationState, setAnimationState] = useState<'idle' | 'pre-expanding' | 'expanding' | 'pre-collapsing' | 'collapsing'>('idle');
  // 用于控制实际渲染状态（动画结束后才真正隐藏）
  const [renderExpanded, setRenderExpanded] = useState(showChatInput);

  // 使用发送模式 hook (确保 hook 在组件顶层调用)
  const { sendMode } = useSendMode();

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

  // 计算内容所需高度
  const getContentHeight = useCallback(() => {
    if (!textareaRef.current) return TEXTAREA_MIN_HEIGHT;
    const el = textareaRef.current;
    const original = el.style.height;
    el.style.height = 'auto';
    const height = Math.min(Math.max(el.scrollHeight, TEXTAREA_MIN_HEIGHT), TEXTAREA_MAX_HEIGHT);
    el.style.height = original;
    return height;
  }, []);

  // 非全屏模式下，根据内容更新高度
  useEffect(() => {
    if (isFullWriting) return;
    setTextareaHeight(getContentHeight());
    if (textareaRef.current) {
      textareaRef.current.style.overflow = 
        textareaRef.current.scrollHeight > TEXTAREA_MAX_HEIGHT ? 'auto' : 'hidden';
    }
  }, [contentText, contentFiles, isFullWriting, getContentHeight]);

  // 监听聊天状态变化，实现自动收起/展开抽屉
  useEffect(() => {
    if (!selectedChat) return;

    const prevStatus = prevChatStatusRef.current;
    const currentStatus = selectedChat.status;

    // 从"非聊天" -> "聊天" 开始：标记本轮会话需要在结束时自动展开；如当前是展开，则先自动收起
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

    // 从"聊天" -> "非聊天"（完成/失败）结束：若本轮是因聊天自动收起，且期间未被用户修改，则自动展开
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
  }, [selectedChat, showChatInput, settingDispatch, isCollapsedByChat]);

  // 监听 showChatInput 变化，触发动画
  useEffect(() => {
    let animationTimer: ReturnType<typeof setTimeout> | null = null;
    let rafId: number | null = null;

    if (showChatInput) {
      // 展开：先设置 pre-expanding 状态，让元素在正确的初始位置渲染
      setAnimationState('pre-expanding');
      setRenderExpanded(true);
      // 双重 rAF 确保 DOM 已经渲染了初始位置
      rafId = requestAnimationFrame(() => {
        rafId = requestAnimationFrame(() => {
          setAnimationState('expanding');
          // 动画结束后重置状态
          animationTimer = setTimeout(() => {
            setAnimationState('idle');
          }, ANIMATION_DURATION_MS);
        });
      });
    } else {
      // 收起：先设置 pre-collapsing 状态，让按钮在屏幕外渲染
      setAnimationState('pre-collapsing');
      // 双重 rAF 确保 DOM 已经渲染了初始位置
      rafId = requestAnimationFrame(() => {
        rafId = requestAnimationFrame(() => {
          setAnimationState('collapsing');
          // 动画结束后隐藏 ChatInput
          animationTimer = setTimeout(() => {
            setRenderExpanded(false);
            setAnimationState('idle');
          }, ANIMATION_DURATION_MS);
        });
      });
    }

    return () => {
      if (animationTimer) clearTimeout(animationTimer);
      if (rafId) cancelAnimationFrame(rafId);
    };
  }, [showChatInput]);

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
    // 传递完整的 FileDef 对象，而不是只传 id
    const fileContents = contentFiles.map((f) => ({
      i: '',
      $type: MessageContentType.fileId as const,
      c: f, // 传递整个 FileDef 对象
    }));
    onSend({
      role: ChatRole.User,
      content: [
        ...fileContents,
        { i: '', $type: MessageContentType.text as const, c: contentText },
      ],
    });
    setContentText('');
    setContentFiles([]);
    setTextareaHeight(TEXTAREA_MIN_HEIGHT);

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
    } else {
      // 对于移动端，仍然使用原来的逻辑避免意外发送
      if (isMobile() && e.key === 'Enter' && !e.shiftKey) {
        return; // 让移动端用户必须点击发送按钮
      }
      
      // Alt+S 发送
      if (e.altKey && e.key.toLowerCase() === 's') {
        e.preventDefault();
        handleSend();
        return;
      }

      // 根据模式处理 Enter 键
      if (e.key === 'Enter' && !isTyping) {
        if (sendMode === 'enter' && !e.shiftKey && !e.ctrlKey) {
          e.preventDefault();
          handleSend();
        } else if (sendMode === 'ctrl-enter' && e.ctrlKey && !e.shiftKey) {
          e.preventDefault();
          handleSend();
        }
      }
    }
  };

  const handleFullWriting = (value: boolean) => {
    if (value) {
      setIsFullWriting(true);
      setTextareaHeight('full');
    } else if (isFullWriting && textareaRef.current) {
      // 只有从全屏退出时才执行动画逻辑
      const fullHeight = textareaRef.current.offsetHeight;
      const targetHeight = getContentHeight();
      setTextareaHeight(fullHeight);
      setIsFullWriting(false);
      requestAnimationFrame(() => {
        setTextareaHeight(targetHeight);
      });
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

  // 计算动画状态对应的 transform
  // ChatInput:
  //   - translateY(0): idle+展开, expanding(目标), pre-collapsing(起始)
  //   - translateY(100%): idle+收起, pre-expanding(起始), collapsing(目标)
  const inputTransform =
    (animationState === 'idle' && showChatInput) ||
    animationState === 'expanding' ||
    animationState === 'pre-collapsing'
      ? 'translateY(0)'
      : 'translateY(100%)';

  // 浮动按钮:
  //   - translateY(0): idle+收起, collapsing(目标), pre-expanding(起始)
  //   - translateY(100%): idle+展开, pre-collapsing(起始), expanding(目标)
  const buttonsTransform =
    (animationState === 'idle' && !showChatInput) ||
    animationState === 'collapsing' ||
    animationState === 'pre-expanding'
      ? 'translateY(0)'
      : 'translateY(100%)';

  return (
    <div className="absolute bottom-0 left-0 w-full z-20 overflow-hidden pointer-events-none min-h-[48px]">
      {/* 展开状态的 ChatInput */}
      {(renderExpanded || animationState !== 'idle') && (
        <div
          className="w-full border-transparent bg-background pointer-events-auto transition-transform ease-out"
          style={{
            transform: inputTransform,
            transitionDuration: `${ANIMATION_DURATION_MS}ms`,
          }}
        >
          <div
            className={cn(
              'stretch flex flex-row rounded-md mx-auto w-full px-2 md:px-4',
            )}
          >
            <div className="relative flex w-full flex-grow flex-col rounded-md bg-card shadow-[0_0_10px_rgba(0,0,0,0.10)] dark:shadow-[0_0_15px_rgba(0,0,0,0.10)] pl-0 pt-0 pr-0 pb-1">
              {/* 滚动按钮组 - 水平排列 */}
              {/* 移除原来的位置，现在放到收起按钮同一排 */}
              <div className="flex px-1 items-center gap-1 md:gap-2 bg-muted/60 md:bg-muted rounded-t-md border-b border-border/40">
                <div className="flex items-center gap-1 md:gap-2">
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
                        size="xs"
                        className="m-0.5 h-8 w-8 p-0 bg-transparent hover:bg-muted flex items-center justify-center"
                      >
                        <IconLoader className="animate-spin" size={22} />
                      </Button>
                    )}
                  </div>
                  <div className="flex items-center">
                    {canUploadFile() && (
                      <FilesPopover
                        onSelect={handleFileSelect}
                        selectedFiles={contentFiles}
                      />
                    )}
                  </div>
                </div>

                <div className="flex flex-1" />

                <div className="flex items-center gap-1 md:gap-2">
                  {/* 滚动到顶部按钮 */}
                  {showScrollToTopButton && (
                    <Tips
                      trigger={
                        <Button
                          size="xs"
                          className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                          size="xs"
                          className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                          size="xs"
                          className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                        size="xs"
                        className={cn(
                          'p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted'
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

                <div className="flex items-center gap-1 md:gap-2">
                  {isFullWriting ? (
                    <Tips
                      trigger={
                        <Button
                          size="xs"
                          className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                          size="xs"
                          className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
              {/* 非全屏模式下的文件预览 */}
              {!isFullWriting && contentFiles.length > 0 && (
                <div className="flex flex-row px-3 py-2 gap-2 border-b border-border/40">
                  {contentFiles.map((file, index) => (
                    <FilePreview
                      key={index}
                      file={file}
                      maxWidth={80}
                      maxHeight={80}
                      showDelete={true}
                      onDelete={() => {
                        setContentFiles((prev) => prev.filter((f) => f !== file));
                      }}
                    />
                  ))}
                </div>
              )}
              {/* Textarea容器 - 相对定位 */}
              <div className="relative w-full">
                <Textarea
                  ref={textareaRef}
                  className={cn(
                    'm-0 w-full resize-none border-none outline-none rounded-md bg-transparent leading-6',
                    `transition-[height] ease-out`,
                    isFullWriting && 'overflow-auto'
                  )}
                  style={{
                    height: textareaHeight === 'full' ? 'calc(100vh - 108px)' : `${textareaHeight}px`,
                    transitionDuration: `${ANIMATION_DURATION_MS}ms`
                  }}
                  placeholder={
                    t('Type a message or type "/" to select a prompt...') || ''
                  }
                  value={contentText}
                  rows={TEXTAREA_MIN_ROWS}
                  onCompositionStart={() => setIsTyping(true)}
                  onCompositionEnd={() => setIsTyping(false)}
                  onChange={handleChange}
                  onKeyDown={handleKeyDown}
                />

                {/* 发送按钮 - 绝对定位在右下角，允许遮挡 */}
                <div className="absolute right-2 bottom-2 flex items-center gap-2 pointer-events-auto">
                  {selectedChat.status === ChatStatus.Chatting ? (
                    <Tips
                      trigger={
                        <Button
                          className="rounded-sm w-20 h-9 shadow-md"
                          onClick={handleStopChats}
                        >
                          <IconStopFilled className="h-4 w-4" />
                        </Button>
                      }
                      side="top"
                      content={t('Stop Generating')}
                    />
                  ) : (
                    <SendButton
                      onSend={handleSend}
                      disabled={!contentText?.trim()}
                      size="sm"
                    />
                  )}
                </div>

                {/* 全屏模式下的文件展示 */}
                {isFullWriting && contentFiles.length > 0 && (
                  <div className="flex flex-row px-3 pb-2 gap-2">
                    {contentFiles.map((file, index) => (
                      <FilePreview
                        key={index}
                        file={file}
                        maxWidth={80}
                        maxHeight={80}
                        showDelete={true}
                        onDelete={() => {
                          setContentFiles((prev) => prev.filter((f) => f !== file));
                        }}
                      />
                    ))}
                  </div>
                )}
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
      )}

      {/* 收起状态的浮动按钮组 */}
      {(!renderExpanded || animationState !== 'idle') && (
        <div
          className="absolute bottom-0 right-2 md:right-4 flex gap-1 md:gap-2 items-center pointer-events-auto transition-transform ease-out"
          style={{
            transform: buttonsTransform,
            transitionDuration: `${ANIMATION_DURATION_MS}ms`,
          }}
        >
          {/* 滚动到顶部按钮 */}
          {showScrollToTopButton && (
            <Tips
              trigger={
                <Button
                  size="xs"
                  className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                  size="xs"
                  className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                  size="xs"
                  className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                size="xs"
                className="p-1 m-0.5 sm:m-1 text-neutral-800 bg-transparent hover:bg-muted"
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
                  size="xs"
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
      )}
    </div>
  );
};
export default ChatInput;
