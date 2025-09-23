// Shared JSON validation helpers

// Returns true if input is empty/whitespace or a valid JSON object (root must be an object)
export function isEmptyOrJsonObject(input?: string | null): boolean {
  if (!input || !input.trim()) return true;
  try {
    const parsed = JSON.parse(input);
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed);
  } catch {
    return false;
  }
}
