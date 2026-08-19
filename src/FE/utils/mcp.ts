export interface McpDisplayItem {
  id: number;
  name: string;
  displayName?: string | null;
}

export function getMcpDisplayLabel<T extends McpDisplayItem>(server: T): string {
  const displayName = server.displayName?.trim();
  return displayName && displayName !== server.name
    ? `${displayName} (${server.name})`
    : server.name;
}
