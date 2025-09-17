import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { McpServerDetailsDto, McpToolBasicInfo, UpdateMcpServerRequest } from '@/types/clientApis';

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
import { getUserInfo } from '@/utils/user';

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
  });
  const [tools, setTools] = useState<McpToolBasicInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [fetchingTools, setFetchingTools] = useState(false);
  const [labelError, setLabelError] = useState<string | null>(null);
  const user = getUserInfo();
  const isAdmin = user?.role === 'admin';

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
      });
      setTools(server.tools);
    } else {
      setFormData({
        label: '',
        url: '',
        headers: '',
      });
      setTools([]);
    }
  }, [server, isCreateMode]);

  const handleInputChange = (field: string, value: any) => {
    setFormData(prev => ({
      ...prev,
      [field]: value,
    }));

    if (field === 'label') {
      if (value && value.includes(':')) {
        setLabelError(t("Label cannot contain ':'"));
      } else {
        setLabelError(null);
      }
    }
  };

  // internal fetch tools with optional silent mode (no toast)
  const fetchToolsInternal = async (silent: boolean): Promise<McpToolBasicInfo[] | null> => {
    if (!formData.url) {
      if (!silent) toast.error(t('Please enter server URL first'));
      return null;
    }

    setFetchingTools(true);
    try {
      const fetchedTools = await fetchMcpTools({
        serverUrl: formData.url,
        headers: formData.headers || undefined,
      });

      const newTools: McpToolBasicInfo[] = fetchedTools.map(tool => ({
        ...tool,
      }));

      setTools(newTools);
      if (!silent) toast.success(t('Tools fetched successfully'));
      return newTools;
    } catch (error) {
      console.error('Failed to fetch tools:', error);
      if (!silent) toast.error(t('Failed to fetch tools'));
      return null;
    } finally {
      setFetchingTools(false);
    }
  };

  const handleFetchTools = async () => {
    await fetchToolsInternal(false);
  };

  const handleToolChange = (index: number, field: keyof McpToolBasicInfo, value: any) => {
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

    if (formData.label.includes(':')) {
      toast.error(t("Label cannot contain ':'"));
      setLabelError(t("Label cannot contain ':'"));
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

    // If in create mode and tools are empty, auto-fetch tools first, then proceed to save regardless of result
    let toolsToSave = tools;
    let skipToolValidation = false;
    if (isCreateMode && tools.length === 0) {
      const fetchedTools = await fetchToolsInternal(true); // silent fetch, shows fetching state on button
      if (fetchedTools) {
        toolsToSave = fetchedTools;
      }
      skipToolValidation = true;
    }

    if (!skipToolValidation) {
      // Validate tools
      for (const tool of toolsToSave) {
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
      const toolNames = toolsToSave.map(t => t.name);
      const uniqueNames = new Set(toolNames);
      if (toolNames.length !== uniqueNames.size) {
        toast.error(t('Tool names must be unique'));
        return;
      }
    }

    setLoading(true);
    try {
      await onSave({
        label: formData.label.trim(),
        url: formData.url.trim(),
        headers: formData.headers.trim() || undefined,
        tools: toolsToSave,
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
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-medium">{t('Basic Information')}</h3>
              </div>
              <div className="space-y-4">
                <div>
                  <Label htmlFor="label">{t('Label')}</Label>
                  <Input
                    id="label"
                    value={formData.label}
                    onChange={(e) => handleInputChange('label', e.target.value)}
                    placeholder={t('Enter server label')}
                    disabled={isReadOnly}
                    className={labelError ? 'border-red-500 focus:border-red-500' : ''}
                  />
                  {labelError && (
                    <p className="text-xs text-red-500 mt-1">{labelError}</p>
                  )}
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
                    className={`${formData.headers && !validateJSON(formData.headers)
                        ? 'border-red-500 focus:border-red-500'
                        : ''
                      }`}
                  />
                  {formData.headers && !validateJSON(formData.headers) && (
                    <p className="text-xs text-red-500 mt-1">{t('Invalid JSON format')}</p>
                  )}
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
                              className={`min-w-[400px] font-mono text-xs ${tool.parameters && !validateJSON(tool.parameters)
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
            <Button onClick={handleSubmit} disabled={loading || isLoadingData || fetchingTools}>
              <IconCheck size={16} className="mr-2" stroke={getIconStroke(theme)} />
              {loading ? t('Saving...') : t('Save')}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default McpModal;
