import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { McpServerDetailsDto, McpToolDto, McpToolBasicInfo, UpdateMcpServerRequest } from '@/types/clientApis';

import {
  IconPlus,
  IconTrash,
  IconRefresh,
  IconCheck,
  IconX,
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card } from '@/components/ui/card';

import { fetchMcpTools } from '@/apis/clientApis';
import { getIconStroke } from '@/utils/common';

interface McpModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (serverData: UpdateMcpServerRequest) => Promise<void>;
  server?: McpServerDetailsDto | null;
  isCreateMode: boolean;
  isReadOnly?: boolean;
  isLoadingData?: boolean;
}

const McpModal = ({ isOpen, onClose, onSave, server, isCreateMode, isReadOnly = false, isLoadingData = false }: McpModalProps) => {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [formData, setFormData] = useState({
    label: '',
    url: '',
    headers: '',
    isSystem: false,
  });
  const [tools, setTools] = useState<McpToolDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [fetchingTools, setFetchingTools] = useState(false);
  
  // JSON 验证函数
  const validateJSON = (jsonString: string): boolean => {
    if (!jsonString.trim()) return true; // 空字符串认为是有效的
    try {
      JSON.parse(jsonString);
      return true;
    } catch {
      return false;
    }
  };

  useEffect(() => {
    if (server && !isCreateMode) {
      setFormData({
        label: server.label,
        url: server.url,
        headers: server.headers || '',
        isSystem: server.isSystem,
      });
      setTools(server.tools);
    } else {
      setFormData({
        label: '',
        url: '',
        headers: '',
        isSystem: false,
      });
      setTools([]);
    }
  }, [server, isCreateMode]);

  const handleInputChange = (field: string, value: any) => {
    setFormData(prev => ({
      ...prev,
      [field]: value,
    }));
  };

  const handleFetchTools = async () => {
    if (!formData.url) {
      toast.error(t('Please enter server URL first'));
      return;
    }

    setFetchingTools(true);
    try {
      const fetchedTools = await fetchMcpTools({
        serverUrl: formData.url,
        headers: formData.headers || undefined,
      });

      const newTools: McpToolDto[] = fetchedTools.map(tool => ({
        ...tool,
        requireApproval: false,
      }));

      setTools(newTools);
      toast.success(t('Tools fetched successfully'));
    } catch (error) {
      console.error('Failed to fetch tools:', error);
      toast.error(t('Failed to fetch tools'));
    } finally {
      setFetchingTools(false);
    }
  };

  const handleToolChange = (index: number, field: keyof McpToolDto, value: any) => {
    setTools(prev => prev.map((tool, i) => 
      i === index ? { ...tool, [field]: value } : tool
    ));
  };

  const handleAddTool = () => {
    setTools(prev => [
      ...prev,
      {
        name: '',
        description: '',
        parameters: '',
        requireApproval: false,
      }
    ]);
  };

  const handleRemoveTool = (index: number) => {
    setTools(prev => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
    if (!formData.label.trim()) {
      toast.error(t('Please enter a label'));
      return;
    }

    if (!formData.url.trim()) {
      toast.error(t('Please enter a URL'));
      return;
    }

    // 验证 headers 是否为有效的 JSON
    if (formData.headers && !validateJSON(formData.headers)) {
      toast.error(t('Invalid JSON format in headers'));
      return;
    }

    // Validate tools
    for (const tool of tools) {
      if (!tool.name.trim()) {
        toast.error(t('All tools must have a name'));
        return;
      }
      
      // 验证参数是否为有效的 JSON
      if (tool.parameters && !validateJSON(tool.parameters)) {
        toast.error(t('Invalid JSON format in tool parameters'));
        return;
      }
    }

    // Check for duplicate tool names
    const toolNames = tools.map(t => t.name);
    const uniqueNames = new Set(toolNames);
    if (toolNames.length !== uniqueNames.size) {
      toast.error(t('Tool names must be unique'));
      return;
    }

    setLoading(true);
    try {
      await onSave({
        label: formData.label.trim(),
        url: formData.url.trim(),
        headers: formData.headers.trim() || undefined,
        isSystem: formData.isSystem,
        tools,
      });
    } catch (error) {
      console.error('Failed to save server:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-6xl max-h-[95vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {isCreateMode ? t('Add MCP Server') : isReadOnly ? t('View MCP Server') : t('Edit MCP Server')}
          </DialogTitle>
        </DialogHeader>

        {isLoadingData ? (
          <div className="flex items-center justify-center py-16">
            <div className="flex items-center space-x-3">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
              <p className="text-lg">{t('Loading server details...')}</p>
            </div>
          </div>
        ) : (
          <div className="space-y-6">
          {/* Basic Information */}
          <Card className="p-4">
            <h3 className="text-lg font-medium mb-4">{t('Basic Information')}</h3>
            <div className="space-y-4">
              <div>
                <Label htmlFor="label">{t('Label')}</Label>
                <Input
                  id="label"
                  value={formData.label}
                  onChange={(e) => handleInputChange('label', e.target.value)}
                  placeholder={t('Enter server label')}
                  disabled={isReadOnly}
                />
              </div>

              <div>
                <Label htmlFor="url">{t('Server URL')}</Label>
                <Input
                  id="url"
                  value={formData.url}
                  onChange={(e) => handleInputChange('url', e.target.value)}
                  placeholder="wss://example.com/mcp"
                  disabled={isReadOnly}
                />
              </div>

              <div>
                <Label htmlFor="headers">{t('Headers (JSON)')}</Label>
                <Textarea
                  id="headers"
                  value={formData.headers}
                  onChange={(e) => handleInputChange('headers', e.target.value)}
                  placeholder='{"Authorization": "Bearer token"}'
                  rows={3}
                  disabled={isReadOnly}
                  className={`${
                    formData.headers && !validateJSON(formData.headers) 
                      ? 'border-red-500 focus:border-red-500' 
                      : ''
                  }`}
                />
                {formData.headers && !validateJSON(formData.headers) && (
                  <p className="text-xs text-red-500 mt-1">{t('Invalid JSON format')}</p>
                )}
              </div>

              <div className="flex items-center space-x-2">
                <Switch
                  id="isPublic"
                  checked={formData.isSystem}
                  onCheckedChange={(checked) => handleInputChange('isPublic', checked)}
                  disabled={isReadOnly}
                />
                <Label htmlFor="isPublic">{t('Public Server')}</Label>
              </div>
            </div>
          </Card>

          {/* Tools */}
          <Card className="p-4">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-medium">{t('Tools')}</h3>
              {!isReadOnly && (
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleFetchTools}
                    disabled={fetchingTools}
                  >
                    <IconRefresh size={16} className="mr-2" />
                    {fetchingTools ? t('Fetching...') : t('Fetch Tools')}
                  </Button>
                  <Button variant="outline" size="sm" onClick={handleAddTool}>
                    <IconPlus size={16} className="mr-2" />
                    {t('Add Tool')}
                  </Button>
                </div>
              )}
            </div>

            {tools.length === 0 ? (
              <div className="text-center py-8 text-muted-foreground">
                {t('No tools configured')}
              </div>
            ) : (
              <div className="overflow-x-auto">
                <Table className="table-compact">
                  <TableHeader>
                    <TableRow>
                      <TableHead className="px-2">{t('Name')}</TableHead>
                      <TableHead className="px-2">{t('Description')}</TableHead>
                      <TableHead className="px-2">{t('Parameters (JSON)')}</TableHead>
                      <TableHead className="px-2">{t('Actions')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tools.map((tool, index) => (
                      <TableRow key={index}>
                        <TableCell className="px-2">
                          <Input
                            value={tool.name}
                            onChange={(e) => handleToolChange(index, 'name', e.target.value)}
                            placeholder={t('Tool name')}
                            className="font-mono w-32"
                            disabled={isReadOnly}
                          />
                        </TableCell>
                        <TableCell className="px-2">
                          <Textarea
                            value={tool.description || ''}
                            onChange={(e) => handleToolChange(index, 'description', e.target.value)}
                            placeholder={t('Tool description')}
                            rows={3}
                            className="min-w-[200px]"
                            disabled={isReadOnly}
                          />
                        </TableCell>
                        <TableCell className="px-2">
                          <Textarea
                            value={tool.parameters || ''}
                            onChange={(e) => handleToolChange(index, 'parameters', e.target.value)}
                            placeholder='{"type": "object", "properties": {...}}'
                            rows={3}
                            className={`min-w-[400px] font-mono text-xs ${
                              tool.parameters && !validateJSON(tool.parameters) 
                                ? 'border-red-500 focus:border-red-500' 
                                : ''
                            }`}
                            disabled={isReadOnly}
                          />
                          {tool.parameters && !validateJSON(tool.parameters) && (
                            <p className="text-xs text-red-500 mt-1">{t('Invalid JSON format')}</p>
                          )}
                        </TableCell>
                        <TableCell className="px-2">
                          {!isReadOnly && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleRemoveTool(index)}
                            >
                              <IconTrash size={16} />
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </Card>
        </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isLoadingData}>
            <IconX size={16} className="mr-2" />
            {isReadOnly ? t('Close') : t('Cancel')}
          </Button>
          {!isReadOnly && (
            <Button onClick={handleSubmit} disabled={loading || isLoadingData}>
              <IconCheck size={16} className="mr-2" stroke={getIconStroke(theme)}/>
              {loading ? t('Saving...') : t('Save')}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default McpModal;
