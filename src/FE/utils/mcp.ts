export interface McpLabelItem {
  id: number;
  label: string;
}

export function getMcpDisplayLabel<T extends McpLabelItem>(
  server: T,
  servers: T[],
): string {
  const normalizedLabel = server.label.toLowerCase();
  const duplicateCount = servers.filter(
    (candidate) => candidate.label.toLowerCase() === normalizedLabel,
  ).length;

  return duplicateCount > 1 ? `${server.label} (#${server.id})` : server.label;
}
