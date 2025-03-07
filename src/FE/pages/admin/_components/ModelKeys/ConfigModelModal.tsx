import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { IconInfo } from '@/components/Icons';
import Spinner from '@/components/Spinner/Spinner';
import Tips from '@/components/Tips/Tips';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import {
  getModelKeyPossibleModels,
  postModelFastCreate,
  postModelValidate,
} from '@/apis/adminApis';

interface IProps {
  modelKeyId: number;
  modelProverId: number;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}

export interface PossibleModel {
  referenceName: string;
  modelReferenceId: number;
  isExists: boolean;
  isLegacy: boolean;
  validating: boolean;
  validateMessage: string | null;
  adding: boolean;
  deploymentName: string | null;
}

const ConfigModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const { modelKeyId, isOpen, onClose } = props;
  const [models, setModels] = useState<PossibleModel[]>([]);
  const [loading, setLoading] = React.useState(false);
  const onSave = async (index: number) => {
    const model = models[index];
    postModelFastCreate({
      modelKeyId: modelKeyId,
      modelReferenceId: model.modelReferenceId,
      deploymentName: null,
    });
  };

  useEffect(() => {
    if (isOpen) {
      setLoading(true);
      setModels([]);
      getModelKeyPossibleModels(modelKeyId).then((data) => {
        setModels(
          data.map((x) => ({
            ...x,
            validating: false,
            adding: false,
            validateMessage: null,
          })),
        );
        setLoading(false);
      });
    }
  }, [isOpen]);

  function handleAdd(index: number) {
    const model = models[index];
    const modelList = [...models];
    model.adding = true;
    setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    postModelFastCreate({
      modelKeyId: modelKeyId,
      modelReferenceId: model.modelReferenceId,
      deploymentName: model.deploymentName || null,
    }).then(() => {
      toast.success(t('Added successfully'));
      model.isExists = true;
      model.adding = false;
      setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    });
  }

  function handleValidate(index: number) {
    const model = models[index];
    const modelList = [...models];
    model.validating = true;
    setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    postModelValidate({
      modelKeyId: modelKeyId,
      modelReferenceId: model.modelReferenceId,
      deploymentName: model.deploymentName || null,
    }).then((data) => {
      if (data.isSuccess) {
        model.validateMessage = null;
        toast.success(t('Verified Successfully'));
      } else {
        toast.error(t('Verified Failed'));
        model.validateMessage = data.errorMessage;
      }
      model.validating = false;
      setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    });
  }

  function handleChangeDeploymentName(index: number, value: string) {
    const model = models[index];
    const modelList = [...models];
    model.deploymentName = value;
    setModels(modelList);
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="min-w-[375px] w-3/5">
        <DialogHeader>
          <DialogTitle>{t('Add Model')}</DialogTitle>
        </DialogHeader>
        <div className="h-[75vh] overflow-scroll flex justify-start gap-2 flex-wrap">
          <Table>
            <TableHeader>
              <TableRow className="pointer-events-none">
                <TableHead>{t('Deployment Name')}</TableHead>
                <TableHead>{t('Model Reference Name')}</TableHead>
                <TableHead className="w-20">{t('Actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {models.map((model, index) => (
                <TableRow key={model.modelReferenceId}>
                  <TableCell>
                    <Input
                      className="max-w-[300px]"
                      disabled={model.isExists}
                      value={model.deploymentName || ''}
                      onChange={(e) => {
                        handleChangeDeploymentName(index, e.target.value);
                      }}
                    />
                  </TableCell>
                  <TableCell>
                    {model.referenceName}
                    {model.isExists && (
                      <Badge variant="default" className="ml-2">
                        {t('Existed')}
                      </Badge>
                    )}
                    {model.isLegacy && (
                      <Badge variant="destructive" className="ml-2">
                        {t('Legacy')}
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="flex gap-x-2 items-center">
                    {!model.isExists && (
                      <Button
                        variant="link"
                        disabled={model.adding}
                        className="p-0"
                        onClick={() => {
                          handleAdd(index);
                        }}
                      >
                        {t('Add Model')}
                      </Button>
                    )}
                    <div className="flex gap-x-2">
                      <Button
                        variant="link"
                        disabled={model.validating}
                        className="p-0"
                        onClick={() => {
                          handleValidate(index);
                        }}
                      >
                        {t('Validate Model')}
                      </Button>
                    </div>
                    {model.validateMessage && (
                      <Tips
                        className="h-[28px]"
                        side="bottom"
                        trigger={
                          <Button variant="ghost" className="p-1 m-0 h-auto">
                            <IconInfo stroke="#FFD738" />
                          </Button>
                        }
                        content={
                          <div className="text-wrap w-80">
                            {model.validateMessage}
                          </div>
                        }
                      ></Tips>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
        {loading && (
          <div
            className={`fixed top-0 left-0 bottom-0 right-0 bg-background z-50 text-center text-[12.5px]`}
          >
            <div className="flex justify-center items-center h-16">
              <Spinner className="text-gray-500 dark:text-gray-50" />
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};
export default ConfigModelModal;
