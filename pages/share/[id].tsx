import { useEffect, useState } from 'react';

import { serverSideTranslations } from 'next-i18next/serverSideTranslations';
import Link from 'next/link';
import { useRouter } from 'next/router';

import { getSelectMessages } from '@/utils/message';

import { GetMessageDetailsResult } from '@/types/admin';
import { ChatMessage } from '@/types/chatMessage';
import { DEFAULT_LANGUAGE } from '@/types/settings';

import { ChatMessage as ChatMessageComponent } from '@/components/Admin/Messages/ChatMessage';
import { Button } from '@/components/ui/button';

import { getShareMessage } from '@/apis/adminService';

export default function ShareMessage() {
  const router = useRouter();
  const { id } = router.query as { id: string };
  const [chat, setChat] = useState<GetMessageDetailsResult | null>(null);
  const [selectMessages, setSelectMessages] = useState<ChatMessage[]>([]);
  const [currentMessages, setCurrentMessages] = useState<ChatMessage[]>([]);

  useEffect(() => {
    getShareMessage(id!).then((data) => {
      document.title = data.name;
      if (data.messages.length > 0) {
        setChat(data);
        setCurrentMessages(data.messages);
        const lastMessage = data.messages[data.messages.length - 1];
        const _selectMessages = getSelectMessages(
          data.messages,
          lastMessage.id,
        );
        setSelectMessages(_selectMessages);
      }
    });
  }, []);

  const onMessageChange = (messageId: string) => {
    const _selectMessages = getSelectMessages(currentMessages, messageId);
    setSelectMessages(_selectMessages);
  };

  return (
    <>
      <div className="w-full">
        {chat &&
          selectMessages.map((current, index) => {
            let parentChildrenIds: string[] = [];
            if (!current.parentId) {
              parentChildrenIds = currentMessages
                .filter((x) => !x.parentId)
                .map((x) => x.id);
            } else {
              parentChildrenIds =
                currentMessages.find((x) => x.id === current.parentId)
                  ?.childrenIds || [];
              parentChildrenIds = [...parentChildrenIds].reverse();
            }
            return (
              <ChatMessageComponent
                currentSelectIndex={parentChildrenIds.findIndex(
                  (x) => x === current.id,
                )}
                isLastMessage={selectMessages.length - 1 === index}
                id={current.id!}
                key={current.id + index}
                parentId={current.parentId}
                onChangeMessage={(messageId: string) => {
                  onMessageChange(messageId);
                }}
                childrenIds={current.childrenIds}
                parentChildrenIds={parentChildrenIds}
                assistantChildrenIds={current.assistantChildrenIds}
                assistantCurrentSelectIndex={current.assistantChildrenIds.findIndex(
                  (x) => x === current.id,
                )}
                modelName={current.modelName}
                message={{ role: current.role, content: current.content }}
              />
            );
          })}
      </div>
      <div className="h-32"></div>
      <div className="fixed bottom-0 py-4 w-full bg-white dark:bg-black dark:text-white">
        <div className="flex justify-center pb-2">
          <Link href="/">
            <Button className="h-8 w-32">使用 Chats</Button>
          </Link>
        </div>
        <div className="flex justify-center text-gray-500 text-sm">
          内容有AI大模型生成，请仔细甄别
        </div>
      </div>
    </>
  );
}

export const getServerSideProps = async ({ locale }: { locale: string }) => {
  return {
    props: {
      ...(await serverSideTranslations(locale ?? DEFAULT_LANGUAGE, [
        'common',
        'markdown',
      ])),
    },
  };
};
