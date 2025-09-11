import React, { useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { formatDate } from '@/utils/date';

import { UserModelDisplay, AdminModelDto } from '@/types/adminApis';

import { 
  IconCheck, 
  IconX, 
  IconTrash 
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
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';

import { editUserModel, deleteUserModel, getModels } from '@/apis/adminApis';
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
    setLoading(true);
    try {
      await deleteUserModel(userModel.id);
      toast.success(t('Deleted successfully'));
      onUpdate();
    } catch (error) {
      toast.error(t('Delete failed'));
      console.error('Error deleting user model:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex items-center justify-between p-3 bg-background rounded-md border">
      <div className="flex items-center gap-4 flex-1">
        <div className="font-medium min-w-[120px]">
          {userModel.displayName}
        </div>
        <div className="text-sm text-muted-foreground min-w-[100px]">
          {userModel.modelKeyName}
        </div>
        
        {isEditing ? (
          <>
            <div className="flex flex-col gap-1">
              <label className="text-xs text-muted-foreground">{t('Tokens')}</label>
              <Input
                type="number"
                value={editData.tokens}
                onChange={(e) => setEditData(prev => ({ ...prev, tokens: Number(e.target.value) }))}
                className="w-20 h-8"
                min="0"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs text-muted-foreground">{t('Counts')}</label>
              <Input
                type="number"
                value={editData.counts}
                onChange={(e) => setEditData(prev => ({ ...prev, counts: Number(e.target.value) }))}
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
            </div>
          </>
        ) : (
          <>
            <div className="text-sm">
              <span className="text-muted-foreground">{t('Tokens')}:</span> {userModel.tokens}
            </div>
            <div className="text-sm">
              <span className="text-muted-foreground">{t('Counts')}:</span> {userModel.counts}
            </div>
            <div className="text-sm">
              <span className="text-muted-foreground">{t('Expires')}:</span> {formatDate(userModel.expires)}
            </div>
          </>
        )}
      </div>
      
      <div className="flex items-center gap-2">
        {isEditing ? (
          <>
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
          </>
        ) : (
          <>
            <Button
              size="sm"
              variant="outline"
              onClick={handleEdit}
              disabled={loading}
            >
              {t('Edit')}
            </Button>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={loading}
                >
                  <IconTrash size={16} />
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>{t('Confirm Delete')}</AlertDialogTitle>
                  <AlertDialogDescription>
                    {t('Are you sure you want to delete this user model? This action cannot be undone.')}
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>{t('Cancel')}</AlertDialogCancel>
                  <AlertDialogAction onClick={handleDelete}>
                    {t('Delete')}
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </>
        )}
      </div>
    </div>
  );
}
