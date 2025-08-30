import { FC, useState, useEffect } from 'react';

import useTranslation from '@/hooks/useTranslation';
import { getMcpServers } from '@/apis/clientApis';
import { McpServerListItemDto, ChatSpanMcp } from '@/types/clientApis';

import { IconPlus, IconTrash } from '../Icons';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select';

interface Props {
  value: ChatSpanMcp[];
  onValueChange: (value: ChatSpanMcp[]) => void;
  validate?: () => string | null; // 返回错误信息，null表示验证通过
}

const McpSelector: FC<Props> = ({
  value = [],
  onValueChange,
}) => {
  const { t } = useTranslation();
  const [mcpServers, setMcpServers] = useState<McpServerListItemDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  // 验证MCP配置
  const validateMcps = (): string | null => {
    for (let i = 0; i < value.length; i++) {
      const mcp = value[i];
      
      // 检查是否选择了工具
      if (!mcp.id || mcp.id === 0) {
        return t('Please select a tool for MCP entry') + ` ${i + 1}`;
      }
      
      // 检查自定义Header是否为有效JSON
      if (mcp.customHeaders && mcp.customHeaders.trim()) {
        try {
          JSON.parse(mcp.customHeaders);
        } catch (error) {
          return t('Invalid JSON format in custom headers for MCP entry') + ` ${i + 1}`;
        }
      }
    }
    return null;
  };

  // 对外暴露验证函数
  if (typeof window !== 'undefined') {
    (window as any).validateMcps = validateMcps;
  }

  // 组件初始化时加载MCP服务器列表
  useEffect(() => {
    fetchMcpServers();
  }, []);

  const fetchMcpServers = async () => {
    try {
      setIsLoading(true);
      const servers = await getMcpServers();
      setMcpServers(servers);
    } catch (error) {
      console.error('Failed to fetch MCP servers:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleAddMcp = () => {
    // 当用户点击加号时，先获取MCP服务器列表
    if (mcpServers.length === 0) {
      fetchMcpServers();
    }
    
    // 添加一个新的MCP项
    const newMcp: ChatSpanMcp = {
      id: mcpServers.length > 0 ? mcpServers[0].id : 0,
      customHeaders: '',
    };
    onValueChange([...value, newMcp]);
  };

  const handleUpdateMcp = (index: number, field: keyof ChatSpanMcp, newValue: string | number) => {
    const updatedMcps = value.map((mcp, i) => 
      i === index ? { ...mcp, [field]: newValue } : mcp
    );
    onValueChange(updatedMcps);
  };

  const handleDeleteMcp = (index: number) => {
    const updatedMcps = value.filter((_, i) => i !== index);
    onValueChange(updatedMcps);
  };

  const getServerLabel = (id: number) => {
    const server = mcpServers.find(s => s.id === id);
    return server ? server.label : `Server ${id}`;
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-between text-sm">
        <div className="flex gap-1 items-center">
          {t('MCP Tools')}
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={handleAddMcp}
          disabled={isLoading}
          className="h-6 w-6 p-0"
        >
          <IconPlus size={16} />
        </Button>
      </div>
      
      {value.length > 0 && (
        <div className="flex flex-col gap-2">
          {value.map((mcp, index) => (
            <div key={index} className="flex items-center gap-2 p-2 border rounded">
              <div className="flex-1">
                <div className="text-xs text-gray-500 mb-1">{t('Tool Name')}</div>
                <Select
                  value={mcp.id.toString()}
                  onValueChange={(newValue) => handleUpdateMcp(index, 'id', parseInt(newValue))}
                >
                  <SelectTrigger className="h-8">
                    <SelectValue placeholder={t('Select Tool')} />
                  </SelectTrigger>
                  <SelectContent>
                    {mcpServers.map((server) => (
                      <SelectItem key={server.id} value={server.id.toString()}>
                        {server.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              
              <div className="flex-1">
                <div className="text-xs text-gray-500 mb-1">{t('Custom Headers (JSON)')}</div>
                <Input
                  value={mcp.customHeaders || ''}
                  onChange={(e) => handleUpdateMcp(index, 'customHeaders', e.target.value)}
                  placeholder='{"key": "value"}'
                  className="h-8"
                />
              </div>
              
              <Button
                variant="ghost"
                size="sm"
                onClick={() => handleDeleteMcp(index)}
                className="h-6 w-6 p-0 text-red-500 hover:text-red-700"
              >
                <IconTrash size={16} />
              </Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default McpSelector;
