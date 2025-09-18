import { FC, KeyboardEvent, useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { PromptSlim } from '@/types/prompt';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';

interface Props {
  prompt: PromptSlim;
  variables: string[];
  onSubmit: (updatedVariables: string[]) => void;
  onClose: () => void;
}

const VariableModal: FC<Props> = ({ prompt, variables, onSubmit, onClose }) => {
  const { t } = useTranslation();
  const [updatedVariables, setUpdatedVariables] = useState<
    { key: string; value: string }[]
  >(
    variables
      .map((variable) => ({ key: variable, value: '' }))
      .filter(
        (item, index, array) =>
          array.findIndex((t) => t.key === item.key) === index,
      ),
  );

  const modalRef = useRef<HTMLDivElement>(null);
  const nameInputRef = useRef<HTMLTextAreaElement>(null);

  const handleChange = (index: number, value: string) => {
    setUpdatedVariables((prev) => {
      const updated = [...prev];
      updated[index].value = value;
      return updated;
    });
  };

  const handleSubmit = () => {
    if (updatedVariables.some((variable) => variable.value === '')) {
      toast.error('Please fill out all variables');
      return;
    }

    onSubmit(updatedVariables.map((variable) => variable.value));
    onClose();
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit();
    } else if (e.key === 'Escape') {
      onClose();
    }
  };

  useEffect(() => {
    const handleOutsideClick = (e: MouseEvent) => {
      if (modalRef.current && !modalRef.current.contains(e.target as Node)) {
        onClose();
      }
    };

    window.addEventListener('click', handleOutsideClick);

    return () => {
      window.removeEventListener('click', handleOutsideClick);
    };
  }, [onClose]);

  useEffect(() => {
    if (nameInputRef.current) {
      nameInputRef.current.focus();
    }
  }, []);

  return (
    <Dialog open={!!prompt} onOpenChange={onClose}>
      <DialogContent className="w-2/5" onKeyDown={handleKeyDown}>
        <DialogHeader>
          <DialogTitle>{prompt.name}</DialogTitle>
        </DialogHeader>
        {updatedVariables.map((variable, index) => (
          <div className="mb-4" key={index}>
            <div className="mb-2 text-sm font-bold">{variable.key}</div>
            <Textarea
              ref={index === 0 ? nameInputRef : undefined}
              placeholder={`Enter a value for ${variable.key}...`}
              value={variable.value}
              onChange={(e) => handleChange(index, e.target.value)}
              rows={3}
            ></Textarea>
          </div>
        ))}
        <Button onClick={handleSubmit}>{t('Confirm')}</Button>
      </DialogContent>
    </Dialog>
  );
};

export default VariableModal;
