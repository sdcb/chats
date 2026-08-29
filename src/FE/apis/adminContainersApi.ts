import { createFetchClient } from '@/hooks/createFetchClient';

import { PageResult } from '@/types/page';

import {
  AdminContainerResource,
  ContainerResourceFilters,
  ImageEntry,
  ImageFilters,
  Quota,
  QuotaFilters,
  RuntimeNode,
  RuntimeNodeFilters,
  RuntimeTemplate,
  TemplateFilters,
} from '@/components/admin/containers/types';

export type AdminContainerResourceQuery = ContainerResourceFilters & {
  page: number;
  pageSize: number;
};

export const getAdminContainerResources = (
  params: AdminContainerResourceQuery,
): Promise<PageResult<AdminContainerResource[]>> =>
  createFetchClient().get('/api/admin/container-catalog/resources', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      id: params.id || undefined,
      query: params.query || undefined,
      owner: params.owner || undefined,
      runtimeNodeId: params.runtimeNodeId || undefined,
      status: params.status || undefined,
      permanent: params.permanent || undefined,
    },
  });

export const ADMIN_CONTAINER_RESOURCES_EXPORT_URL =
  '/api/admin/container-catalog/resources/export';

const nonEmptyParams = (params: Record<string, string | undefined>) =>
  Object.fromEntries(
    Object.entries(params).filter(
      ([, value]) => value !== undefined && value !== '',
    ),
  );

export const getAdminRuntimeNodes = (
  filters: RuntimeNodeFilters,
): Promise<RuntimeNode[]> =>
  createFetchClient().get<RuntimeNode[]>(
    '/api/admin/container-catalog/runtime-nodes',
    {
      params: nonEmptyParams({
        query: filters.query || undefined,
        backendType: filters.backendType || undefined,
        enabled: filters.enabled || undefined,
      }),
    },
  );

export const getAdminTemplates = (
  filters: TemplateFilters,
): Promise<RuntimeTemplate[]> =>
  createFetchClient().get<RuntimeTemplate[]>(
    '/api/admin/container-catalog/templates',
    {
      params: nonEmptyParams({
        query: filters.query || undefined,
        runtimeNodeId: filters.runtimeNodeId || undefined,
        visibility: filters.visibility || undefined,
      }),
    },
  );

export const getAdminImages = (filters: ImageFilters): Promise<ImageEntry[]> =>
  createFetchClient().get<ImageEntry[]>('/api/admin/container-catalog/images', {
    params: nonEmptyParams({
      query: filters.query || undefined,
      enabled: filters.enabled || undefined,
    }),
  });

export const getAdminQuotas = (filters: QuotaFilters): Promise<Quota[]> =>
  createFetchClient().get<Quota[]>('/api/admin/container-catalog/quotas', {
    params: nonEmptyParams({
      query: filters.query || undefined,
      allowCustomImage: filters.allowCustomImage || undefined,
      scope: filters.scope || undefined,
    }),
  });

export type QuotaUserOption = {
  id: number;
  userName: string;
  displayName: string | null;
  hasQuota: boolean;
};

export const searchQuotaUsers = (
  query: string,
  page = 1,
  pageSize = 20,
): Promise<PageResult<QuotaUserOption[]>> =>
  createFetchClient().get<PageResult<QuotaUserOption[]>>(
    '/api/admin/container-catalog/quota-users',
    {
      params: {
        query: query || undefined,
        page,
        pageSize,
      },
    },
  );

export const ADMIN_RUNTIME_NODES_EXPORT_URL =
  '/api/admin/container-catalog/runtime-nodes/export';
export const ADMIN_TEMPLATES_EXPORT_URL =
  '/api/admin/container-catalog/templates/export';
export const ADMIN_IMAGES_EXPORT_URL =
  '/api/admin/container-catalog/images/export';
export const ADMIN_QUOTAS_EXPORT_URL =
  '/api/admin/container-catalog/quotas/export';
