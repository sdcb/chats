import { useContext } from 'react';

import useTranslation from '@/hooks/useTranslation';

import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import { IconPlus, IconX } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { setSelectedChat } from '../../_actions/chat.actions';
import HomeContext from '../../_contexts/home.context';
import ModeToggle from '../ModeToggle/ModeToggle';

import {
  deleteUserChatSpan,
  postUserChatSpan,
  putUserChatSpan,
} from '@/apis/clientApis';
import { cn } from '@/lib/utils';

const ChatHeader = () => {
  const { t } = useTranslation();
  const MAX_SELECT_MODEL_COUNT = 10;
  const {
    state: {
      selectedChat,
      models,

      defaultPrompt,
      showChatBar,
    },
    chatDispatch,
  } = useContext(HomeContext);

  const handleAddChatModel = async (modelId: number) => {
    await postUserChatSpan(selectedChat.id, { modelId }).then((data) => {
      selectedChat.spans.push({
        spanId: data.spanId,
        modelId: data.modelId,
        modelName: data.modelName,
        modelProviderId: data.modelProviderId,
        temperature: data.temperature,
        enableSearch: data.enableSearch,
        reasoningEffort: data?.reasoningEffort,
        prompt: defaultPrompt?.content!,
      });
      chatDispatch(setSelectedChat(selectedChat));
    });
  };

  const handleRemoveChatModel = async (spanId: number) => {
    await deleteUserChatSpan(selectedChat.id, spanId).then(() => {
      selectedChat.spans = selectedChat.spans.filter(
        (s) => s.spanId !== spanId,
      );
      chatDispatch(setSelectedChat(selectedChat));
    });
  };

  const handleUpdateChatModel = async (spanId: number, modelId: number) => {
    await putUserChatSpan(selectedChat.id, spanId, { modelId }).then((data) => {
      selectedChat.spans = selectedChat.spans.map((s) => {
        if (s.spanId === spanId) {
          return {
            ...s,
            modelId: data.modelId,
            modelName: data.modelName,
            modelProviderId: data.modelProviderId,
            temperature: data.temperature,
            enableSearch: data.enableSearch,
          };
        }
        return s;
      });
      chatDispatch(setSelectedChat(selectedChat));
    });
  };

  const AddBtnRender = () => (
    <div className="flex items-center rounded-md mx-1">
      {selectedChat.spans.length < MAX_SELECT_MODEL_COUNT && (
        <ChatModelDropdownMenu
          models={models}
          className="text-sm"
          content={
            <Tips
              trigger={
                <Button
                  variant="ghost"
                  className="p-1 m-0 h-auto hover:bg-transparent"
                >
                  <IconPlus />
                </Button>
              }
              content={t('Add Model')}
            />
          }
          hideIcon={true}
          onChangeModel={(model) => {
            handleAddChatModel(model.modelId);
          }}
        />
      )}
    </div>
  );

  return (
    <div className="sticky top-0 z-10 text-sm bg-background right-0">
      <div className="flex justify-between items-center w-full">
        <div
          className={cn(
            'flex justify-start ml-24 h-12 items-center overflow-auto',
            showChatBar && 'ml-6',
          )}
        >
          <div className="flex gap-2 flex-wrap h-10 items-center overflow-auto">
            {selectedChat.spans.map((span) => (
              <div
                className="flex bg-muted rounded-md"
                key={'chat-header-' + span.spanId}
              >
                <ChatModelDropdownMenu
                  key={'change-model-' + span.modelId}
                  models={models}
                  modelName={span.modelName}
                  className="text-sm"
                  content={span?.modelName}
                  hideIcon={true}
                  onChangeModel={(model) => {
                    handleUpdateChatModel(span.spanId, model.modelId);
                  }}
                />

                <div hidden={selectedChat.spans.length === 1}>
                  <Tips
                    trigger={
                      <Button
                        onClick={() => {
                          handleRemoveChatModel(span.spanId);
                        }}
                        variant="ghost"
                        className="py-1 px-0 pr-1 m-0 h-auto hover:bg-transparent"
                      >
                        <IconX />
                      </Button>
                    }
                    content={t('Remove')}
                  />
                </div>
              </div>
            ))}
          </div>
          <AddBtnRender />
        </div>
        <div className="ml-2 mr-2 md:mr-4">{<ModeToggle />}</div>
      </div>
    </div>
  );
};

export default ChatHeader;
