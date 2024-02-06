import { getModels, putUserModel } from '@/apis/adminService';
import { GetModelResult, GetUserModelResult } from '@/types/admin';
import {
  Button,
  Modal,
  ModalBody,
  ModalContent,
  ModalFooter,
  ModalHeader,
  Select,
  SelectItem,
} from '@nextui-org/react';
import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

interface IProps {
  isOpen: boolean;
  selectedModel: GetUserModelResult | null;
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

export const AddUserModelModal = (props: IProps) => {
  const { isOpen, selectedModel, onClose, onSuccessful } = props;
  const [select, setSelect] = useState<GetModelResult>();
  const [models, setModel] = useState<GetModelResult[]>([]);

  useEffect(() => {
    isOpen &&
      getModels().then((data) => {
        const _models = data.filter(
          (x) =>
            !selectedModel?.models.find(
              (m) => m.modelId === x.modelId && m.enable
            )
        );
        setModel(_models);
      });
  }, [isOpen]);

  const handleSave = () => {
    let models = selectedModel!.models;
    const foundModel = models.find((m) => m.modelId === select?.modelId);
    if (!foundModel) {
      models.push({ modelId: select?.modelId!, enable: true });
    } else {
      models = models.map((x) => {
        return x.modelId === select?.modelId ? { ...x, enable: true } : x;
      });
    }
    putUserModel({
      userModelId: selectedModel!.userModelId,
      models,
    })
      .then(() => {
        toast.success('Save successful!');
        onSuccessful();
      })
      .catch(() => {
        toast.error(
          'Save failed! Please try again later, or contact technical personnel.'
        );
      });
  };

  return (
    <Modal
      backdrop='transparent'
      placement='top'
      isOpen={isOpen}
      onClose={onClose}
    >
      <ModalContent>
        {(onClose) => (
          <>
            <ModalHeader className='flex flex-col gap-1'>Add Model</ModalHeader>
            <ModalBody>
              <Select
                value={select?.modelId}
                labelPlacement='outside'
                label='Select an Model'
                onChange={(ev) => {
                  setSelect(models.find((x) => x.modelId === ev.target.value));
                }}
              >
                {models.map((model) => (
                  <SelectItem key={model.modelId} value={model.modelId}>
                    {model.modelId}
                  </SelectItem>
                ))}
              </Select>
            </ModalBody>
            <ModalFooter>
              <Button color='danger' variant='light' onPress={onClose}>
                Close
              </Button>
              <Button color='primary' onPress={handleSave}>
                Save
              </Button>
            </ModalFooter>
          </>
        )}
      </ModalContent>
    </Modal>
  );
};