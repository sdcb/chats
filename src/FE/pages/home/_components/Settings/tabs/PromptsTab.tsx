import { useContext, useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { Prompt, PromptSlim } from '@/types/prompt';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { IconBulbFilled, IconCheck, IconPlus, IconSearch } from '@/components/Icons';

import { setPrompts } from '../../../_actions/prompt.actions';
import HomeContext from '../../../_contexts/home.context';
import DeletePopover from '../../Popover/DeletePopover';

import {
  deleteUserPrompts,
  getUserPromptDetail,
  postUserPrompts,
  putUserPrompts,
} from '@/apis/clientApis';

import PromptModal from './PromptsTab/PromptModal';

const PromptsTab = () => {
  const { t } = useTranslation();
  const { state: { prompts }, promptDispatch } = useContext(HomeContext);

  const [searchTerm, setSearchTerm] = useState('');
  const [filteredPrompts, setFilteredPrompts] = useState<PromptSlim[]>([]);
  const [selectedPrompt, setSelectedPrompt] = useState<Prompt | null>(null);
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    if (searchTerm) {
      changeFilteredPrompts(prompts);
    } else {
      setFilteredPrompts(prompts);
    }
  }, [prompts, searchTerm]);

  const changeFilteredPrompts = (promptList: PromptSlim[]) => {
    setFilteredPrompts(
      promptList.filter((prompt) => {
        const searchable = prompt.name.toLowerCase();
        return searchable.includes(searchTerm.toLowerCase());
      }),
    );
  };

  const handleCreatePrompt = () => {
    const newPrompt: Prompt = {
      id: 0,
      name: `Prompt ${prompts.length + 1}`,
      content: '',
      isDefault: false,
      isSystem: false,
      temperature: null,
    };

    postUserPrompts(newPrompt).then((data) => {
      const newPrompts = [...prompts, data];
      promptDispatch(setPrompts(newPrompts));
      toast.success(t('Created successful'));
      fetchPromptDetails(data.id);
    });
  };

  const handleDeletePrompt = (id: number) => {
    deleteUserPrompts(id).then(() => {
      const newPrompts = prompts.filter((p) => p.id !== id);
      promptDispatch(setPrompts(newPrompts));
      toast.success(t('Deleted successful'));
    });
  };

  const handleUpdatePrompt = (prompt: Prompt) => {
    putUserPrompts(prompt.id, prompt).then(() => {
      const existingPrompts = prompts.filter((x) => x.id !== prompt.id);
      const newPrompts = [...existingPrompts, prompt];
      promptDispatch(setPrompts(newPrompts));
      toast.success(t('Updated successful'));
    });
  };

  const fetchPromptDetails = (id: number) => {
    getUserPromptDetail(id).then((data) => {
      setSelectedPrompt(data);
      setShowModal(true);
    });
  };

  const handlePromptClick = (prompt: PromptSlim) => {
    fetchPromptDetails(prompt.id);
  };

  const getPromptColor = (prompt: PromptSlim) => {
    if (prompt.isSystem) {
      return 'text-green-700';
    } else if (prompt.isDefault) {
      return 'text-blue-700';
    } else {
      return 'text-gray-600';
    }
  };

  const sortedPrompts = [...filteredPrompts].reverse();

  const EmptyState = () => (
    <div className="flex flex-col items-center justify-center h-full text-center p-4">
      <IconBulbFilled size={48} className="text-muted-foreground mb-2" />
      <p className="text-muted-foreground">
        {searchTerm ? t('No prompts found') : t('No prompts created yet')}
      </p>
      <Button
        variant="outline"
        size="sm"
        className="mt-4"
        onClick={handleCreatePrompt}
      >
        {t('Create your first prompt')}
      </Button>
    </div>
  );

  return (
    <div className="flex flex-col h-full">
      <div className="flex justify-end mb-4">
        <Button size="sm" onClick={handleCreatePrompt}>
          {t('New Prompt')}
        </Button>
      </div>

      <div className="relative mb-4">
        <Input
          placeholder={t('Search prompts...')}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="pr-10"
        />
        <IconSearch className="absolute right-3 top-2.5 text-muted-foreground" size={18} />
      </div>

      <div className="flex-1 overflow-auto -mx-2 sm:mx-0 px-2 sm:px-0 block sm:hidden">
        {filteredPrompts.length === 0 ? (
          <EmptyState />
        ) : (
          <div className="space-y-2">
            {sortedPrompts.map((prompt) => (
              <Card
                key={prompt.id}
                className="p-3 cursor-pointer hover:bg-muted/50 transition-colors relative border-none shadow-sm"
                onClick={() => handlePromptClick(prompt)}
              >
                <div className="flex items-center gap-2">
                  <IconBulbFilled size={18} className={getPromptColor(prompt)} />
                  <div className="font-medium truncate pr-6 flex gap-2 items-center text-xs">{prompt.name}{prompt.isDefault && (
                    <div className="flex items-center gap-1 text-green-600">
                      <span>({t('Default')})</span>
                    </div>
                  )}</div>
                </div>

                <div className="absolute right-2 top-0" onClick={(e) => e.stopPropagation()}>
                  <DeletePopover
                    onDelete={() => handleDeletePrompt(prompt.id)}
                  />
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>

      <div className="flex-1 overflow-auto hidden sm:block">
        {filteredPrompts.length === 0 ? (
          <EmptyState />
        ) : (
          <Card className="border-none">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[300px]">{t('Name')}</TableHead>
                  <TableHead className="w-[120px]">{t('Default')}</TableHead>
                  <TableHead className="w-[120px]">{t('System')}</TableHead>
                  <TableHead className="w-[80px] text-right">{t('Actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {sortedPrompts.map((prompt) => (
                  <TableRow
                    key={prompt.id}
                    className="cursor-pointer"
                    onClick={() => handlePromptClick(prompt)}
                  >
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        <IconBulbFilled size={18} className={getPromptColor(prompt)} />
                        <span className="truncate">{prompt.name}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      {prompt.isDefault ? (
                        <div className="flex items-center gap-1 text-green-600">
                          <span>{t('Yes')}</span>
                        </div>
                      ) : (
                        <span className="text-muted-foreground">{t('No')}</span>
                      )}
                    </TableCell>
                    <TableCell>
                      {prompt.isSystem ? (
                        <div className="flex items-center gap-1 text-green-600">
                          <span>{t('Yes')}</span>
                        </div>
                      ) : (
                        <span className="text-muted-foreground">{t('No')}</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                      <DeletePopover
                        onDelete={() => handleDeletePrompt(prompt.id)}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>
        )}
      </div>

      {showModal && selectedPrompt && (
        <PromptModal
          prompt={selectedPrompt}
          onClose={() => setShowModal(false)}
          onUpdatePrompt={handleUpdatePrompt}
        />
      )}
    </div>
  );
};

export default PromptsTab; 