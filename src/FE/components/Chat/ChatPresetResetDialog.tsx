import { useContext, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { GetChatPresetResult } from '@/types/clientApis';

import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { setChats } from '@/actions/chat.actions';
import HomeContext from '@/contexts/home.context';

import { getChatPreset, postApplyChatPreset } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface ChatPresetResetDialogProps {
  isOpen: boolean;
  onClose: () => void;
}

const ChatPresetResetDialog = ({
  isOpen,
  onClose,
}: ChatPresetResetDialogProps) => {
  const { t } = useTranslation();
  const {
    chatDispatch,
    state: { chats, modelMap },
    selectedChat,
  } = useContext(HomeContext);
  const [chatPresets, setChatPresets] = useState<GetChatPresetResult[]>([]);

  useEffect(() => {
    if (isOpen) {
      getChatPreset().then((data) => {
        setChatPresets(data);
      });
    }
  }, [isOpen]);

  const handleSelectChatPreset = async (item: GetChatPresetResult) => {
    if (!selectedChat || item.spans.length === 0) return;
    
    try {
      await postApplyChatPreset(selectedChat.id, item.id);
      const updatedChats = chats.map((c) => {
        if (c.id === selectedChat.id) {
          return { ...c, spans: item.spans };
        }
        return c;
      });
      chatDispatch(setChats(updatedChats));
      onClose();
    } catch (error) {
      console.error('Failed to apply chat preset:', error);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('Reset Models')}</DialogTitle>
        </DialogHeader>
        <div className="grid grid-cols-[repeat(auto-fit,minmax(120px,240px))] place-content-center gap-3 p-4">
          {chatPresets?.map((item) => (
            <div
              key={'chat-preset-reset-' + item.id}
              className={cn(
                'rounded-sm p-3 h-20 hover:bg-muted cursor-pointer shadow-sm bg-card border',
              )}
              onClick={() => handleSelectChatPreset(item)}
            >
              <div className="flex justify-between items-start mb-2">
                <span className="text-sm text-ellipsis whitespace-nowrap overflow-hidden">
                  {item.name}
                </span>
              </div>
              <div className="flex justify-end items-end h-8">
                {item.spans.map((s) => (
                  <TooltipProvider
                    delayDuration={100}
                    key={'span-tooltip-reset-' + s.spanId}
                  >
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Button className="bg-transparent p-0 m-0 h-auto hover:bg-transparent">
                          <ModelProviderIcon
                            className={cn(
                              'cursor-pointer border border-1 border-muted-foreground bg-white w-6 h-6',
                              !modelMap[s.modelId] && 'grayscale',
                            )}
                            providerId={s.modelProviderId}
                          />
                        </Button>
                      </TooltipTrigger>
                      <TooltipContent>{s.modelName}</TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                ))}
              </div>
            </div>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
};

export default ChatPresetResetDialog;
