import { ChatSpanStatus, IChat } from '@/types/chat';

export function preprocessLaTeX(content?: string) {
  if (!content) {
    return '';
  }
  // Replace block-level LaTeX delimiters \[ \] with $$ $$

  const blockProcessedContent = content.replace(
    /\\\[(.*?)\\\]/gs,
    (_, equation) => `$$${equation}$$`,
  );
  // Replace inline LaTeX delimiters \( \) with $ $
  const inlineProcessedContent = blockProcessedContent.replace(
    /\\\((.*?)\\\)/gs,
    (_, equation) => `$${equation}$`,
  );
  return inlineProcessedContent;
}

export const chatsGroupByUpdatedAt = (data: IChat[]): Map<string, IChat[]> => {
  const groupedData = new Map<string, IChat[]>();
  const now = new Date();

  const sortedData = data.sort((a, b) => {
    const dateA = new Date(a.updatedAt).getTime();
    const dateB = new Date(b.updatedAt).getTime();
    return dateB - dateA;
  });

  const otherChats: IChat[] = [],
    topMostChats: IChat[] = [];
  sortedData.forEach((x) => {
    x.isTopMost ? topMostChats.push(x) : otherChats.push(x);
  });
  topMostChats.length > 0 && groupedData.set('Pinned', topMostChats);

  const isSameDay = (date1: Date, date2: Date): boolean => {
    return (
      date1.getFullYear() === date2.getFullYear() &&
      date1.getMonth() === date2.getMonth() &&
      date1.getDate() === date2.getDate()
    );
  };

  const isWithinDays = (date: Date, days: number): boolean => {
    const pastDate = new Date(now);
    pastDate.setDate(now.getDate() - days);
    return date >= pastDate && date <= now;
  };

  otherChats.forEach((item) => {
    const date = new Date(item.updatedAt);

    let groupKey: string;

    if (isSameDay(date, now)) {
      groupKey = 'Today';
    } else if (
      isSameDay(
        date,
        new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1),
      )
    ) {
      groupKey = 'Yesterday';
    } else if (isWithinDays(date, 7)) {
      groupKey = 'Last 7 days';
    } else if (isWithinDays(date, 30)) {
      groupKey = 'Last 30 days';
    } else {
      groupKey = `${date.getFullYear()}`;
    }

    if (!groupedData.has(groupKey)) {
      groupedData.set(groupKey, []);
    }

    groupedData.get(groupKey)!.push(item);
  });

  return groupedData;
};

export const isUnGroupChat = (id: string | null) => {
  return id === null;
};

export const isChatting = (status: ChatSpanStatus) => {
  return (
    status === ChatSpanStatus.Chatting ||
    status === ChatSpanStatus.Reasoning ||
    status === ChatSpanStatus.Pending
  );
};
