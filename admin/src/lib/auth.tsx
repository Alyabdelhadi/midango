import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, onSessionEnded, refreshSession, setAccessToken } from './api';
import type { Staff } from './types';

type Status = 'loading' | 'signedOut' | 'signedIn';

type Auth = {
  status: Status;
  staff: Staff | null;
  signIn: (email: string, password: string) => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<Auth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>('loading');
  const [staff, setStaff] = useState<Staff | null>(null);

  const end = useCallback(() => {
    setAccessToken(null);
    setStaff(null);
    setStatus('signedOut');
  }, []);

  const loadMe = useCallback(async () => {
    setStaff(await api<Staff>('/staff/me'));
    setStatus('signedIn');
  }, []);

  // Restore the session from the HttpOnly cookie on every page load.
  useEffect(() => {
    onSessionEnded(end);
    refreshSession()
      .then((ok) => (ok ? loadMe() : end()))
      .catch(end);
  }, [end, loadMe]);

  const signIn = async (email: string, password: string) => {
    const result = await api<{ accessToken: string }>('/staff/auth/login?session=cookie', {
      method: 'POST',
      body: { email, password },
    });
    setAccessToken(result.accessToken);
    await loadMe();
  };

  const changePassword = async (currentPassword: string, newPassword: string) => {
    const result = await api<{ accessToken: string }>('/staff/me/password?session=cookie', {
      method: 'POST',
      body: { currentPassword, newPassword },
    });
    setAccessToken(result.accessToken);
    await loadMe();
  };

  const signOut = async () => {
    await api('/auth/logout', { method: 'POST', body: {} }).catch(() => undefined);
    end();
  };

  return (
    <AuthContext.Provider value={{ status, staff, signIn, changePassword, signOut }}>{children}</AuthContext.Provider>
  );
}

export function useAuth(): Auth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
