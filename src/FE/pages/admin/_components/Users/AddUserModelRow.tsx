import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { formatDate } from '@/utils/date';

import { AdminModelDto } from '@/types/adminApis';

import { 
  IconCheck, 
  IconX,
  IconPlus 
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Calendar } from '@/components/ui/calendar';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import { addUserModel, getModels } from '@/apis/adminApis';
import { cn } from '@/lib/utils';

interface IProps {
  userId: string;
  onUpdate: () => void;
}

export default function AddUserModelRow({ userId, onUpdate }: IProps) {
  const { t } = useTranslation();
  const [isAdding, setIsAdding] = useState(false);
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [addData, setAddData] = useState({
    modelId: 0,
    tokens: 0,
    counts: 0,
    expires: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000), // 默认30天后过期
  });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isAdding && models.length === 0) {
      loadModels();
    }
  }, [isAdding]);

  const loadModels = async () => {
    try {
      const modelsData = await getModels(false); // 只获取启用的模型
      setModels(modelsData);
    } catch (error) {
      console.error('Failed to load models:', error);
    }
  };

  const handleAdd = () => {
    setIsAdding(true);
  };

  const handleCancel = () => {
    setIsAdding(false);
    setAddData({
      modelId: 0,
      tokens: 0,
      counts: 0,
      expires: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000),
    });
  };

  const handleSave = async () => {
    if (addData.modelId === 0) {
      toast.error(t('Please select a model'));
      return;
    }

    setLoading(true);
    try {
      await addUserModel({
        userId: parseInt(userId),
        modelId: addData.modelId,
        tokens: addData.tokens,
        counts: addData.counts,
        expires: addData.expires.toISOString(),
      });
      toast.success(t('Added successfully'));
      setIsAdding(false);
      handleCancel();
      onUpdate();
    } catch (error) {
      toast.error(t('Add failed'));
      console.error('Error adding user model:', error);
    } finally {
      setLoading(false);
    }
  };

  if (!isAdding) {
    return (
      <div className="flex items-center justify-center p-3 bg-muted/50 rounded-md border border-dashed">
        <Button
          variant="ghost"
          onClick={handleAdd}
          className="text-muted-foreground hover:text-foreground"
        >
          <IconPlus size={16} className="mr-2" />
          {t('Add Model')}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-between p-3 bg-background rounded-md border">
      <div className="flex items-center gap-4 flex-1">
        <div className="min-w-[120px]">
          <Select
            value={addData.modelId.toString()}
            onValueChange={(value) => setAddData(prev => ({ ...prev, modelId: Number(value) }))}
          >
            <SelectTrigger className="w-full">
              <SelectValue placeholder={t('Select Model')} />
            </SelectTrigger>
            <SelectContent>
              {models.map((model) => (
                <SelectItem key={model.modelId} value={model.modelId.toString()}>
                  {model.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">{t('Tokens')}</label>
          <Input
            type="number"
            value={addData.tokens}
            onChange={(e) => setAddData(prev => ({ ...prev, tokens: Number(e.target.value) }))}
            className="w-20 h-8"
            min="0"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">{t('Counts')}</label>
          <Input
            type="number"
            value={addData.counts}
            onChange={(e) => setAddData(prev => ({ ...prev, counts: Number(e.target.value) }))}
            className="w-20 h-8"
            min="0"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">{t('Expires')}</label>
          <Popover>
            <PopoverTrigger asChild>
              <Button
                variant="outline"
                className={cn(
                  "w-[140px] h-8 justify-start text-left font-normal",
                  !addData.expires && "text-muted-foreground"
                )}
              >
                📅 {addData.expires ? formatDate(addData.expires.toISOString()) : <span>{t('Pick a date')}</span>}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-auto p-0">
              <Calendar
                mode="single"
                selected={addData.expires}
                onSelect={(date) => date && setAddData(prev => ({ ...prev, expires: date }))}
                initialFocus
              />
            </PopoverContent>
          </Popover>
        </div>
      </div>
      
      <div className="flex items-center gap-2">
        <Button
          size="sm"
          variant="outline"
          onClick={handleSave}
          disabled={loading}
        >
          <IconCheck size={16} />
        </Button>
        <Button
          size="sm"
          variant="outline"
          onClick={handleCancel}
          disabled={loading}
        >
          <IconX size={16} />
        </Button>
      </div>
    </div>
  );
}
