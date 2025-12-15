import {
  ChatRole,
  ChatSpanStatus,
  IChat,
  MessageContentType,
  ResponseContent,
} from '@/types/chat';
import {
  ChatMessageNode,
  IChatMessage,
  ResponseMessageTempId,
  UserMessageTempId,
  getMessageContents,
} from '@/types/chatMessage';

export function findLastLeafId(
  messages: ChatMessageNode[],
  id: string,
): string {
  const currentMessage = messages.find((message) => message.id === id);
  if (!currentMessage) {
    return id;
  }
  const childMap: Record<string, IChatMessage[]> = {};
  messages.forEach((node) => {
    if (node.parentId !== null) {
      if (!childMap[node.parentId]) {
        childMap[node.parentId] = [];
      }
      childMap[node.parentId].push(node);
    }
  });

  function findDeepest(
    message: IChatMessage,
    depth: number,
  ): { id: string; depth: number } {
    if (!childMap[message.id] || childMap[message.id].length === 0) {
      return { id: message.id, depth };
    }

    let deepest = { id: message.id, depth };
    for (const child of childMap[message.id]) {
      const childDeepest = findDeepest(child, depth + 1);
      if (childDeepest.depth > deepest.depth) {
        deepest = childDeepest;
      }
    }

    return deepest;
  }

  const deepestNode = findDeepest(currentMessage, 0);
  return deepestNode.id;
}

export function findSelectedMessageByLeafId(
  messages: ChatMessageNode[],
  leafId: string,
): ChatMessageNode[][] {
  const messageMap = new Map<string, ChatMessageNode>();
  messages.forEach((m) => messageMap.set(m.id, m));

  const path: ChatMessageNode[][] = [];
  let currentMessage = messageMap.get(leafId);

  if (!currentMessage) return path;

  while (currentMessage) {
    const parentId: string | null = currentMessage.parentId;
    let prevUserMessage: ChatMessageNode | null = null;

    if (currentMessage.role === ChatRole.User) {
      const siblingIds = messages
        .filter((m) => m.parentId === parentId && m.role === ChatRole.User)
        .map((x) => x.id);

      const currentOutputMessage: ChatMessageNode = {
        ...currentMessage,
        siblingIds,
      };
      prevUserMessage = currentOutputMessage;
      path.unshift([currentOutputMessage]);
    } else if (currentMessage.role === ChatRole.Assistant) {
      const assistantSiblings = messages.filter(
        (m) => m.parentId === parentId && m.role === ChatRole.Assistant,
      );
      const groupedSiblings = groupBy(assistantSiblings, 'spanId');

      const group: ChatMessageNode[] = [];
      groupedSiblings.forEach((siblingGroup) => {
        const siblingIds = siblingGroup.map((x) => x.id);
        let selectedMessage: ChatMessageNode | null = null;

        siblingGroup.forEach((x) => {
          const messageIsError = !!getMessageContents(x).find(
            (c) => c.$type === MessageContentType.error,
          );
          if (x.id === currentMessage!.id) {
            selectedMessage = {
              ...x,
              siblingIds,
              isActive: true,
              status: messageIsError
                ? ChatSpanStatus.Failed
                : ChatSpanStatus.None,
            };
          } else if (prevUserMessage && prevUserMessage.parentId === x.id) {
            selectedMessage = {
              ...x,
              siblingIds,
              isActive: true,
              status: messageIsError
                ? ChatSpanStatus.Failed
                : ChatSpanStatus.None,
            };
          }
        });

        if (!selectedMessage) {
          const lastMessage = siblingGroup[siblingGroup.length - 1];
          const messageIsError = !!getMessageContents(lastMessage).find(
            (c) => c.$type === MessageContentType.error,
          );
          selectedMessage = {
            ...lastMessage,
            siblingIds,
            isActive: false,
            status: messageIsError
              ? ChatSpanStatus.Failed
              : ChatSpanStatus.None,
          };
        }

        group.push(selectedMessage);
      });

      path.unshift(group);
    }

    currentMessage = parentId ? messageMap.get(parentId) : undefined;
  }

  return path;
}

function groupBy<T>(array: T[], key: keyof T): T[][] {
  const groups: { [key: string]: T[] } = {};
  for (const item of array) {
    const groupKey = String(item[key]);
    groups[groupKey] = groups[groupKey] || [];
    groups[groupKey].push(item);
  }
  return Object.values(groups);
}

export function generateResponseMessages(
  selectedChat: IChat,
  parentId?: string,
  status = ChatSpanStatus.Pending,
) {
  return selectedChat.spans
    .filter((x) => x.enabled)
    .map((x) => {
      return generateResponseMessage(
        x.spanId,
        parentId,
        x.modelId,
        x.modelName,
        status,
      );
    });
}

export function generateResponseMessage(
  spanId: number,
  parentId?: string,
  modelId?: number,
  modelName?: string,
  status = ChatSpanStatus.Pending,
) {
  return {
    spanId: spanId,
    id: `${ResponseMessageTempId}-${spanId}`,
    role: ChatRole.Assistant,
    parentId: parentId,
    status,
    siblingIds: [],
    isActive: false,
    steps: [{ id: '', contents: [], edited: false, createdAt: new Date().toISOString() }],
    modelName: modelName,
    modelId: modelId,
  } as IChatMessage;
}

export function generateUserMessage(
  content: ResponseContent[],
  parentId?: string,
) {
  return {
    spanId: null,
    modelId: 0,
    id: UserMessageTempId,
    role: ChatRole.User,
    status: ChatSpanStatus.None,
    parentId,
    siblingIds: [],
    isActive: false,
    steps: [{ id: '', contents: content, edited: false, createdAt: new Date().toISOString() }],
  } as IChatMessage;
}
