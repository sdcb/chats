export type DockerSessionDto = {
  encryptedSessionId: string;
  label: string;
  image: string;
  containerId: string;
  cpuCores: number | null;
  memoryBytes: number | null;
  maxProcesses: number | null;
  networkMode: string;
  createdAt: string;
  lastActiveAt: string;
  expiresAt: string;
};

export type DefaultImageResponse = {
  defaultImage: string;
  description: string | null;
};

export type ImageListResponse = {
  images: string[];
};

export type ResourceLimitResponse = {
  defaultValue: number;
  maxValue: number;
};

export type MemoryLimitResponse = {
  defaultBytes: number;
  maxBytes: number;
};

export type NetworkModesResponse = {
  defaultNetworkMode: string;
  maxAllowedNetworkMode: string;
  allowedNetworkModes: string[];
};

export type CreateDockerSessionRequest = {
  label?: string | null;
  image?: string | null;
  cpuCores?: number | null;
  memoryBytes?: number | null;
  maxProcesses?: number | null;
  networkMode?: string | null;
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

export type DirectoryListResponse = {
  path: string;
  entries: FileEntry[];
};

export type TextFileResponse = {
  path: string;
  isText: boolean;
  sizeBytes: number;
  text: string | null;
};

export type SaveTextFileRequest = {
  path: string;
  text: string;
};

