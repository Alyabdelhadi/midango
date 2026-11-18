import {
  Building2,
  CalendarDays,
  ChartColumnBig,
  ConciergeBell,
  Contact,
  LayoutDashboard,
  ListChecks,
  MapPin,
  Shapes,
  ShieldCheck,
  Tag,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { NavLink, Outlet } from 'react-router';
import { Button } from '../components/Button';
import { LanguageToggle } from '../components/LanguageToggle';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { ROLE_KEY, type Role } from '../lib/types';
import type { MessageKey } from '../locales/en';

type NavItem = { to: string; label: MessageKey; icon: LucideIcon; roles: Role[] };

/** Only screens that work for the role are listed. */
const NAV: NavItem[] = [
  { to: '/today', label: 'navToday', icon: LayoutDashboard, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/desk', label: 'navDesk', icon: ConciergeBell, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/schedule', label: 'navSchedule', icon: CalendarDays, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/bookings', label: 'navBookings', icon: ListChecks, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/customers', label: 'navCustomers', icon: Contact, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/reports', label: 'navReports', icon: ChartColumnBig, roles: ['VenueManager'] },
  { to: '/categories', label: 'navCategories', icon: Shapes, roles: ['PlatformOperator'] },
  { to: '/venues', label: 'navVenues', icon: Building2, roles: ['PlatformOperator'] },
  { to: '/rules', label: 'navRules', icon: ShieldCheck, roles: ['PlatformOperator'] },
  { to: '/venue', label: 'navVenue', icon: MapPin, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/prices', label: 'navPrices', icon: Tag, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/staff', label: 'navStaff', icon: Users, roles: ['VenueManager'] },
];

export function Shell() {
  const { staff, signOut } = useAuth();
  const { t } = useI18n();
  if (!staff) return null;

  return (
    <div className="shell">
      <aside className="shell-nav">
        <div className="shell-brand">
          <img src="/midango-icon.png" alt="Midango" height={28} />
          <span>{t('appBusiness')}</span>
        </div>
        <nav aria-label={t('navLabel')}>
          {NAV.filter((item) => item.roles.includes(staff.role)).map((item) => (
            <NavLink key={item.to} to={item.to} className="shell-link">
              <item.icon size={18} aria-hidden="true" />
              {t(item.label)}
            </NavLink>
          ))}
        </nav>
        <div className="shell-user">
          <span className="shell-user-name">{staff.displayName}</span>
          <span className="shell-user-role">{t(ROLE_KEY[staff.role])}</span>
          <div className="shell-user-actions">
            <LanguageToggle />
            <Button variant="text" onClick={() => void signOut()}>
              {t('signOut')}
            </Button>
          </div>
        </div>
      </aside>
      <main className="shell-main">
        <Outlet />
      </main>
    </div>
  );
}
