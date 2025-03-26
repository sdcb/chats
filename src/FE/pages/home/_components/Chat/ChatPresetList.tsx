import { useContext, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { GetChatPresetResult } from '@/types/clientApis';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import { IconEdit, IconPlus } from '@/components/Icons';

import HomeContext from '../../_contexts/home.context';
import ChatPresetModal from './ChatPresetModal';

import { getChatPreset, postChatPreset, postChats } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

const ChatPresetList = () => {
  const { hasModel } = useContext(HomeContext);
  const [chatPresets, setChatPresets] = useState<GetChatPresetResult[]>([]);
  const [chatPreset, setChatPreset] = useState<GetChatPresetResult>();
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    getChatPreset().then((data) => {
      setChatPresets(data);
    });
  }, []);

  const handleCreateChatPreset = () => {
    postChatPreset('test').then((item) => {
      setChatPresets((prev) => {
        return [...prev, item];
      });
    });
  };

  return (
    <div className={cn('mx-auto px-0 md:px-8 pt-6 pb-32')}>
      {hasModel() && (
        <div className="grid grid-cols-[repeat(auto-fit,minmax(144px,320px))] gap-4">
          {chatPresets?.map((item) => {
            return (
              <div
                key={'chat-preset' + item.id}
                className="rounded-sm p-4 border h-24"
              >
                <div className="flex justify-between">
                  <span>{item.name}</span>
                  <span>
                    <IconEdit
                      onClick={() => {
                        setChatPreset(item);
                        setIsOpen(true);
                      }}
                    />
                  </span>
                </div>
                <div>
                  {item.spans.map((s) => (
                    <ChatIcon
                      key={'chat-icon-' + s.spanId}
                      providerId={s.modelProviderId}
                    />
                  ))}
                </div>
              </div>
            );
          })}
          <div
            className="rounded-sm px-4 border flex justify-center items-center h-36"
            onClick={handleCreateChatPreset}
          >
            <IconPlus size={32} />
          </div>
        </div>
      )}
      <ChatPresetModal
        chatPreset={chatPreset}
        isOpen={isOpen}
        onClose={() => {
          setIsOpen(false);
        }}
      />
    </div>
  );
};

export default ChatPresetList;
