import { createFetchClient } from '@/hooks/createFetchClient';

import {
  DnsProvider,
  HostedDomain,
  PublishedSiteAdmin,
  UpsertDnsProviderRequest,
  UpsertHostedDomainRequest,
  UpsertWebGatewayRequest,
  WebGateway,
  WebsiteEvent,
  WebsiteHostingTestResult,
  WebsiteReconcileResult,
} from '@/types/websites';

const BASE = '/api/admin/website-hosting';

export const getAdminWebGateways = () =>
  createFetchClient().get<WebGateway[]>(`${BASE}/gateways`);

export const saveAdminWebGateway = (
  id: number | null,
  body: UpsertWebGatewayRequest,
) =>
  id == null
    ? createFetchClient().post<void>(`${BASE}/gateways`, { body })
    : createFetchClient().put<void>(`${BASE}/gateways/${id}`, { body });

export const deleteAdminWebGateway = (id: number) =>
  createFetchClient().delete<void>(`${BASE}/gateways/${id}`);

export const testAdminWebGateway = (id: number) =>
  createFetchClient().post<WebsiteHostingTestResult>(
    `${BASE}/gateways/${id}/test`,
  );

export const getAdminDnsProviders = () =>
  createFetchClient().get<DnsProvider[]>(`${BASE}/dns-providers`);

export const saveAdminDnsProvider = (
  id: number | null,
  body: UpsertDnsProviderRequest,
) =>
  id == null
    ? createFetchClient().post<void>(`${BASE}/dns-providers`, { body })
    : createFetchClient().put<void>(`${BASE}/dns-providers/${id}`, { body });

export const deleteAdminDnsProvider = (id: number) =>
  createFetchClient().delete<void>(`${BASE}/dns-providers/${id}`);

export const testAdminDnsProvider = (id: number, zone?: string) =>
  createFetchClient().post<WebsiteHostingTestResult>(
    `${BASE}/dns-providers/${id}/test${
      zone ? `?zone=${encodeURIComponent(zone)}` : ''
    }`,
  );

export const getAdminHostedDomains = () =>
  createFetchClient().get<HostedDomain[]>(`${BASE}/domains`);

export const saveAdminHostedDomain = (
  id: number | null,
  body: UpsertHostedDomainRequest,
) =>
  id == null
    ? createFetchClient().post<void>(`${BASE}/domains`, { body })
    : createFetchClient().put<void>(`${BASE}/domains/${id}`, { body });

export const deleteAdminHostedDomain = (id: number) =>
  createFetchClient().delete<void>(`${BASE}/domains/${id}`);

export const testAdminHostedDomain = (id: number) =>
  createFetchClient().post<WebsiteHostingTestResult>(
    `${BASE}/domains/${id}/test`,
  );

export const getAdminPublishedSites = (includeUnpublished: boolean) =>
  createFetchClient().get<PublishedSiteAdmin[]>(
    `${BASE}/sites?includeUnpublished=${includeUnpublished}`,
  );

export const getAdminPublishedSiteEvents = (id: number) =>
  createFetchClient().get<WebsiteEvent[]>(`${BASE}/sites/${id}/events`);

export const unpublishAdminPublishedSite = (id: number, force: boolean) =>
  createFetchClient().post<void>(
    `${BASE}/sites/${id}/unpublish?force=${force}`,
  );

export const reconcileAdminWebsites = () =>
  createFetchClient().post<WebsiteReconcileResult>(`${BASE}/reconcile`);
