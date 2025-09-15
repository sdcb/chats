import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { getIconStroke } from '@/utils/common';

import { McpServerDetailsDto, McpServerListManagementItemDto } from '@/types/clientApis';

import DeletePopover from '@/pages/home/_components/Popover/DeletePopover';

import {
  IconPlus,
  IconSearch,
  IconEdit,
  IconRefresh,
  IconEye,
} from '@/components/Icons';
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
import { Badge } from '@/components/ui/badge';

import McpModal from './McpTab/McpModal';

import {
  getMcpServersForManagement,
  getMcpServerDetails,
  createMcpServer,
  updateMcpServer,
  deleteMcpServer,
} from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

const McpTab = () => {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [mcpServers, setMcpServers] = useState<McpServerListManagementItemDto[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [filteredServers, setFilteredServers] = useState<McpServerListManagementItemDto[]>([]);
  const [selectedServer, setSelectedServer] = useState<McpServerDetailsDto | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [isCreateMode, setIsCreateMode] = useState(false);
  const [isReadOnly, setIsReadOnly] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadingServerDetails, setLoadingServerDetails] = useState(false);
  const user = useUserInfo();
  const isAdmin = user?.role === 'admin';

  useEffect(() => {
    setLoading(true);
    fetchMcpServers();
  }, []);

  useEffect(() => {
    const filtered = mcpServers.filter(
      (server) =>
        server.label.toLowerCase().includes(searchTerm.toLowerCase()) ||
        server.url.toLowerCase().includes(searchTerm.toLowerCase())
    );
    setFilteredServers(filtered);
  }, [mcpServers, searchTerm]);

  const fetchMcpServers = async () => {
    try {
      const data = await getMcpServersForManagement();
      setMcpServers(data);
    } catch (error) {
      console.error('Failed to fetch MCP servers:', error);
      toast.error(t('Failed to fetch MCP servers'));
    } finally {
      setLoading(false);
    }
  };

  const handleCreateServer = () => {
    setSelectedServer(null);
    setIsCreateMode(true);
    setIsReadOnly(false);
    setShowModal(true);
  };

  const handleEditServer = async (serverId: number) => {
    setSelectedServer(null); // 先清空数据
    setIsCreateMode(false);
    setIsReadOnly(false);
    setShowModal(true); // 立即显示Modal

    setLoadingServerDetails(true);
    try {
      const serverDetails = await getMcpServerDetails(serverId);
      setSelectedServer(serverDetails);
    } catch (error) {
      console.error('Failed to fetch server details:', error);
      toast.error(t('Failed to fetch server details'));
      setShowModal(false); // 加载失败时关闭Modal
    } finally {
      setLoadingServerDetails(false);
    }
  };

  const handleViewServer = async (serverId: number) => {
    setSelectedServer(null); // 先清空数据
    setIsCreateMode(false);
    setIsReadOnly(true);
    setShowModal(true); // 立即显示Modal

    setLoadingServerDetails(true);
    try {
      const serverDetails = await getMcpServerDetails(serverId);
      setSelectedServer(serverDetails);
    } catch (error) {
      console.error('Failed to fetch server details:', error);
      toast.error(t('Failed to fetch server details'));
      setShowModal(false); // 加载失败时关闭Modal
    } finally {
      setLoadingServerDetails(false);
    }
  };

  const handleDeleteServer = async (serverId: number) => {
    try {
      await deleteMcpServer(serverId);
      toast.success(t('MCP server deleted successfully'));
      fetchMcpServers();
    } catch (error) {
      console.error('Failed to delete MCP server:', error);
      toast.error(t('Failed to delete MCP server'));
      throw error; // 重新抛出错误，让 DeletePopover 知道删除失败
    }
  };

  const handleSaveServer = async (serverData: any) => {
    try {
      if (isCreateMode) {
        await createMcpServer(serverData);
        toast.success(t('MCP server created successfully'));
      } else if (selectedServer) {
        await updateMcpServer(selectedServer.id, serverData);
        toast.success(t('MCP server updated successfully'));
      }
      setShowModal(false);
      fetchMcpServers();
    } catch (error) {
      console.error('Failed to save MCP server:', error);
      toast.error(t('Failed to save MCP server'));
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">{t('MCP Management')}</h2>
        <div className="flex items-center gap-2">
          <Button onClick={fetchMcpServers} variant="outline" size="sm">
            <IconRefresh size={16} className="mr-2" />
            {t('Refresh')}
          </Button>
          <Button onClick={handleCreateServer}>
            <IconPlus
              size={16}
              className="mr-2"
              stroke={getIconStroke(theme)}
            />
            {t('Add MCP Server')}
          </Button>
        </div>
      </div>

      <Card className="p-4">
        <div className="flex items-center space-x-2 mb-4">
          <IconSearch size={16} />
          <Input
            placeholder={t('Search MCP servers...')}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="flex-1"
          />
        </div>

        {loading ? (
          <div className="text-center py-8">
            <p>{t('Loading...')}</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('Label')}</TableHead>
                  <TableHead>{t('URL')}</TableHead>
                  {isAdmin && <TableHead>{t('Status')}</TableHead>}
                  <TableHead>{t('Tool Count')}</TableHead>
                  {isAdmin && <TableHead>{t('Owner')}</TableHead>}
                  <TableHead>{t('Created')}</TableHead>
                  <TableHead>{t('Updated')}</TableHead>
                  <TableHead>{t('Actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredServers.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8} className="text-center py-8">
                      {searchTerm ? t('No MCP servers found') : t('No MCP servers yet')}
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredServers.map((server) => (
                    <TableRow key={server.id}>
                      <TableCell className="font-medium">{server.label}</TableCell>
                      <TableCell className="max-w-xs truncate" title={server.url}>
                        {server.url}
                      </TableCell>
                      {isAdmin && (
                        <TableCell>
                          <Badge variant={server.isSystem ? 'default' : 'secondary'}>
                            {server.isSystem ? t('System') : t('User')}
                          </Badge>
                        </TableCell>
                      )}
                      <TableCell>
                        <Badge variant="outline">{server.toolsCount}</Badge>
                      </TableCell>
                      {isAdmin && <TableCell>{server.owner || t('System')}</TableCell>}
                      <TableCell>{formatDate(server.createdAt)}</TableCell>
                      <TableCell>
                        {formatDate(server.updatedAt)}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          {server.editable ? (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleEditServer(server.id)}
                              title={t('Edit')}
                            >
                              <IconEdit size={16} />
                            </Button>
                          ) : (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleViewServer(server.id)}
                              title={t('View')}
                            >
                              <IconEye size={16} />
                            </Button>
                          )}
                          {server.editable && (
                            <DeletePopover
                              onDelete={() => handleDeleteServer(server.id)}
                            />
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </Card>

      {showModal && (
        <McpModal
          isOpen={showModal}
          onClose={() => setShowModal(false)}
          onSave={handleSaveServer}
          server={selectedServer}
          isCreateMode={isCreateMode}
          isReadOnly={isReadOnly}
          isLoadingData={loadingServerDetails}
        />
      )}
    </div>
  );
};

export default McpTab;
