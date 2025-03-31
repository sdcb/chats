export enum GlobalConfigKeys {
  tencentSms = 'tencentSms',
  siteInfo = 'siteInfo',
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
};
