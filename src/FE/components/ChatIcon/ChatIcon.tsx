import useTranslation from '@/hooks/useTranslation';

import { DBModelProvider, feModelProviders } from '@/types/model';

import { cn } from '@/lib/utils';

interface Props {
  providerId?: DBModelProvider;
  providerName?: string; // 新增: 如果没有 providerId，通过名称匹配
  className?: string;
}

const ChatIcon = (props: Props) => {
  const { providerId, providerName, className } = props;
  const { t } = useTranslation();

  let finalProvider = undefined as typeof feModelProviders[number] | undefined;

  if (providerId !== undefined) {
    finalProvider = feModelProviders[providerId];
  } else if (providerName) {
    finalProvider = feModelProviders.find(p => p.name === providerName);
  }

  // 回退: 如果未找到，使用 Test 以避免空白闪烁，但不渲染 alt 中误导文本
  if (!finalProvider) {
    finalProvider = feModelProviders[DBModelProvider.Test];
  }

  return (
    <img
      key={`img-${finalProvider.id}`}
      src={finalProvider.icon}
      alt={t(finalProvider.name)}
      style={{ background: 'transparent' }}
      className={cn('h-5 w-5 rounded-md', className)}
    />
  );
};
export default ChatIcon;
