import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { Prompt, PromptSlim } from '@/types/prompt';

import DeletePopover from '@/pages/home/_components/Popover/DeletePopover';

import {
  IconBulbFilled,
  IconCheck,
  IconPlus,
  IconSearch,
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import PromptModal from './PromptsTab/PromptModal';

import {
  deleteUserPrompts,
  getUserPromptDetail,
  getUserPrompts,
  postUserPrompts,
  putUserPrompts,
} from '@/apis/clientApis';

const PromptsTab = () => {
  const { t } = useTranslation();
  const [prompts, setPrompts] = useState<PromptSlim[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [filteredPrompts, setFilteredPrompts] = useState<PromptSlim[]>([]);
  const [selectedPrompt, setSelectedPrompt] = useState<Prompt | null>(null);
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    fetchPrompts();
  }, []);

  useEffect(() => {
    if (searchTerm) {
      changeFilteredPrompts(prompts);
    } else {
      setFilteredPrompts(prompts);
    }
  }, [prompts, searchTerm]);

  const fetchPrompts = async () => {
    try {
      const data = await getUserPrompts();
      setPrompts(data);
    } catch (error) {
      console.error('Error fetching prompts:', error);
      toast.error(t('Failed to load prompts'));
    }
  };

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
      setPrompts(newPrompts);
      toast.success(t('Created successful'));
      fetchPromptDetails(data.id);
    });
  };

  const handleDeletePrompt = (id: number) => {
    deleteUserPrompts(id).then(() => {
      const newPrompts = prompts.filter((p) => p.id !== id);
      setPrompts(newPrompts);
      toast.success(t('Deleted successful'));
    });
  };

  const handleUpdatePrompt = (prompt: Prompt) => {
    putUserPrompts(prompt.id, prompt).then(() => {
      const existingPrompts = prompts.filter((x) => x.id !== prompt.id);
      const newPrompts = [...existingPrompts, prompt];
      setPrompts(newPrompts);
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
    <div className="flex flex-col h-full gap-4">
      <div className="flex justify-between items-center gap-4 px-2 bg-card rounded-md h-14">
        <div className="relative w-full">
          <Input
            placeholder={t('Search prompts...')}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="border-none"
          />
          <IconSearch
            className="absolute right-3 top-2.5 text-muted-foreground"
            size={18}
          />
        </div>
        <div>
          <Button size="sm" onClick={handleCreatePrompt}>
            {t('New Prompt')}
          </Button>
        </div>
      </div>

      <div className="flex-1 overflow-auto sm:hidden">
        {filteredPrompts.length === 0 ? (
          <EmptyState />
        ) : (
          <div className="space-y-2">
            {sortedPrompts.map((prompt) => (
              <Card
                key={prompt.id}
                className="p-2 h-14 flex items-center cursor-pointer hover:bg-muted/50 transition-colors relative border-none shadow-sm"
                onClick={() => handlePromptClick(prompt)}
              >
                <div className="flex items-center gap-2">
                  <IconBulbFilled
                    size={18}
                    className={getPromptColor(prompt)}
                  />
                  <div className="font-medium truncate pr-6 flex gap-2 items-center text-xs">
                    {prompt.name}
                    {prompt.isDefault && (
                      <div className="flex items-center gap-1 text-green-600">
                        <span>({t('Default')})</span>
                      </div>
                    )}
                  </div>
                </div>

                <div
                  className="absolute right-2 top-2"
                  onClick={(e) => e.stopPropagation()}
                >
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
                  <TableHead className="w-[80px] text-right">
                    {t('Actions')}
                  </TableHead>
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
                        <IconBulbFilled
                          size={18}
                          className={getPromptColor(prompt)}
                        />
                        <span>{prompt.name}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      {prompt.isDefault && (
                        <div className="flex items-center gap-1 text-green-600">
                          <IconCheck size={18} />
                          <span>{t('Default')}</span>
                        </div>
                      )}
                    </TableCell>
                    <TableCell>
                      {prompt.isSystem && (
                        <div className="flex items-center gap-1 text-green-600">
                          <IconCheck size={18} />
                          <span>{t('System')}</span>
                        </div>
                      )}
                    </TableCell>
                    <TableCell
                      className="text-right"
                      onClick={(e) => e.stopPropagation()}
                    >
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
          onUpdatePrompt={handleUpdatePrompt}
          onClose={() => setShowModal(false)}
        />
      )}
    </div>
  );
};

export default PromptsTab;
