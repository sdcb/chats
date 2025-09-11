import React, { useEffect, useState } from 'react';

import useDebounce from '@/hooks/useDebounce';
import { useThrottle } from '@/hooks/useThrottle';
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
import { IconPlus, IconChevronDown, IconChevronRight } from '@/components/Icons';

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
  
  // 展开状态管理
  const [expandedUserIds, setExpandedUserIds] = useState<Set<string>>(new Set());
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
      handleClose();
      setLoading(false);
    });
  };

  const loadUserModels = async (userId: string) => {
    if (userModels[userId]) return; // 已经加载过了
    
    setLoadingModels(prev => new Set(prev).add(userId));
    try {
      const models = await getModelsByUserId(userId);
      setUserModels(prev => ({ ...prev, [userId]: models }));
    } catch (error) {
      console.error('Failed to load user models:', error);
    } finally {
      setLoadingModels(prev => {
        const newSet = new Set(prev);
        newSet.delete(userId);
        return newSet;
      });
    }
  };

  const handleToggleExpand = async (userId: string) => {
    const newExpanded = new Set(expandedUserIds);
    if (newExpanded.has(userId)) {
      newExpanded.delete(userId);
    } else {
      newExpanded.add(userId);
      await loadUserModels(userId);
    }
    setExpandedUserIds(newExpanded);
  };

  const handleUserModelsUpdate = (userId: string) => {
    // 重新加载该用户的模型数据
    delete userModels[userId];
    loadUserModels(userId);
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
              <TableHead>{t('Account')}</TableHead>
              <TableHead>{t('Role')}</TableHead>
              <TableHead>{t('Phone')}</TableHead>
              <TableHead>{t('E-Mail')}</TableHead>
              <TableHead>
                {t('Balance')}({t('Yuan')})
              </TableHead>
              <TableHead>{t('Model Count')}</TableHead>
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
                  <TableCell>{item.account}</TableCell>
                  <TableCell>{item.role}</TableCell>
                  <TableCell>{item.phone}</TableCell>
                  <TableCell
                    className="hover:underline cursor-pointer"
                    onClick={(e) => {
                      handleShowEditModal(item);
                      e.stopPropagation();
                    }}
                  >
                    {item.email}
                  </TableCell>
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
                        {expandedUserIds.has(item.id) ? (
                          <IconChevronDown size={16} />
                        ) : (
                          <IconChevronRight size={16} />
                        )}
                        {item.userModelCount}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell colSpan={7} className="p-0">
                    <div 
                      className={`overflow-hidden transition-all duration-300 ease-in-out ${
                        expandedUserIds.has(item.id) 
                          ? 'max-h-screen opacity-100' 
                          : 'max-h-0 opacity-0'
                      }`}
                    >
                      <div className="bg-muted/30 p-4">
                        {loadingModels.has(item.id) ? (
                            <div className="text-center py-4">
                              {t('Loading...')}
                            </div>
                          ) : userModels[item.id] && userModels[item.id].length > 0 ? (
                            <Table>
                              <TableHeader>
                                <TableRow>
                                  <TableHead>{t('Model Name')}</TableHead>
                                  <TableHead>{t('Model Key')}</TableHead>
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
