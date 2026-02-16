export enum GlobalConfigKeys {
  tencentSms = 'tencentSms',
  siteInfo = 'siteInfo',
  inboundRequestTrace = 'inboundRequestTrace',
  outboundRequestTrace = 'outboundRequestTrace',
}

export interface SiteInfoConfig {
  customizedLine1: string;
  customizedLine2: string;
}

export const GlobalDefaultConfigs = {
  tencentSms: {
    secretId: '',
    secretKey: '',
    sdkAppId: '',
    signName: '',
    templateId: '',
  },
  siteInfo: {
    customizedLine1: '',
    customizedLine2: '',
  },
  inboundRequestTrace: {
    enabled: false,
    sampleRate: 1,
    filters: {
      sourcePatterns: null,
      includeUrlPatterns: null,
      excludeUrlPatterns: null,
      methods: null,
      statusCodes: null,
      minDurationMs: null,
    },
    headers: {
      includeRequestHeaders: null,
      includeResponseHeaders: null,
      redactRequestHeaders: ['authorization', 'cookie', 'x-api-key', 'proxy-authorization'],
      redactResponseHeaders: ['set-cookie'],
    },
    body: {
      captureRequestBody: true,
      captureResponseBody: true,
      captureRawRequestBody: false,
      captureRawResponseBody: false,
      maxBytes: 5 * 1024 * 1024,
      allowedContentTypes: null,
      redactJsonFields: ['password', 'token', 'secret', 'apiKey', 'access_token', 'refresh_token'],
    },
  },
  outboundRequestTrace: {
    enabled: false,
    sampleRate: 1,
    filters: {
      sourcePatterns: null,
      includeUrlPatterns: null,
      excludeUrlPatterns: null,
      methods: null,
      statusCodes: null,
      minDurationMs: null,
    },
    headers: {
      includeRequestHeaders: null,
      includeResponseHeaders: null,
      redactRequestHeaders: ['authorization', 'cookie', 'x-api-key', 'proxy-authorization'],
      redactResponseHeaders: ['set-cookie'],
    },
    body: {
      captureRequestBody: true,
      captureResponseBody: true,
      captureRawRequestBody: false,
      captureRawResponseBody: false,
      maxBytes: 5 * 1024 * 1024,
      allowedContentTypes: null,
      redactJsonFields: ['password', 'token', 'secret', 'apiKey', 'access_token', 'refresh_token'],
    },
  },
};
