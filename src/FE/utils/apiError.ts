export function getApiErrorMessage(err: any, fallback: string): string {
  if (!err) return fallback;
  if (typeof err === 'string' && err.trim()) return err;

  const direct = err?.message || err?.errMessage;
  if (typeof direct === 'string' && direct.trim()) return direct;

  const errorsObj = err?.errors;
  const maybeDict =
    errorsObj && typeof errorsObj === 'object' ? errorsObj : err;
  if (maybeDict && typeof maybeDict === 'object') {
    const messages: string[] = [];
    for (const v of Object.values(maybeDict)) {
      if (typeof v === 'string' && v.trim()) {
        messages.push(v);
      } else if (Array.isArray(v)) {
        for (const item of v) {
          if (typeof item === 'string' && item.trim()) messages.push(item);
        }
      }
    }
    if (messages.length > 0) return messages.join('\n');
  }

  return fallback;
}

