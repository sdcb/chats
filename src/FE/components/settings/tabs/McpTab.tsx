import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
// import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { 
  McpServerDetailsDto, 
  McpServerListManagementItemDto,
  AssignedUserNameDto 
} from '@/types/clientApis';

import DeletePopover from '@/components/Popover/DeletePopover';

import {
  IconPlus,
  IconSearch,
  IconEdit,
  IconRefresh,
  IconEye,
  IconUserPlus,
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
import Tips from '@/components/Tips/Tips';
// Tooltips are handled via <Tips /> component
// Use Radix Tooltip primitives locally to avoid changing shared Tips/tooltip components
import * as TooltipPrimitive from '@radix-ui/react-tooltip';

import McpModal from './McpTab/McpModal';
import AssignUsersModal from './McpTab/AssignUsersModal';

import {
  getMcpServersForManagement,
  getMcpServerDetails,
  createMcpServer,
  updateMcpServer,
  deleteMcpServer,
  getAssignedUserNames,
} from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

const AssignedUsersTooltip = ({ mcpId, assignedUserCount, editable }: { mcpId: number; assignedUserCount: number; editable: boolean }) => {
  const { t } = useTranslation();
  const [assignedUsers, setAssignedUsers] = useState<AssignedUserNameDto[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(false);

  const loadAssignedUsers = async () => {
    if (assignedUserCount === 0) return;
    if (!editable) return; // 如果不可编辑，则说明没有权限查看分配的用户
    
    setLoadingUsers(true);
    try {
      const users = await getAssignedUserNames(mcpId);
      setAssignedUsers(users);
    } catch (error) {
      console.error('Failed to fetch assigned users:', error);
    } finally {
      setLoadingUsers(false);
    }
  };

  if (assignedUserCount === 0) {
    return <Badge variant="outline">0</Badge>;
  }

  return (
    <TooltipPrimitive.Provider delayDuration={50}>
      <TooltipPrimitive.Root>
        <TooltipPrimitive.Trigger asChild>
          <span onMouseEnter={loadAssignedUsers} className="inline-block">
            <Badge
              variant="outline"
              className="cursor-pointer hover:bg-muted"
            >
              {assignedUserCount}
            </Badge>
          </span>
        </TooltipPrimitive.Trigger>
        <TooltipPrimitive.Portal>
          <TooltipPrimitive.Content
            side="bottom"
            sideOffset={4}
            className="z-50 overflow-hidden rounded-md border bg-popover px-3 py-1.5 text-sm text-popover-foreground shadow-md animate-in fade-in-0 zoom-in-95 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2"
          >
            {loadingUsers ? (
              <p>{t('Loading...')}</p>
            ) : assignedUsers.length > 0 ? (
              <div className="max-w-sm">
                <p className="font-medium mb-1">{t('Assigned Users')}</p>
                <p className="text-sm whitespace-normal break-words">
                  {assignedUsers.map((user) => user.userName).join(', ')}
                </p>
              </div>
            ) : (
              <p>{t('No assigned users')}</p>
            )}
          </TooltipPrimitive.Content>
        </TooltipPrimitive.Portal>
      </TooltipPrimitive.Root>
    </TooltipPrimitive.Provider>
  );
};

const McpTab = () => {
  const { t } = useTranslation();
  // const { theme } = useTheme();
  const [mcpServers, setMcpServers] = useState<McpServerListManagementItemDto[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [filteredServers, setFilteredServers] = useState<McpServerListManagementItemDto[]>([]);
  const [selectedServer, setSelectedServer] = useState<McpServerDetailsDto | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [isCreateMode, setIsCreateMode] = useState(false);
  const [isReadOnly, setIsReadOnly] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadingServerDetails, setLoadingServerDetails] = useState(false);
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [assignMcpId, setAssignMcpId] = useState<number | null>(null);
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

  const handleAssignUsers = (serverId: number) => {
    setAssignMcpId(serverId);
    setShowAssignModal(true);
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

  // 渲染编辑和分配用户的组合按钮
  const renderEditAssignComboButton = (server: McpServerListManagementItemDto) => {
    if (!server.editable) return null;
    
    return (
      <div className="flex">
        {/* 左侧编辑按钮 */}
        <Tips
          trigger={
            <Button
              variant="ghost"
              size="sm"
              className="p-1 m-0 h-8 w-8 rounded-r-none border-r border-r-border/50 hover:bg-accent hover:border-r-accent-foreground/20"
              onClick={() => handleEditServer(server.id)}
            >
              <IconEdit size={16} />
            </Button>
          }
          side="bottom"
          content={t('Edit')!}
        />

        {/* 右侧分配用户按钮 */}
        <Tips
          trigger={
            <Button
              variant="ghost"
              size="sm"
              className="p-1 m-0 h-8 w-8 rounded-l-none hover:bg-accent"
              onClick={() => handleAssignUsers(server.id)}
            >
              <IconUserPlus size={16} />
            </Button>
          }
          side="bottom"
          content={t('Assign Users')!}
        />
      </div>
    );
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
              className="mr-2 stroke-primary-foreground"
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
                  <TableHead>{t('Tool Count')}</TableHead>
                  <TableHead>{t('Assigned Users')}</TableHead>
                  {isAdmin && <TableHead>{t('Owner')}</TableHead>}
                  <TableHead className="hidden md:table-cell">{t('Created')}</TableHead>
                  <TableHead className="hidden md:table-cell">{t('Updated')}</TableHead>
                  <TableHead>{t('Actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredServers.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={isAdmin ? 8 : 7} className="text-center py-8">
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
                      <TableCell>
                        <Badge variant="outline">{server.toolsCount}</Badge>
                      </TableCell>
                      <TableCell>
                        <AssignedUsersTooltip 
                          mcpId={server.id} 
                          assignedUserCount={server.assignedUserCount}
                          editable={server.editable}
                        />
                      </TableCell>
                      {isAdmin && <TableCell>{server.owner || t('System')}</TableCell>}
                      <TableCell className="hidden md:table-cell">{formatDate(server.createdAt)}</TableCell>
                      <TableCell className="hidden md:table-cell">
                        {formatDate(server.updatedAt)}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          {/* 编辑和分配用户组合按钮 */}
                          {renderEditAssignComboButton(server)}
                          
                          {/* 查看按钮（非editable时显示） */}
                          {!server.editable && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleViewServer(server.id)}
                              title={t('View')}
                            >
                              <IconEye size={16} />
                            </Button>
                          )}
                          
                          {/* 删除按钮 */}
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

      <AssignUsersModal
        isOpen={showAssignModal}
        onClose={() => setShowAssignModal(false)}
        mcpId={assignMcpId}
        onSuccess={fetchMcpServers}
        isAdmin={isAdmin}
      />
      </div>
  );
};

export default McpTab;
