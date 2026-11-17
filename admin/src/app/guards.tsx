import type { ReactNode } from 'react';
import { Navigate, Outlet } from 'react-router';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import type { Role } from '../lib/types';
import { ChangePasswordPage } from '../pages/ChangePasswordPage';

/** Signed-in staff only. A temporary password must be replaced before anything else. */
export function RequireStaff() {
  const { status, staff } = useAuth();
  const { t } = useI18n();
  if (status === 'loading') return <p className="page-loading">{t('loading')}</p>;
  if (status === 'signedOut' || !staff) return <Navigate to="/sign-in" replace />;
  if (staff.mustChangePassword) return <ChangePasswordPage />;
  return <Outlet />;
}

export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { staff } = useAuth();
  return staff && roles.includes(staff.role) ? <>{children}</> : <Navigate to="/" replace />;
}

export function HomeRedirect() {
  const { staff } = useAuth();
  return <Navigate to={staff?.role === 'PlatformOperator' ? '/categories' : '/venue'} replace />;
}
