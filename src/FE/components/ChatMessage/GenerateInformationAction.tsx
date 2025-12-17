import { IChatMessage, IStepGenerateInfo } from '@/types/chatMessage';
import {
  aggregateStepGenerateInfo,
  fetchGenerateInfoCached,
  getCachedGenerateInfo,
  subscribeGenerateInfoCache,
  GenerateInfoCacheKey,
  requestGenerateInfo,
} from '@/utils/generateInfoCache';

import { IconInfo } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { GenerateInfoPopoverContent } from './GenerateInfoPopoverContent';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  message: IChatMessage;
  chatId?: string;
  chatShareId?: string;
  isAdminView?: boolean;
}

export const GenerateInformationAction = (props: Props) => {
  const { message, hidden, disabled, chatId, chatShareId } = props;
  const [isOpen, setIsOpen] = useState(false);
  const cacheKey = useMemo<GenerateInfoCacheKey>(
    () => ({ turnId: message.id, chatId, chatShareId }),
    [message.id, chatId, chatShareId],
  );
  const [stepInfos, setStepInfos] = useState<IStepGenerateInfo[] | null>(() => {
    const cached = getCachedGenerateInfo(cacheKey);
    return cached ? [...cached] : null;
  });
  const [loading, setLoading] = useState(false);

  const fetchGenerateInfo = useCallback(async () => {
    if (stepInfos) return;

    const cached = getCachedGenerateInfo(cacheKey);
    if (cached) {
      setStepInfos([...cached]);
      return;
    }

    setLoading(true);
    try {
      const infos = await fetchGenerateInfoCached(cacheKey, () => requestGenerateInfo(cacheKey));
      setStepInfos(infos ? [...infos] : []);
    } catch (error) {
      console.error('Failed to fetch generate info:', error);
    } finally {
      setLoading(false);
    }
  }, [stepInfos, cacheKey]);

  useEffect(() => {
    if (isOpen && !stepInfos && !loading) {
      fetchGenerateInfo();
    }
  }, [isOpen, stepInfos, loading, fetchGenerateInfo]);

  useEffect(() => {
    const cached = getCachedGenerateInfo(cacheKey);
    if (cached && cached.length > 0) {
      setStepInfos([...cached]);
    } else {
      setStepInfos(null);
    }

    const unsubscribe = subscribeGenerateInfoCache(cacheKey, (data) => {
      setStepInfos(data.length > 0 ? [...data] : []);
    });

    return () => {
      unsubscribe();
    };
  }, [cacheKey]);

  // 聚合步骤数据
  const info = aggregateStepGenerateInfo(stepInfos);

  if (hidden) return null;

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        <Button
          disabled={disabled}
          variant="ghost"
          className="p-1 m-0 h-7 w-7 hover:bg-accent hover:text-accent-foreground transition-colors"
          onClick={(e) => {
            e.stopPropagation();
            setIsOpen(!isOpen);
          }}
          onMouseEnter={() => {
            if (window.matchMedia('(hover: hover)').matches) {
              setIsOpen(true);
            }
          }}
          onMouseLeave={() => {
            if (window.matchMedia('(hover: hover)').matches) {
              setIsOpen(false);
            }
          }}
        >
          <IconInfo />
        </Button>
      </PopoverTrigger>
      <PopoverContent 
        side="bottom" 
        className="w-auto p-1 shadow-lg border-2"
        onPointerDownOutside={() => setIsOpen(false)}
      >
        <GenerateInfoPopoverContent info={info} loading={loading} />
      </PopoverContent>
    </Popover>
  );
};

export default GenerateInformationAction;
