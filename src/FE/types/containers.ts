export type ContainerResource = {
  encryptedId: string;
  name: string;
  isPermanent: boolean;
  image: string;
  cpuCores: number | null;
  memoryBytes: number | null;
  maxProcesses: number | null;
  backendNetworkName: string | null;
  runtimeNodeAIName: string | null;
  ip: string | null;
  isDeleted: boolean;
  isStopped: boolean;
  createdAt: string;
  updatedAt: string;
  cleanupAt: string | null;
  grantedChatIds: string[];
};

export type CreateContainerResourceRequest = {
  name?: string | null;
  isPermanent: boolean;
  templateId: number;
  image?: string | null;
  cpuCores?: number | null;
  memoryBytes?: number | null;
  maxProcesses?: number | null;
  backendNetworkName?: string | null;
  ownerChatId?: string | null;
};

export type ContainerTemplate = {
  id: number;
  name: string;
  runtimeNodeId: number;
  runtimeNodeAIName: string;
  image: string;
  cpuCores: number;
  memoryBytes: number;
  maxProcesses: number;
  backendNetworkName: string | null;
  defaultVolumeBytes: number | null;
  visibility: number;
};

export type ContainerSessionDto = {
  encryptedId: string;
  name: string;
  image: string;
  ownerTurnId: number | null;
  cpuCores: number | null;
  memoryBytes: number | null;
  maxProcesses: number | null;
  backendNetworkName: string | null;
  ip: string | null;
  createdAt: string;
  lastActiveAt: string;
  cleanupAt: string | null;
  isPermanent: boolean;
  isStopped: boolean;
  grantedChatIds: string[];
};

export type CreateContainerSessionRequest = {
  name?: string | null;
  image?: string | null;
  cpuCores?: number | null;
  memoryBytes?: number | null;
  maxProcesses?: number | null;
  backendNetworkName?: string | null;
  isPermanent?: boolean;
  templateId?: number;
};

export type RunCommandRequest = {
  command: string;
  timeoutSeconds?: number | null;
};
export type CommandStreamLine =
  | { kind: 'stdout'; data: string }
  | { kind: 'stderr'; data: string }
  | { kind: 'exit'; exitCode: number; executionTimeMs: number }
  | { kind: 'error'; message: string };
export type FileEntry = {
  name: string;
  path: string;
  isDirectory: boolean;
  size: number;
  lastModified: string;
};
export type DirectoryListResponse = { path: string; entries: FileEntry[] };
export type TextFileResponse = {
  path: string;
  isText: boolean;
  sizeBytes: number;
  text: string | null;
};
export type SaveTextFileRequest = { path: string; text: string };
export type EnvironmentVariable = { key: string; value: string };
export type EnvironmentVariablesResponse = {
  systemVariables: EnvironmentVariable[];
  userVariables: EnvironmentVariable[];
};
export type SaveUserEnvironmentVariablesRequest = {
  variables: EnvironmentVariable[];
};
export type DefaultImageResponse = {
  defaultImage: string;
  description: string | null;
};
export type ImageListResponse = { images: string[] };
export type ResourceLimitResponse = { defaultValue: number; maxValue: number };
export type MemoryLimitResponse = { defaultBytes: number; maxBytes: number };
export type NetworkModesResponse = {
  defaultNetworkMode: string;
  maxAllowedNetworkMode: string;
  allowedNetworkModes: string[];
};
