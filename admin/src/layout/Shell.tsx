import {
  Building2,
  CalendarDays,
  ChartColumnBig,
  ConciergeBell,
  Globe,
  Contact,
  Inbox,
  LayoutDashboard,
  ListChecks,
  MapPin,
  Scale,
  Shapes,
  Smartphone,
  ShieldCheck,
  Tag,
  Trophy,
  Users,
  UserSearch,
  type LucideIcon,
} from 'lucide-react';
import { NavLink, Outlet, useNavigate } from 'react-router';
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
  { to: '/tournaments', label: 'navTournaments', icon: Trophy, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/reports', label: 'navReports', icon: ChartColumnBig, roles: ['VenueManager'] },
  { to: '/categories', label: 'navCategories', icon: Shapes, roles: ['PlatformOperator'] },
  { to: '/venues', label: 'navVenues', icon: Building2, roles: ['PlatformOperator'] },
  { to: '/all-customers', label: 'navCustomers', icon: UserSearch, roles: ['PlatformOperator'] },
  { to: '/app-content', label: 'navAppContent', icon: Smartphone, roles: ['PlatformOperator'] },
  { to: '/website', label: 'navWebsite', icon: Globe, roles: ['PlatformOperator'] },
  { to: '/rules', label: 'navRules', icon: ShieldCheck, roles: ['PlatformOperator'] },
  { to: '/leads', label: 'navLeads', icon: Inbox, roles: ['PlatformOperator'] },
  { to: '/legal', label: 'navLegal', icon: Scale, roles: ['PlatformOperator'] },
  { to: '/venue', label: 'navVenue', icon: MapPin, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/prices', label: 'navPrices', icon: Tag, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/staff', label: 'navStaff', icon: Users, roles: ['VenueManager'] },
];

export function Shell() {
  const { staff, signOut, acting, exitVenue } = useAuth();
  const { t, name } = useI18n();
  const navigate = useNavigate();
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
        {acting && (
          <div className="acting-banner" role="status">
            <span>{t('actingBanner', { venue: name(acting) })}</span>
            <Button
              variant="secondary"
              onClick={() => {
                exitVenue();
                // After the role guard has redirected away from the venue page, land on Venues.
                setTimeout(() => navigate('/venues'), 0);
              }}
            >
              {t('exitVenue')}
            </Button>
          </div>
        )}
        <Outlet />
      </main>
    </div>
  );
}
