import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';

import { termDateString } from '@/utils/common';
import { formatDate } from '@/utils/date';

import { UserModelDisplay } from '@/types/adminApis';

import { IconSquareRoundedX } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import { getModelsByUserId, putUserModel } from '@/apis/adminApis';
import { cn } from '@/lib/utils';

interface IProps {
  userId: string;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}
const EditUserModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const { isOpen, onClose, onSuccessful } = props;
  const [submit, setSubmit] = useState(false);
  const [models, setModels] = useState<UserModelDisplay[]>([]);
  const [filteredModels, setFilteredModels] = useState<UserModelDisplay[]>([]);
  const [query, setQuery] = useState('');

  const updateQueryWithDebounce = useDebounce((q: string) => {
    let queryData = JSON.parse(JSON.stringify(models));

    if (q) {
      queryData = models.filter((x) =>
        x.displayName.toLowerCase().includes(q.toLowerCase()),
      );
    }
    setFilteredModels(queryData);
  }, 1000);

  useEffect(() => {
    if (isOpen) {
      getModelsByUserId(props.userId).then((data) => {
        setModels(data);
        setFilteredModels(data);
      });
    }
  }, [isOpen]);

  const onSubmit = async () => {
    setSubmit(true);
    putUserModel({
      userId: props.userId,
      models: models.map((x) => x.toUpdateDto()),
    })
      .then(() => {
        toast.success(t('Save successful'));
        onSuccessful();
      })
      .finally(() => {
        setSubmit(false);
      });
  };

  const onChangeModel = (
    index: number,
    type: 'tokens' | 'counts' | 'expires' | 'enabled',
    value: any,
  ) => {
    const _models = models as any;
    _models[index][type] = value;
    setModels([..._models]);
    setFilteredModels([..._models]);
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('Edit User Model')}</DialogTitle>
        </DialogHeader>
        <div>
          <Input
            className="max-w-[238px] w-full"
            value={query}
            placeholder={t('Search...')}
            onChange={(e) => {
              setQuery(e.target.value);
              updateQueryWithDebounce(e.target.value);
            }}
          />
        </div>
        <div className="h-[48em] overflow-scroll flex justify-start gap-2 flex-wrap">
          <Table>
            <TableHeader>
              <TableRow className="pointer-events-none">
                <TableHead>{t('Model Display Name')}</TableHead>
                <TableHead>{t('Model Key')}</TableHead>
                <TableHead>{t('Tokens')}</TableHead>
                <TableHead>{t('Chat Counts')}</TableHead>
                <TableHead>{t('Expiration Time')}</TableHead>
                <TableHead>{t('Is Enabled')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredModels.map((model, index) => (
                <TableRow key={`user-model-${model.id}-${index}`}>
                  <TableCell>{model.displayName}</TableCell>
                  <TableCell>{model.modelKeyName}</TableCell>
                  <TableCell>
                    <Input
                      className="w-16"
                      value={model.tokens?.toString()}
                      onChange={(e) => {
                        onChangeModel(
                          index,
                          'tokens',
                          parseInt(e.target.value) || 0,
                        );
                      }}
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      className="w-16"
                      value={model.counts?.toString()}
                      onChange={(e) => {
                        onChangeModel(
                          index,
                          'counts',
                          parseInt(e.target.value) || 0,
                        );
                      }}
                    />
                  </TableCell>
                  <TableCell>
                    <Popover>
                      <PopoverTrigger asChild>
                        <Button
                          variant={'outline'}
                          className={cn('pl-3 text-left font-normal w-[150px]')}
                        >
                          {formatDate(model.expires)}
                          <IconSquareRoundedX
                            onClick={(e) => {
                              onChangeModel(index, 'expires', termDateString());
                              e.preventDefault();
                            }}
                            className="z-10 ml-auto h-5 w-5 opacity-50"
                          />
                        </Button>
                      </PopoverTrigger>
                      <PopoverContent className="w-auto p-0" align="start">
                        <Calendar
                          mode="single"
                          selected={new Date(model.expires)}
                          onSelect={(d) => {
                            onChangeModel(index, 'expires', d?.toISOString());
                          }}
                          initialFocus
                        />
                      </PopoverContent>
                    </Popover>
                  </TableCell>
                  <TableCell>
                    <Switch
                      checked={model.enabled}
                      onCheckedChange={(checked) => {
                        onChangeModel(index, 'enabled', checked);
                        if (checked) {
                          onChangeModel(index, 'expires', termDateString());
                        }
                      }}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
        <DialogFooter className="pt-4">
          <Button disabled={submit} onClick={onSubmit} type="submit">
            {t('Save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
export default EditUserModelModal;
