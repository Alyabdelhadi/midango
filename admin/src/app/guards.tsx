import type { ReactNode } from 'react';
import { Navigate, Outlet } from 'react-router';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { opRoleOf } from '../layout/nav';
import type { OperatorRole, Role } from '../lib/types';
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

/** `ops` further limits an operator page to those admin roles (the API enforces the same). */
export function RequireRole({ roles, ops, children }: { roles: Role[]; ops?: OperatorRole[]; children: ReactNode }) {
  const { staff } = useAuth();
  const allowed =
    staff && roles.includes(staff.role) && (!ops || staff.role !== 'PlatformOperator' || ops.includes(opRoleOf(staff)));
  return allowed ? <>{children}</> : <Navigate to="/" replace />;
}

export function HomeRedirect() {
  const { staff } = useAuth();
  const home = staff?.role === 'PlatformOperator' ? '/overview' : staff?.role === 'DeskStaff' ? '/desk' : '/today';
  return <Navigate to={home} replace />;
}
