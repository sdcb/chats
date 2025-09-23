import { FC, memo } from 'react';

import { ChatMessage, Props } from '@/components/ChatMessage';

// const ChatMessageMemoized: FC<Props> = memo(
//   ChatMessage,
//   (prevProps, nextProps) =>
//     prevProps.selectedMessages.length === nextProps.selectedMessages.length &&
//     prevProps.selectedChat.status === nextProps.selectedChat.status,
// );

export default ChatMessage;
