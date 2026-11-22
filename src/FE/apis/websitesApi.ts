import { createFetchClient } from '@/hooks/createFetchClient';

import {
  AvailableDomain,
  MyWebsite,
  PublishWebsiteRequest,
  WebsiteContainer,
  WebsiteEvent,
} from '@/types/websites';

export const getWebsiteDomains = () =>
  createFetchClient().get<AvailableDomain[]>('/api/websites/domains');

export const getWebsiteContainers = () =>
  createFetchClient().get<WebsiteContainer[]>('/api/websites/containers');

export const getMyWebsites = (includeUnpublished = false) =>
  createFetchClient().get<MyWebsite[]>(
    `/api/websites?includeUnpublished=${includeUnpublished}`,
  );

export const publishWebsite = (body: PublishWebsiteRequest) =>
  createFetchClient().post<number>('/api/websites', { body });

export const unpublishWebsite = (id: number) =>
  createFetchClient().post<void>(`/api/websites/${id}/unpublish`);

export const getWebsiteEvents = (id: number) =>
  createFetchClient().get<WebsiteEvent[]>(`/api/websites/${id}/events`);
