import { DragEvent, useContext, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isUnGroupChat } from '@/utils/chats';

import { IChat } from '@/types/chat';

import { Button } from '@/components/ui/button';

import HomeContext from '@/contexts/home.context';
import ChatListItem from './ChatListItem';

import { cn } from '@/lib/utils';

interface Props {
  groupId: string | null;
  chatGroups: Map<string, IChat[]>;
  onShowMore?: (groupId: string | null) => void;
  onDragItemStart?: (e: DragEvent<HTMLElement>, chat: IChat) => void;
}

const ChatList = ({
  groupId,
  chatGroups,
  onShowMore,
  onDragItemStart,
}: Props) => {
  const { t } = useTranslation();
  const {
    state: { chatPaging },
  } = useContext(HomeContext);

  const [showMore, setShowMore] = useState(true);

  useEffect(() => {
    const { count, page, pageSize } = chatPaging.find(
      (x) => x.groupId === groupId,
    )!;
    setShowMore(count <= page * pageSize);
  }, [chatPaging]);

  const handleShowMore = () => {
    onShowMore && onShowMore(groupId);
  };

  return (
    <div className="flex w-full flex-col gap-1">
      {chatGroups.size > 0 &&
        [...chatGroups.entries()].map(([group, items]) => (
          <div key={group}>
            <div className="w-full pl-2.5 text-xs text-gray-500 font-medium my-1">
              {t(group)}
            </div>
            {items.map((chat, index) => (
              <div
                className={cn(!isUnGroupChat(groupId) && 'ml-1')}
                key={'conversation-' + index}
              >
                <ChatListItem
                  onDragItemStart={onDragItemStart}
                  chat={chat}
                />
              </div>
            ))}
          </div>
        ))}
      {!showMore && (
        <Button onClick={handleShowMore} className="text-xs" variant="link">
          {t('Show more')}
        </Button>
      )}
    </div>
  );
};
export default ChatList;
