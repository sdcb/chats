import { useContext, useEffect, useState } from 'react';

import { useCreateReducer } from '@/hooks/useCreateReducer';
import useTranslation from '@/hooks/useTranslation';

import { getNextName } from '@/utils/common';

import { CHATS_SELECT_TYPE, ChatStatus, DefaultChatPaging } from '@/types/chat';
import { ChatResult } from '@/types/clientApis';

import {
  setChatGroup,
  setChatPaging,
  setChats,
  setChatsSelectType,
} from '@/actions/chat.actions';
import { setShowChatBar } from '@/actions/setting.actions';
import HomeContext from '@/contexts/home.context';
import Sidebar from '../Sidebar/Sidebar';
import ChatActionConfirm from './ChatActionConfirm';
import ChatActions from './ChatActions';
import ChatGroups from './ChatGroups';
import ChatbarContext from './Chatbar.context';
import { ChatbarInitialState, initialState } from './Chatbar.context';
import ChatBarSettings from './ChatbarSettings';

import { deleteChats, postChatGroup, putChats } from '@/apis/clientApis';

const Chatbar = () => {
  const { t } = useTranslation();

  const chatBarContextValue = useCreateReducer<ChatbarInitialState>({
    initialState,
  });

  const {
    state: {
      chats,
      chatGroups,
      chatPaging,
      showChatBar,
      isChatsLoading,
      chatsSelectType,
    },
    selectedChat,
    chatDispatch,
    settingDispatch,
    handleDeleteChat,
    handleNewChat,
    hasModel,
    getChats,
    getChatsByGroup,
  } = useContext(HomeContext);

  const {
    state: { filteredChats },
    dispatch,
  } = chatBarContextValue;
  const [searchTerm, setSearchTerm] = useState('');
  const [actionConfirming, setActionConfirming] = useState(false);

  const handleToggleChatbar = () => {
    settingDispatch(setShowChatBar(!showChatBar));
  };

  const handleAddGroup = () => {
    const groupNames = chatGroups.map((x) => x.name);
    const name = getNextName(groupNames, t('New Group'));
    postChatGroup({ rank: 0, name, isExpanded: false }).then((data) => {
      chatDispatch(setChatGroup([data, ...chatGroups]));
      chatDispatch(
        setChatPaging([
          ...chatPaging,
          { ...DefaultChatPaging, count: 0, groupId: data.id },
        ]),
      );
    });
  };

  const handleChangeChatsSelectType = (
    type: CHATS_SELECT_TYPE = CHATS_SELECT_TYPE.NONE,
  ) => {
    chatDispatch(setChatsSelectType(type));
  };

  const handleActionCancel = () => {
    const chatList = chats.map((x) => ({ ...x, selected: false }));
    chatDispatch(setChats(chatList));
    handleChangeChatsSelectType(CHATS_SELECT_TYPE.NONE);
  };

  const handleActionConfirm = async () => {
    const selectedChatIds = chats.filter((c) => c.selected).map((c) => c.id);
    setActionConfirming(true);
    if (chatsSelectType === CHATS_SELECT_TYPE.DELETE) {
      for (const id of selectedChatIds) {
        await deleteChats(id);
      }
      handleDeleteChat(selectedChatIds);
      chatDispatch(setChatsSelectType(CHATS_SELECT_TYPE.NONE));
    } else if (chatsSelectType === CHATS_SELECT_TYPE.ARCHIVE) {
      for (const id of selectedChatIds) {
        await putChats(id, { isArchived: true });
      }
      handleDeleteChat(selectedChatIds);
      chatDispatch(setChatsSelectType(CHATS_SELECT_TYPE.NONE));
    }
    setActionConfirming(false);
  };

  const handleShowMore = (groupId: string | null) => {
    const { page, pageSize } = chatPaging.find((x) => x.groupId === groupId)!;
    getChatsByGroup({
      groupId,
      page: page + 1,
      pageSize: pageSize,
      query: searchTerm,
    });
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
        folderComponent={<ChatGroups onShowMore={handleShowMore} />}
        actionComponent={
          <ChatActions
            onAddGroup={handleAddGroup}
            onBatchArchive={() => {
              handleChangeChatsSelectType(CHATS_SELECT_TYPE.ARCHIVE);
            }}
            onBatchDelete={() => {
              handleChangeChatsSelectType(CHATS_SELECT_TYPE.DELETE);
            }}
          />
        }
        actionConfirmComponent={
          chatsSelectType !== CHATS_SELECT_TYPE.NONE && (
            <ChatActionConfirm
              confirming={actionConfirming}
              selectedCount={chats.filter((c) => c.selected).length}
              onCancel={handleActionCancel}
              onConfirm={handleActionConfirm}
            />
          )
        }
        items={filteredChats}
        searchTerm={searchTerm}
        handleSearchTerm={(value: string) => {
          setSearchTerm(value);
          getChats(value);
        }}
        toggleOpen={handleToggleChatbar}
        handleCreateItem={handleNewChat}
        footerComponent={<ChatBarSettings />}
      />
    </ChatbarContext.Provider>
  );
};
export default Chatbar;
