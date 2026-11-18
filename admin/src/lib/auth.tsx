import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { api, getAccessToken, onActingEnded, onSessionEnded, refreshSession, setAccessToken, setActing } from './api';
import type { Staff } from './types';

type Status = 'loading' | 'signedOut' | 'signedIn';

/** The venue a platform operator has open. Memory only: a reload returns to the operator view. */
export type Acting = { venueId: string; nameEn: string; nameAr: string; expiresAt: string };

type Auth = {
  status: Status;
  staff: Staff | null;
  signIn: (email: string, password: string) => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  signOut: () => Promise<void>;
  acting: Acting | null;
  openVenue: (venueId: string) => Promise<void>;
  exitVenue: () => void;
};

const AuthContext = createContext<Auth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>('loading');
  const [staff, setStaff] = useState<Staff | null>(null);
  const [acting, setActingState] = useState<Acting | null>(null);
  // The operator's own token and profile, put back on exit.
  const operator = useRef<{ token: string | null; staff: Staff } | null>(null);

  const exitVenue = useCallback(() => {
    const saved = operator.current;
    if (!saved) return;
    operator.current = null;
    setAccessToken(saved.token);
    setActing(false);
    setStaff(saved.staff);
    setActingState(null);
  }, []);

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
    onActingEnded(exitVenue);
    refreshSession()
      .then((ok) => (ok ? loadMe() : end()))
      .catch(end);
  }, [end, exitVenue, loadMe]);

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

  const openVenue = async (venueId: string) => {
    if (!staff || operator.current) return;
    const result = await api<{ accessToken: string } & Acting>(`/admin/venues/${venueId}/act`, { method: 'POST', body: {} });
    operator.current = { token: getAccessToken(), staff };
    setAccessToken(result.accessToken);
    setActing(true);
    setStaff({ ...staff, role: 'VenueManager', venueId });
    setActingState({ venueId, nameEn: result.nameEn, nameAr: result.nameAr, expiresAt: result.expiresAt });
  };

  const signOut = async () => {
    exitVenue();
    await api('/auth/logout', { method: 'POST', body: {} }).catch(() => undefined);
    end();
  };

  return (
    <AuthContext.Provider value={{ status, staff, signIn, changePassword, signOut, acting, openVenue, exitVenue }}>{children}</AuthContext.Provider>
  );
}

export function useAuth(): Auth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
