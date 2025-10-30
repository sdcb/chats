import { Skeleton } from '@/components/ui/skeleton';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { IChat } from '@/types/chat';
import { cn } from '@/lib/utils';

interface Props {
  selectedChat: IChat;
}

const ChatMessagesSkeleton = ({ selectedChat }: Props) => {
  const spans = selectedChat?.spans || [];
  const isMultiSpan = spans.length > 1;

  return (
    <div className={cn(
      'w-full m-auto p-2 md:p-4',
      !isMultiSpan && 'w-full lg:w-11/12',
    )}>
      {/* 用户消息骨骼屏 */}
      <div className="flex w-full justify-end mb-4">
        <div className="prose w-full dark:prose-invert rounded-r-md sm:w-[50vw] xl:w-[50vw]">
          <div className="bg-card py-2 px-3 rounded-md">
            <div className="space-y-2">
              <Skeleton className="h-4 w-3/4 ml-auto" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-2/3 ml-auto" />
            </div>
          </div>
          <div className="flex justify-end my-1 gap-1">
            <Skeleton className="h-6 w-6 rounded" />
            <Skeleton className="h-6 w-6 rounded" />
          </div>
        </div>
      </div>

      {/* 助手消息骨骼屏 - 根据 spans 数量显示 */}
      <div className={cn(
        'mb-4',
        isMultiSpan && 'md:grid md:grid-cols-[repeat(auto-fit,minmax(375px,1fr))] gap-4'
      )}>
        {spans.map((span, index) => (
          <div key={`assistant-skeleton-1-${span.spanId}`} className="group/item">
            {/* Header - 显示真实的图标和模型名称 */}
            <div className="flex justify-between items-center h-8 mb-1">
              <div className="flex gap-1 items-center">
                <ModelProviderIcon
                  providerId={span.modelProviderId}
                  className="w-4 h-4 hidden sm:block"
                />
                <span className="text-sm">{span.modelName}</span>
              </div>
            </div>
            {/* Message content */}
            <div className={cn(
              'border-[1px] border-background rounded-md flex w-full bg-card mb-1',
              isMultiSpan && 'p-1 md:p-2',
              !isMultiSpan && 'border-none'
            )}>
              <div className="prose dark:prose-invert rounded-r-md flex-1 overflow-auto text-base py-1 px-2">
                <div className="space-y-3 py-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-4/5" />
                  <Skeleton className="h-4 w-3/5" />
                </div>
              </div>
            </div>
            {/* Actions */}
            <div className="flex gap-1 my-1">
              <Skeleton className="h-6 w-6 rounded" />
              <Skeleton className="h-6 w-6 rounded" />
              <Skeleton className="h-6 w-6 rounded" />
            </div>
          </div>
        ))}
      </div>

      {/* 用户消息骨骼屏 */}
      <div className="flex w-full justify-end mb-4">
        <div className="prose w-full dark:prose-invert rounded-r-md sm:w-[50vw] xl:w-[50vw]">
          <div className="bg-card py-2 px-3 rounded-md">
            <div className="space-y-2">
              <Skeleton className="h-4 w-4/5 ml-auto" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-1/2 ml-auto" />
            </div>
          </div>
          <div className="flex justify-end my-1 gap-1">
            <Skeleton className="h-6 w-6 rounded" />
            <Skeleton className="h-6 w-6 rounded" />
          </div>
        </div>
      </div>

      {/* 助手消息骨骼屏 - 根据 spans 数量显示 */}
      <div className={cn(
        'mb-4',
        isMultiSpan && 'md:grid md:grid-cols-[repeat(auto-fit,minmax(375px,1fr))] gap-4'
      )}>
        {spans.map((span, index) => (
          <div key={`assistant-skeleton-2-${span.spanId}`} className="group/item">
            {/* Header - 显示真实的图标和模型名称 */}
            <div className="flex justify-between items-center h-8 mb-1">
              <div className="flex gap-1 items-center">
                <ModelProviderIcon
                  providerId={span.modelProviderId}
                  className="w-4 h-4 hidden sm:block"
                />
                <span className="text-sm">{span.modelName}</span>
              </div>
            </div>
            {/* Message content */}
            <div className={cn(
              'border-[1px] border-background rounded-md flex w-full bg-card mb-1',
              isMultiSpan && 'p-1 md:p-2',
              !isMultiSpan && 'border-none'
            )}>
              <div className="prose dark:prose-invert rounded-r-md flex-1 overflow-auto text-base py-1 px-2">
                <div className="space-y-3 py-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-3/4" />
                  <Skeleton className="h-4 w-2/3" />
                </div>
              </div>
            </div>
            {/* Actions */}
            <div className="flex gap-1 my-1">
              <Skeleton className="h-6 w-6 rounded" />
              <Skeleton className="h-6 w-6 rounded" />
              <Skeleton className="h-6 w-6 rounded" />
            </div>
          </div>
        ))}
      </div>

      {/* 用户消息骨骼屏 */}
      <div className="flex w-full justify-end mb-4">
        <div className="prose w-full dark:prose-invert rounded-r-md sm:w-[50vw] xl:w-[50vw]">
          <div className="bg-card py-2 px-3 rounded-md">
            <div className="space-y-2">
              <Skeleton className="h-4 w-2/3 ml-auto" />
              <Skeleton className="h-4 w-full" />
            </div>
          </div>
          <div className="flex justify-end my-1 gap-1">
            <Skeleton className="h-6 w-6 rounded" />
            <Skeleton className="h-6 w-6 rounded" />
          </div>
        </div>
      </div>

      {/* 助手消息骨骼屏 - 根据 spans 数量显示 */}
      <div className={cn(
        'mb-4',
        isMultiSpan && 'md:grid md:grid-cols-[repeat(auto-fit,minmax(375px,1fr))] gap-4'
      )}>
        {spans.map((span, index) => (
          <div key={`assistant-skeleton-3-${span.spanId}`} className="group/item">
            {/* Header - 显示真实的图标和模型名称 */}
            <div className="flex justify-between items-center h-8 mb-1">
              <div className="flex gap-1 items-center">
                <ModelProviderIcon
                  providerId={span.modelProviderId}
                  className="w-4 h-4 hidden sm:block"
                />
                <span className="text-sm">{span.modelName}</span>
              </div>
            </div>
            {/* Message content */}
            <div className={cn(
              'border-[1px] border-background rounded-md flex w-full bg-card mb-1',
              isMultiSpan && 'p-1 md:p-2',
              !isMultiSpan && 'border-none'
            )}>
              <div className="prose dark:prose-invert rounded-r-md flex-1 overflow-auto text-base py-1 px-2">
                <div className="space-y-3 py-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-5/6" />
                </div>
              </div>
            </div>
            {/* Actions */}
            <div className="flex gap-1 my-1">
              <Skeleton className="h-6 w-6 rounded" />
              <Skeleton className="h-6 w-6 rounded" />
              <Skeleton className="h-6 w-6 rounded" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default ChatMessagesSkeleton;
