import { useContext, useEffect, useState } from 'react';

import { useCreateReducer } from '@/hooks/useCreateReducer';
import useTranslation from '@/hooks/useTranslation';

import { ChatStatus } from '@/types/chat';
import { ChatResult } from '@/types/clientApis';

import { setShowChatBar } from '../../_actions/setting.actions';
import HomeContext from '../../_contexts/home.context';
import Sidebar from '../Sidebar/Sidebar';
import { ChatFolders } from './ChatFolders';
import ChatbarContext from './Chatbar.context';
import { ChatbarInitialState, initialState } from './Chatbar.context';
import ChatBarSettings from './ChatbarSettings';
import Conversations from './Conversations';

import { postChatGroup } from '@/apis/clientApis';

const Chatbar = () => {
  const { t } = useTranslation();

  const chatBarContextValue = useCreateReducer<ChatbarInitialState>({
    initialState,
  });

  const {
    state: { chats, showChatBar, selectedChat, isChatsLoading },
    settingDispatch,
    handleDeleteChat,
    handleNewChat,
    hasModel,
    getChats,
  } = useContext(HomeContext);

  const {
    state: { filteredChats },
    dispatch,
  } = chatBarContextValue;
  const [searchTerm, setSearchTerm] = useState('');

  const handleToggleChatbar = () => {
    settingDispatch(setShowChatBar(!showChatBar));
  };

  const handleAddFolder = () => {
    postChatGroup({ rank: 0, name: t('New Folder'), isExpanded: true }).then(
      () => {},
    );
  };

  useEffect(() => {
    if (searchTerm) {
      dispatch({
        field: 'filteredChats',
        value: chats.filter((chat) => {
          const searchable = chat.title.toLocaleLowerCase();
          return searchable.toLowerCase().includes(searchTerm.toLowerCase());
        }),
      });
    } else {
      dispatch({
        field: 'filteredChats',
        value: chats,
      });
    }
  }, [searchTerm, chats]);

  return (
    <ChatbarContext.Provider
      value={{
        ...chatBarContextValue,
        handleDeleteChat,
      }}
    >
      <Sidebar<ChatResult>
        isLoading={isChatsLoading}
        messageIsStreaming={selectedChat?.status === ChatStatus.Chatting}
        side={'left'}
        isOpen={showChatBar}
        addItemButtonTitle={t('New chat')}
        hasModel={hasModel}
        onAddFolder={handleAddFolder}
        // itemComponent={<Conversations chats={filteredChats} />}
        folderComponent={<ChatFolders searchTerm="" />}
        items={filteredChats}
        searchTerm={searchTerm}
        handleSearchTerm={(value: string) => {
          setSearchTerm(value);
          getChats({ query: value, page: 1, pageSize: 50 });
        }}
        toggleOpen={handleToggleChatbar}
        handleCreateItem={handleNewChat}
        onDrop={() => {}}
        footerComponent={<ChatBarSettings />}
      />
    </ChatbarContext.Provider>
  );
};
export default Chatbar;
