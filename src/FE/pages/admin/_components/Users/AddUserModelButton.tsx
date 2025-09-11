import React, { useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';

import { IconPlus } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuPortal,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import ChatIcon from '@/components/ChatIcon/ChatIcon';
import { feModelProviders } from '@/types/model';

import { addUserModel, getUserUnassignedModels } from '@/apis/adminApis';

interface IProps {
  userId: string;
  onUpdate: () => void;
}

export default function AddUserModelButton({ userId, onUpdate }: IProps) {
  const { t } = useTranslation();
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [loading, setLoading] = useState(false);

  const loadUnassignedModels = async () => {
    if (models.length === 0) {
      try {
        const unassignedModels = await getUserUnassignedModels(userId);
        setModels(unassignedModels);
      } catch (error) {
        console.error('Failed to load unassigned models:', error);
        toast.error(t('Failed to load models'));
      }
    }
  };

  const handleAddModel = async (model: AdminModelDto) => {
    setLoading(true);
    try {
      const expires = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000); // 默认30天后过期
      await addUserModel({
        userId: parseInt(userId),
        modelId: model.modelId,
        tokens: 0,
        counts: 0,
        expires: expires.toISOString(),
      });
      toast.success(t('Model added successfully'));
      onUpdate();
      // 重新加载未分配模型列表
      setModels([]);
    } catch (error) {
      toast.error(t('Failed to add model'));
      console.error('Error adding user model:', error);
    } finally {
      setLoading(false);
    }
  };

  // 按提供商分组模型
  const modelGroups = models.reduce((groups, model) => {
    const providerId = model.modelProviderId;
    if (!groups[providerId]) {
      groups[providerId] = [];
    }
    groups[providerId].push(model);
    return groups;
  }, {} as Record<number, AdminModelDto[]>);

  return (
    <DropdownMenu onOpenChange={(open) => open && loadUnassignedModels()}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          disabled={loading}
          className="h-8 w-8 p-0"
        >
          <IconPlus size={16} />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-40 md:w-52">
        {models.length === 0 ? (
          <div className="p-2 mx-1 text-center text-muted-foreground text-sm">
            {loading ? t('Loading...') : t('No available models')}
          </div>
        ) : (
          <DropdownMenuGroup>
            {Object.entries(modelGroups).map(([providerId, providerModels]) => {
              const provider = feModelProviders[parseInt(providerId)];
              if (!provider) return null;
              
              return (
                <DropdownMenuSub key={providerId}>
                  <DropdownMenuSubTrigger className="p-2 flex gap-2">
                    <ChatIcon providerId={parseInt(providerId)} />
                    <span className="w-full text-nowrap overflow-hidden text-ellipsis whitespace-nowrap">
                      {t(provider.name)}
                    </span>
                  </DropdownMenuSubTrigger>
                  <DropdownMenuPortal>
                    <DropdownMenuSubContent className="max-h-96 overflow-y-auto custom-scrollbar max-w-[64px] md:max-w-[256px]">
                      {providerModels.map((model) => (
                        <DropdownMenuItem
                          key={model.modelId}
                          onClick={() => handleAddModel(model)}
                        >
                          {model.name}
                        </DropdownMenuItem>
                      ))}
                    </DropdownMenuSubContent>
                  </DropdownMenuPortal>
                </DropdownMenuSub>
              );
            })}
          </DropdownMenuGroup>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
