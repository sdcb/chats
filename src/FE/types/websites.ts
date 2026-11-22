export enum PublishedSiteStatus {
  Publishing = 1,
  Active = 2,
  Failed = 3,
  Unpublishing = 4,
  Unpublished = 5,
}

export enum WebGatewayType {
  Caddy2 = 1,
}

export enum DnsProviderType {
  Cloudflare = 1,
  AliyunDns = 2,
}

export enum DnsRecordMode {
  Address = 1,
  Cname = 2,
}

export interface AvailableDomain {
  id: number;
  domain: string;
  publicScheme: string;
  publicPort: number;
}

export interface WebsiteContainer {
  id: string;
  name: string;
  image: string;
  isRunning: boolean;
}

export interface MyWebsite {
  id: number;
  hostname: string;
  subdomain: string;
  url: string;
  domain: string;
  containerId: string;
  containerName: string;
  containerPort: number;
  status: PublishedSiteStatus;
  lastError: string | null;
  createdAt: string;
  updatedAt: string;
  unpublishedAt: string | null;
}

export interface PublishWebsiteRequest {
  containerId: string;
  containerPort: number;
  hostedDomainId: number;
  subdomain: string;
}

export interface WebsiteEvent {
  id: number;
  action: string;
  isSuccess: boolean;
  message: string | null;
  operatorUserId?: number | null;
  createdAt: string;
}

export interface WebGateway {
  id: number;
  name: string;
  gatewayType: WebGatewayType;
  adminEndpoint: string;
  hasCredential: boolean;
  serverNames: string;
  publicIpv4: string | null;
  publicIpv6: string | null;
  cnameTarget: string | null;
  publicScheme: string;
  publicPort: number;
  isEnabled: boolean;
  domainCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertWebGatewayRequest {
  name: string;
  gatewayType: WebGatewayType;
  adminEndpoint: string;
  credential: string | null;
  clearCredential: boolean;
  serverNames: string;
  publicIpv4: string | null;
  publicIpv6: string | null;
  cnameTarget: string | null;
  publicScheme: string;
  publicPort: number;
  isEnabled: boolean;
}

export interface DnsProvider {
  id: number;
  name: string;
  providerType: DnsProviderType;
  hasCredential: boolean;
  isEnabled: boolean;
  domainCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertDnsProviderRequest {
  name: string;
  providerType: DnsProviderType;
  credential: string | null;
  isEnabled: boolean;
}

export interface HostedDomain {
  id: number;
  domain: string;
  dnsZone: string;
  dnsProviderId: number;
  dnsProviderName: string;
  webGatewayId: number;
  webGatewayName: string;
  recordMode: DnsRecordMode;
  recordTtl: number;
  isProxied: boolean;
  allowAllUsers: boolean;
  isEnabled: boolean;
  liveSiteCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertHostedDomainRequest {
  domain: string;
  dnsZone: string;
  dnsProviderId: number;
  webGatewayId: number;
  recordMode: DnsRecordMode;
  recordTtl: number;
  isProxied: boolean;
  allowAllUsers: boolean;
  isEnabled: boolean;
}

export interface WebsiteHostingTestResult {
  success: boolean;
  message: string;
}

export interface PublishedSiteAdmin {
  id: number;
  userId: number;
  userName: string;
  hostname: string;
  url: string;
  domain: string;
  containerName: string;
  containerDeleted: boolean;
  containerPort: number;
  upstreamDial: string | null;
  status: PublishedSiteStatus;
  lastError: string | null;
  createdAt: string;
  updatedAt: string;
  unpublishedAt: string | null;
}

export interface WebsiteReconcileResult {
  routesApplied: number;
  orphanRoutesRemoved: number;
  sitesUnpublished: number;
  errors: string[];
}
