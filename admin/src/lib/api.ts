/** All calls go to /api, which Vite (dev) and the reverse proxy (production) forward to the .NET API. */
export class ApiError extends Error {
  readonly status: number;
  readonly missing?: string[];

  constructor(status: number, message: string, missing?: string[]) {
    super(message);
    this.status = status;
    this.missing = missing;
  }
}

let accessToken: string | null = null;
let sessionEnded: () => void = () => {};
let refreshing: Promise<boolean> | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionEnded(handler: () => void) {
  sessionEnded = handler;
}

/** Trades the HttpOnly session cookie for a new access token. Concurrent callers share one request:
 *  refresh tokens are single-use, and spending one twice signs the user out everywhere. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: '{}',
      });
      if (!res.ok) {
        accessToken = null;
        return false;
      }
      accessToken = ((await res.json()) as { accessToken: string }).accessToken;
      return true;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

type Options = { method?: 'GET' | 'POST' | 'PUT'; body?: unknown };

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  const send = () =>
    fetch(`/api${path}`, {
      method: options.method ?? 'GET',
      headers: {
        'Content-Type': 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });

  let res = await send();
  if (res.status === 401 && accessToken !== null) {
    if (await refreshSession()) res = await send();
    else sessionEnded();
  }
  if (!res.ok) {
    const problem = (await res.json().catch(() => ({}))) as { title?: string; missing?: string[] };
    throw new ApiError(res.status, problem.title ?? `Request failed (${res.status})`, problem.missing);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

export function errorText(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
