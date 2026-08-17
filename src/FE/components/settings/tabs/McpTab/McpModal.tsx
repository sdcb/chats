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
import { Switch } from '@/components/ui/switch';

import { fetchMcpTools } from '@/apis/clientApis';
import { isEmptyOrJsonObject } from '@/utils/json';
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
    name: '',
    displayName: '',
    url: '',
    headers: '',
    serverInstructions: '',
  });
  const [tools, setTools] = useState<McpToolBasicInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [fetchingTools, setFetchingTools] = useState(false);
  const [nameError, setNameError] = useState<string | null>(null);
  const user = getUserInfo();
  const isAdmin = user?.role === 'admin';

  // 前端校验：允许空白或JSON对象
  const validateJSON = (jsonString: string): boolean => isEmptyOrJsonObject(jsonString);

  useEffect(() => {
    if (server && !isCreateMode) {
      setFormData({
        name: server.name,
        displayName: server.displayName || '',
        url: server.url,
        headers: server.headers || '',
        serverInstructions: server.serverInstructions || '',
      });
      setTools(server.tools);
    } else {
      setFormData({
        name: '',
        displayName: '',
        url: '',
        headers: '',
        serverInstructions: '',
      });
      setTools([]);
    }
  }, [server, isCreateMode]);

  const handleInputChange = (field: string, value: any) => {
    setFormData(prev => ({
      ...prev,
      [field]: value,
    }));

    if (field === 'name') {
      setNameError(
        value && !/^[A-Za-z0-9_-]{1,50}$/.test(value)
          ? t('Name must contain only letters, numbers, underscores, or hyphens (max 50)')
          : null,
      );
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
      const response = await fetchMcpTools({
        serverUrl: formData.url,
        headers: formData.headers || undefined,
      });

      const newTools: McpToolBasicInfo[] = (response.tools || []).map(tool => ({
        ...tool,
      }));

      setTools(newTools);
      if (response.serverInstructions !== undefined && response.serverInstructions !== null) {
        setFormData(prev => ({
          ...prev,
          serverInstructions: response.serverInstructions || '',
        }));
      }
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
        title: '',
        description: '',
        parameters: '',
        destructive: false,
        idempotent: false,
        openWorld: false,
        readOnly: false,
      }
    ]);
  };

  const handleRemoveTool = (index: number) => {
    setTools(prev => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
    const serverName = formData.name.trim();
    if (!/^[A-Za-z0-9_-]{1,50}$/.test(serverName)) {
      const message = t('Name must contain only letters, numbers, underscores, or hyphens (max 50)');
      toast.error(message);
      setNameError(message);
      return;
    }

    if (!formData.url.trim()) {
      toast.error(t('Please enter a URL'));
      return;
    }

    // 验证 headers：必须为空白或合法 JSON 对象
    if (formData.headers && !validateJSON(formData.headers)) {
      toast.error(t('Headers must be empty or a valid JSON object'));
      return;
    }

    // If in create mode and tools are empty, auto-fetch tools first, then proceed to save regardless of result
    let toolsToSave = tools;
    if (isCreateMode && tools.length === 0) {
      const fetchedTools = await fetchToolsInternal(true); // silent fetch, shows fetching state on button
      if (fetchedTools) {
        toolsToSave = fetchedTools;
      }
    }

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

        const protocolName = `mcp__${serverName}__${tool.name}`;
        if (!/^[A-Za-z0-9_-]{1,64}$/.test(protocolName)) {
          toast.error(
            t('Tool {{name}} produces an invalid or too-long model tool name', {
              name: tool.name,
            }),
          );
          return;
        }
      }

    const toolNames = toolsToSave.map(t => t.name);
    const uniqueNames = new Set(toolNames);
    if (toolNames.length !== uniqueNames.size) {
      toast.error(t('Tool names must be unique'));
      return;
    }

    setLoading(true);
    try {
      await onSave({
        name: serverName,
        displayName: formData.displayName.trim() || undefined,
        url: formData.url.trim(),
        headers: formData.headers.trim() || undefined,
        serverInstructions: formData.serverInstructions.trim() || undefined,
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
                  <Label htmlFor="name">{t('Name')}</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => handleInputChange('name', e.target.value)}
                    placeholder="my_server"
                    disabled={isReadOnly}
                    className={nameError ? 'border-red-500 focus:border-red-500' : ''}
                  />
                  {nameError && (
                    <p className="text-xs text-red-500 mt-1">{nameError}</p>
                  )}
                  {!isCreateMode && !isReadOnly && (
                    <p className="text-xs text-amber-600 mt-1">
                      {t('Changing Name changes every model-facing MCP tool name.')}
                    </p>
                  )}
                </div>

                <div>
                  <Label htmlFor="displayName">{t('Display Name')}</Label>
                  <Input
                    id="displayName"
                    value={formData.displayName}
                    onChange={(e) => handleInputChange('displayName', e.target.value)}
                    placeholder={t('Optional name shown to users')}
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
                    className={`${formData.headers && !validateJSON(formData.headers)
                        ? 'border-red-500 focus:border-red-500'
                        : ''
                      }`}
                  />
                  {formData.headers && !validateJSON(formData.headers) && (
                    <p className="text-xs text-red-500 mt-1">{t('Headers must be empty or a valid JSON object')}</p>
                  )}
                </div>

                <div>
                  <Label htmlFor="serverInstructions">{t('Server Instructions')}</Label>
                  <Textarea
                    id="serverInstructions"
                    value={formData.serverInstructions}
                    onChange={(e) => handleInputChange('serverInstructions', e.target.value)}
                    placeholder={t('Optional MCP server instructions for the model')}
                    rows={4}
                    disabled={isReadOnly}
                  />
                  <p className="text-xs text-muted-foreground mt-1">
                    {t('Fetched automatically when available. You can edit before saving.')}
                  </p>
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
                  <Table className="table-compact min-w-[840px]">
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-36 px-1.5">{t('Name')}</TableHead>
                        <TableHead className="w-40 px-1.5">{t('Title')}</TableHead>
                        <TableHead className="w-52 px-1.5">{t('Description')}</TableHead>
                        <TableHead className="w-72 px-1.5">{t('Parameters')}</TableHead>
                        <TableHead className="w-20 px-1.5 text-center">{t('Read Only')}</TableHead>
                        <TableHead className="w-20 px-1.5 text-center">{t('Idempotent')}</TableHead>
                        <TableHead className="w-14 px-1.5">{t('Actions')}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {tools.map((tool, index) => (
                        <TableRow key={index}>
                          <TableCell className="px-1.5 py-1.5">
                            <Input
                              value={tool.name}
                              onChange={(e) => handleToolChange(index, 'name', e.target.value)}
                              placeholder={t('Tool name')}
                              className="h-8 w-32"
                              disabled={isReadOnly}
                            />
                          </TableCell>
                          <TableCell className="px-1.5 py-1.5">
                            <Input
                              value={tool.title || ''}
                              onChange={(e) => handleToolChange(index, 'title', e.target.value)}
                              placeholder={t('Display title')}
                              className="h-8 w-36"
                              disabled={isReadOnly}
                            />
                          </TableCell>
                          <TableCell className="px-1.5 py-1.5">
                            <Textarea
                              value={tool.description || ''}
                              onChange={(e) => handleToolChange(index, 'description', e.target.value)}
                              placeholder={t('Tool description')}
                              rows={1}
                              wrap="off"
                              className="h-8 min-h-8 w-48 resize-none overflow-auto whitespace-pre py-1.5"
                              disabled={isReadOnly}
                            />
                          </TableCell>
                          <TableCell className="px-1.5 py-1.5">
                            <Textarea
                              value={tool.parameters || ''}
                              onChange={(e) => handleToolChange(index, 'parameters', e.target.value)}
                              placeholder='{"type": "object", "properties": {...}}'
                              rows={1}
                              wrap="off"
                              className={`h-8 min-h-8 w-64 resize-none overflow-auto whitespace-pre py-1.5 text-sm font-mono ${tool.parameters && !validateJSON(tool.parameters)
                                  ? 'border-red-500 focus:border-red-500'
                                  : ''
                                }`}
                              disabled={isReadOnly}
                            />
                            {tool.parameters && !validateJSON(tool.parameters) && (
                              <p className="text-sm text-red-500 mt-1">{t('Invalid JSON format')}</p>
                            )}
                          </TableCell>
                          <TableCell className="px-1.5 py-1.5 text-center">
                            <Switch
                              checked={tool.readOnly}
                              onCheckedChange={(checked) => handleToolChange(index, 'readOnly', checked)}
                              disabled={isReadOnly}
                              aria-label={t('Read Only')}
                            />
                          </TableCell>
                          <TableCell className="px-1.5 py-1.5 text-center">
                            <Switch
                              checked={tool.idempotent}
                              onCheckedChange={(checked) => handleToolChange(index, 'idempotent', checked)}
                              disabled={isReadOnly}
                              aria-label={t('Idempotent')}
                            />
                          </TableCell>
                          <TableCell className="px-1.5 py-1.5">
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
              <IconCheck size={16} className="mr-2 stroke-primary-foreground" />
              {loading ? t('Saving...') : t('Save')}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default McpModal;
