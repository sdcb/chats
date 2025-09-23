import React, { useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { formatDate } from '@/utils/date';

import { UserModelDisplay } from '@/types/adminApis';

import { 
  IconCheck, 
  IconX, 
  IconEdit 
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Calendar } from '@/components/ui/calendar';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  TableCell,
  TableRow,
} from '@/components/ui/table';
import DeletePopover from '@/components/Popover/DeletePopover';
import ChatIcon from '@/components/ChatIcon/ChatIcon';

import { editUserModel, deleteUserModel } from '@/apis/adminApis';
import { cn } from '@/lib/utils';

interface IProps {
  userModel: UserModelDisplay;
  userId: string;
  onUpdate: () => void;
}

export default function UserModelRow({ userModel, userId, onUpdate }: IProps) {
  const { t } = useTranslation();
  const [isEditing, setIsEditing] = useState(false);
  const [editData, setEditData] = useState({
    tokens: userModel.tokens,
    counts: userModel.counts,
    expires: new Date(userModel.expires),
  });
  const [loading, setLoading] = useState(false);

  const handleEdit = () => {
    setIsEditing(true);
    setEditData({
      tokens: userModel.tokens,
      counts: userModel.counts,
      expires: new Date(userModel.expires),
    });
  };

  const handleCancel = () => {
    setIsEditing(false);
    setEditData({
      tokens: userModel.tokens,
      counts: userModel.counts,
      expires: new Date(userModel.expires),
    });
  };

  const handleSave = async () => {
    setLoading(true);
    try {
      await editUserModel(userModel.id, {
        tokens: editData.tokens,
        counts: editData.counts,
        expires: editData.expires.toISOString(),
      });
      toast.success(t('Saved successfully'));
      setIsEditing(false);
      onUpdate();
    } catch (error) {
      toast.error(t('Save failed'));
      console.error('Error saving user model:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    await deleteUserModel(userModel.id);
    toast.success(t('Deleted successfully'));
    onUpdate();
  };

  return (
    <TableRow>
      <TableCell className="font-medium">
        <div className="flex items-center gap-2">
          <ChatIcon providerId={userModel.modelProviderId} />
          {userModel.displayName}
        </div>
      </TableCell>
      <TableCell className="text-muted-foreground hidden sm:table-cell">
        {userModel.modelKeyName}
      </TableCell>
      <TableCell>
        {isEditing ? (
          <Input
            type="number"
            value={editData.tokens}
            onChange={(e) => setEditData(prev => ({ ...prev, tokens: Number(e.target.value) }))}
            className="w-32 h-8"
            min="0"
          />
        ) : (
          userModel.tokens
        )}
      </TableCell>
      <TableCell>
        {isEditing ? (
          <Input
            type="number"
            value={editData.counts}
            onChange={(e) => setEditData(prev => ({ ...prev, counts: Number(e.target.value) }))}
            className="w-32 h-8"
            min="0"
          />
        ) : (
          userModel.counts
        )}
      </TableCell>
      <TableCell>
        {isEditing ? (
          <Popover>
            <PopoverTrigger asChild>
              <Button
                variant="outline"
                className={cn(
                  "w-[140px] h-8 justify-start text-left font-normal",
                  !editData.expires && "text-muted-foreground"
                )}
              >
                📅 {editData.expires ? formatDate(editData.expires.toISOString()) : <span>{t('Pick a date')}</span>}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-auto p-0">
              <Calendar
                mode="single"
                selected={editData.expires}
                onSelect={(date) => date && setEditData(prev => ({ ...prev, expires: date }))}
                initialFocus
              />
            </PopoverContent>
          </Popover>
        ) : (
          formatDate(userModel.expires)
        )}
      </TableCell>
      <TableCell>
        <div className="flex items-center gap-2 justify-end">
          {isEditing ? (
            <>
              <Button
                size="sm"
                variant="ghost"
                onClick={handleSave}
                disabled={loading}
              >
                <IconCheck size={16} />
              </Button>
              <Button
                size="sm"
                variant="ghost"
                onClick={handleCancel}
                disabled={loading}
              >
                <IconX size={16} />
              </Button>
            </>
          ) : (
            <>
              <Button
                size="sm"
                variant="ghost"
                onClick={handleEdit}
                disabled={loading}
              >
                <IconEdit size={16} />
              </Button>
              <DeletePopover onDelete={handleDelete} />
            </>
          )}
        </div>
      </TableCell>
    </TableRow>
  );
}
