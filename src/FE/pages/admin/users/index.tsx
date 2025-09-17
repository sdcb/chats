import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';

import { GetUsersResult, UserModelDisplay } from '@/types/adminApis';
import { PageResult, Paging } from '@/types/page';

import PaginationContainer from '../../../components/Pagiation/Pagiation';
import { Badge } from '@/components/ui/badge';
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
import { IconChevronDown, IconChevronRight, IconLoader, IconPencil } from '@/components/Icons';

import EditUserBalanceModal from '../_components/Users/EditUserBalanceModel';
import UserModal from '../_components/Users/UserModal';
import UserModelRow from '../_components/Users/UserModelRow';
import AddUserModelButton from '../_components/Users/AddUserModelButton';

import { getUsers, getModelsByUserId } from '@/apis/adminApis';

export default function Users() {
  const { t } = useTranslation();
  const [isOpenModal, setIsOpenModal] = useState({
    edit: false,
    create: false,
    recharge: false,
  });
  const [pagination, setPagination] = useState<Paging>({
    page: 1,
    pageSize: 50,
  });
  const [selectedUser, setSelectedUser] = useState<GetUsersResult | null>(null);
  const [users, setUsers] = useState<PageResult<GetUsersResult[]>>({
    count: 0,
    rows: [],
  });

  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState<string>('');
  
  // 展开状态管理 - 只允许同时展开一个用户
  const [expandedUserId, setExpandedUserId] = useState<string | null>(null);
  const [userModels, setUserModels] = useState<Record<string, UserModelDisplay[]>>({});
  const [loadingModels, setLoadingModels] = useState<Set<string>>(new Set());

  useEffect(() => {
    init();
  }, [pagination]);

  const updateQueryWithDebounce = useDebounce((query: string) => {
    init(query);
  }, 1000);

  const init = (query: string = '') => {
    getUsers({ query, ...pagination }).then((data) => {
      setUsers(data);
      // 清除所有展开状态和模型数据
      setUserModels({});
      setExpandedUserId(null);
      handleClose();
      setLoading(false);
    });
  };

  const loadUserModels = async (userId: string) => {
    // 如果正在加载，避免重复请求
    if (loadingModels.has(userId)) return;
    
    setLoadingModels(prev => new Set(prev).add(userId));
    try {
      const models = await getModelsByUserId(userId);
      setUserModels(prev => ({ ...prev, [userId]: models }));
    } catch (error) {
      console.error('Failed to load user models:', error);
      setUserModels(prev => ({ ...prev, [userId]: [] }));
      // 重新抛出错误，让调用者能够处理
      throw error;
    } finally {
      setLoadingModels(prev => {
        const newSet = new Set(prev);
        newSet.delete(userId);
        return newSet;
      });
    }
  };

  const handleToggleExpand = async (userId: string) => {
    if (expandedUserId === userId) {
      // 如果点击的是已展开的用户，则收起
      // 同时更新两个状态，避免中间状态导致的两阶段关闭
      setExpandedUserId(null);
      setUserModels(prev => {
        const newModels = { ...prev };
        delete newModels[userId];
        return newModels;
      });
    } else {
      // 展开新用户前，先清除之前展开用户的数据
      if (expandedUserId) {
        setUserModels(prev => {
          const newModels = { ...prev };
          delete newModels[expandedUserId];
          return newModels;
        });
      }
      
      try {
        // 每次展开都重新加载数据，不使用缓存
        await loadUserModels(userId);
        // 请求成功后才展开
        setExpandedUserId(userId);
      } catch (error) {
        console.error('Failed to load user models:', error);
        toast.error(t('Failed to load user models'));
        // 请求失败时不展开，loadingModels 状态已在 loadUserModels 中清理
      }
    }
  };

  const handleUserModelsUpdate = async (userId: string) => {
    // 重新加载该用户的模型数据，不依赖缓存
    await loadUserModels(userId);
  };

  const handleShowAddModal = () => {
    setIsOpenModal({
      edit: false,
      create: true,
      recharge: false,
    });
  };

  const handleShowEditModal = (user: GetUsersResult) => {
    setSelectedUser(user);
    setIsOpenModal({
      edit: true,
      create: false,
      recharge: false,
    });
  };

  const handleShowReChargeModal = (user: GetUsersResult) => {
    setSelectedUser(user);
    setIsOpenModal({
      edit: false,
      create: false,
      recharge: true,
    });
  };

  const handleClose = () => {
    setIsOpenModal({
      edit: false,
      create: false,
      recharge: false,
    });
    setSelectedUser(null);
  };

  return (
    <>
      <div className="flex flex-warp gap-4 mb-4">
        <Input
          className="max-w-[238px] w-full"
          placeholder={t('Search...')!}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            updateQueryWithDebounce(e.target.value);
          }}
        />
        <Button onClick={() => handleShowAddModal()} color="primary">
          {t('Add User')}
        </Button>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('User Name')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('Account')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('Role')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('Phone')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('E-Mail')}</TableHead>
              <TableHead>
                {t('Balance')}({t('Yuan')})
              </TableHead>
              <TableHead>{t('Model Count')}</TableHead>
              <TableHead>{t('Actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={users.rows.length === 0}>
            {users.rows.map((item) => (
              <React.Fragment key={item.id}>
                <TableRow className="cursor-pointer">
                  <TableCell>
                    <div className="flex gap-1 items-center">
                      <div
                        className={`w-2 h-2 rounded-full ${
                          item.enabled ? 'bg-green-400' : 'bg-gray-400'
                        }`}
                      ></div>
                      {item.username}
                      {item.provider && (
                        <Badge className="capitalize">{item.provider}</Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden sm:table-cell">{item.account}</TableCell>
                  <TableCell className="hidden sm:table-cell">{item.role}</TableCell>
                  <TableCell className="hidden sm:table-cell">{item.phone}</TableCell>
                  <TableCell className="hidden sm:table-cell">{item.email}</TableCell>
                  <TableCell
                    className="hover:underline cursor-pointer"
                    onClick={(e) => {
                      handleShowReChargeModal(item);
                      e.stopPropagation();
                    }}
                  >
                    {toFixed(+item.balance)}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Button
                        type="button"
                        variant="link"
                        className="px-0"
                        title={t('Click to manage models')}
                        onClick={(e) => {
                          handleToggleExpand(item.id);
                          e.stopPropagation();
                        }}
                      >
                        {loadingModels.has(item.id) ? (
                          <IconLoader className="animate-spin" size={16} />
                        ) : expandedUserId === item.id ? (
                          <IconChevronDown size={16} />
                        ) : (
                          <IconChevronRight size={16} />
                        )}
                        {item.userModelCount}
                      </Button>
                    </div>
                  </TableCell>
                  <TableCell>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="h-8 w-8 p-0"
                      title={t('Edit User')}
                      onClick={(e) => {
                        handleShowEditModal(item);
                        e.stopPropagation();
                      }}
                    >
                      <IconPencil size={16} />
                    </Button>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell colSpan={4} className="p-0 sm:hidden">
                    <div 
                      className={`overflow-hidden transition-all duration-300 ease-in-out ${
                        expandedUserId === item.id 
                          ? 'max-h-screen opacity-100' 
                          : 'max-h-0 opacity-0'
                      }`}
                    >
                      <div className={`bg-muted/30 transition-all duration-300 ease-in-out ${
                        expandedUserId === item.id ? 'p-4' : 'p-0'
                      }`}>
                        {(expandedUserId === item.id || loadingModels.has(item.id)) && (
                          <>
                            {loadingModels.has(item.id) ? (
                              <div className="text-center py-4">
                                {t('Loading...')}
                              </div>
                            ) : userModels[item.id] && userModels[item.id].length > 0 ? (
                              <Table>
                                <TableHeader>
                                  <TableRow>
                                    <TableHead>{t('Model')}</TableHead>
                                    <TableHead className="hidden sm:table-cell">{t('Model Key')}</TableHead>
                                    <TableHead>{t('Tokens')}</TableHead>
                                    <TableHead>{t('Counts')}</TableHead>
                                    <TableHead>{t('Expires')}</TableHead>
                                    <TableHead>
                                      <div className="flex items-center justify-end">
                                        <AddUserModelButton
                                          userId={item.id}
                                          onUpdate={() => handleUserModelsUpdate(item.id)}
                                        />
                                      </div>
                                    </TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {userModels[item.id].map((userModel) => (
                                    <UserModelRow
                                      key={userModel.id}
                                      userModel={userModel}
                                      userId={item.id}
                                      onUpdate={() => handleUserModelsUpdate(item.id)}
                                    />
                                  ))}
                                </TableBody>
                              </Table>
                            ) : (
                              <div>
                                <div className="text-center py-4 text-muted-foreground">
                                  {t('No models assigned')}
                                </div>
                                <div className="flex justify-center">
                                  <AddUserModelButton
                                    userId={item.id}
                                    onUpdate={() => handleUserModelsUpdate(item.id)}
                                  />
                                </div>
                              </div>
                            )}
                          </>
                        )}
                      </div>
                    </div>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell colSpan={8} className="p-0 hidden sm:table-cell">
                    <div 
                      className={`overflow-hidden transition-all duration-300 ease-in-out ${
                        expandedUserId === item.id 
                          ? 'max-h-screen opacity-100' 
                          : 'max-h-0 opacity-0'
                      }`}
                    >
                      <div className={`bg-muted/30 transition-all duration-300 ease-in-out ${
                        expandedUserId === item.id ? 'p-4' : 'p-0'
                      }`}>
                        {(expandedUserId === item.id || loadingModels.has(item.id)) && (
                          <>
                            {loadingModels.has(item.id) ? (
                              <div className="text-center py-4">
                                {t('Loading...')}
                              </div>
                            ) : userModels[item.id] && userModels[item.id].length > 0 ? (
                              <Table>
                                <TableHeader>
                                  <TableRow>
                                    <TableHead>{t('Model')}</TableHead>
                                    <TableHead className="hidden sm:table-cell">{t('Model Key')}</TableHead>
                                    <TableHead>{t('Tokens')}</TableHead>
                                    <TableHead>{t('Counts')}</TableHead>
                                    <TableHead>{t('Expires')}</TableHead>
                                    <TableHead>
                                      <div className="flex items-center justify-end">
                                        <AddUserModelButton
                                          userId={item.id}
                                          onUpdate={() => handleUserModelsUpdate(item.id)}
                                        />
                                      </div>
                                    </TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {userModels[item.id].map((userModel) => (
                                    <UserModelRow
                                      key={userModel.id}
                                      userModel={userModel}
                                      userId={item.id}
                                      onUpdate={() => handleUserModelsUpdate(item.id)}
                                    />
                                  ))}
                                </TableBody>
                              </Table>
                            ) : (
                              <div>
                                <div className="text-center py-4 text-muted-foreground">
                                  {t('No models assigned')}
                                </div>
                                <div className="flex justify-center">
                                  <AddUserModelButton
                                    userId={item.id}
                                    onUpdate={() => handleUserModelsUpdate(item.id)}
                                  />
                                </div>
                              </div>
                            )}
                          </>
                        )}
                      </div>
                    </div>
                  </TableCell>
                  </TableRow>
              </React.Fragment>
            ))}
          </TableBody>
        </Table>
        {users.count !== 0 && (
          <PaginationContainer
            page={pagination.page}
            pageSize={pagination.pageSize}
            currentCount={users.rows.length}
            totalCount={users.count}
            onPagingChange={(page, pageSize) => {
              setPagination({ page, pageSize });
            }}
          />
        )}
      </Card>
      <UserModal
        user={selectedUser}
        onSuccessful={init}
        onClose={handleClose}
        isOpen={isOpenModal.create || isOpenModal.edit}
      />
      <EditUserBalanceModal
        onSuccessful={init}
        onClose={handleClose}
        userId={selectedUser?.id}
        userBalance={selectedUser?.balance}
        isOpen={isOpenModal.recharge}
      />
    </>
  );
}
