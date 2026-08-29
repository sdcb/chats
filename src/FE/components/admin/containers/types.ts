export type RuntimeNode = {
  id: number;
  name: string;
  aiName: string;
  description: string | null;
  backendType: number;
  endpoint: string | null;
  hasCredential: boolean;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
};

export type RuntimeNodeFilters = {
  query: string;
  backendType: '' | '1' | '2' | '3' | '4';
  enabled: '' | 'true' | 'false';
};

export type RuntimeTemplate = {
  id: number;
  name: string;
  runtimeNodeId: number;
  image: string;
  cpuCores: number;
  memoryBytes: number;
  maxProcesses: number;
  backendNetworkName: string | null;
  defaultVolumeBytes: number | null;
  visibility: number;
  createdAt: string;
  updatedAt: string;
  runtimeNode?: RuntimeNode | null;
};

export type TemplateFilters = {
  query: string;
  runtimeNodeId: string;
  visibility: '' | '0' | '1' | '2' | '3';
};

export type ImageEntry = {
  id: number;
  image: string;
  description: string | null;
  isEnabled: boolean;
};

export type ImageFilters = {
  query: string;
  enabled: '' | 'true' | 'false';
};

export type Quota = {
  id: number;
  userId: number | null;
  userName: string | null;
  allowCustomImage: boolean;
  allowedNetworkModes: string;
  maxContainerCount: number | null;
  maxCpuCores: number | null;
  maxMemoryBytes: number | null;
  maxContainerProcesses: number | null;
  maxVolumeBytes: number | null;
  maxContainerCpuCores: number | null;
  maxContainerMemoryBytes: number | null;
  maxVolumeBytesPerVolume: number | null;
  updatedAt: string;
};

export type QuotaFilters = {
  query: string;
  allowCustomImage: '' | 'true' | 'false';
  scope: '' | 'default' | 'user';
};

export type AdminContainerResource = {
  id: number;
  ownerUserId: number;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  ownerChatId: number | null;
  ownerChatTitle: string | null;
  ownerTurnId: number | null;
  runtimeNodeId: number;
  runtimeNodeName: string | null;
  runtimeNodeAIName: string | null;
  isPermanent: boolean;
  backendResourceId: string;
  ip: string | null;
  name: string;
  image: string;
  shellPrefix: string | null;
  cpuCores: number | null;
  memoryBytes: number | null;
  maxProcesses: number | null;
  backendNetworkName: string | null;
  createdAt: string;
  updatedAt: string;
  lastActiveAt: string | null;
  stoppedAt: string | null;
  deletedAt: string | null;
  cleanupAt: string | null;
  volumeDeclaredBytes: number | null;
  volumeMountCount: number;
  chatAccessCount: number;
};

export type ContainerResourceStatus = '' | 'active' | 'stopped' | 'deleted';

export type ContainerResourceFilters = {
  id: string;
  query: string;
  owner: string;
  runtimeNodeId: string;
  status: ContainerResourceStatus;
  permanent: '' | 'true' | 'false';
};

export type RuntimeForm = {
  name: string;
  aiName: string;
  description: string;
  backendType: number;
  endpoint: string;
  credential: string;
  isEnabled: boolean;
};

export type TemplateForm = {
  name: string;
  runtimeNodeId: number;
  image: string;
  cpuCores: number;
  memoryBytes: number;
  maxProcesses: number;
  backendNetworkName: string;
  defaultVolumeBytes: number | null;
  visibility: number;
};

export type ImageForm = {
  image: string;
  description: string;
  isEnabled: boolean;
};

export type QuotaForm = {
  userId: string;
  allowCustomImage: boolean;
  allowedNetworkModes: string;
  maxContainerCount: string;
  maxCpuCores: string;
  maxMemoryBytes: string;
  maxContainerProcesses: string;
  maxVolumeBytes: string;
  maxContainerCpuCores: string;
  maxContainerMemoryBytes: string;
  maxVolumeBytesPerVolume: string;
};

export type DeleteTarget = {
  kind: 'runtime' | 'template' | 'image' | 'quota';
  id: number;
  label: string;
};

export type ContainerTab =
  | 'resources'
  | 'runtime'
  | 'templates'
  | 'images'
  | 'quotas';

export const isContainerTab = (
  value: string | string[] | undefined,
): value is ContainerTab => {
  const tab = Array.isArray(value) ? value[0] : value;
  return (
    tab === 'resources' ||
    tab === 'runtime' ||
    tab === 'templates' ||
    tab === 'images' ||
    tab === 'quotas'
  );
};

export const PAGE_SIZE = 20;
export const EMPTY_VALUE = '-';

export const emptyRuntime: RuntimeForm = {
  name: '',
  aiName: '',
  description: '',
  backendType: 1,
  endpoint: '',
  credential: '',
  isEnabled: true,
};

export const emptyTemplate: TemplateForm = {
  name: '',
  runtimeNodeId: 0,
  image: 'code-interpreter:latest',
  cpuCores: 2,
  memoryBytes: 2147483648,
  maxProcesses: 200,
  backendNetworkName: 'bridge',
  defaultVolumeBytes: null,
  visibility: 3,
};

export const emptyImage: ImageForm = {
  image: '',
  description: '',
  isEnabled: true,
};

export const emptyQuota: QuotaForm = {
  userId: '',
  allowCustomImage: false,
  allowedNetworkModes: 'none,bridge',
  maxContainerCount: '',
  maxCpuCores: '',
  maxMemoryBytes: '',
  maxContainerProcesses: '',
  maxVolumeBytes: '',
  maxContainerCpuCores: '',
  maxContainerMemoryBytes: '',
  maxVolumeBytesPerVolume: '',
};

export const formatDateTime = (value?: string) => {
  if (!value) return EMPTY_VALUE;
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
};

export const formatBytes = (value: number | null | undefined) => {
  if (value == null) return EMPTY_VALUE;
  if (value === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const index = Math.min(
    Math.floor(Math.log(value) / Math.log(1024)),
    units.length - 1,
  );
  return `${(value / 1024 ** index).toFixed(index === 0 ? 0 : 1)} ${
    units[index]
  }`;
};

export const limitText = (value: number | null | undefined, suffix = '') =>
  value == null ? EMPTY_VALUE : `${value}${suffix}`;
