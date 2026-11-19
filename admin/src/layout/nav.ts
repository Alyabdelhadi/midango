import {
  BookUser,
  Bug,
  Building2,
  History,
  Wallet,
  CalendarDays,
  ChartColumnBig,
  ConciergeBell,
  Contact,
  Globe,
  Inbox,
  LayoutDashboard,
  ListChecks,
  MapPin,
  Scale,
  Shapes,
  ShieldCheck,
  Smartphone,
  Tag,
  Trophy,
  Users,
  UserSearch,
  type LucideIcon,
} from 'lucide-react';
import type { OperatorRole, Role, Staff } from '../lib/types';
import type { MessageKey } from '../locales/en';

export type NavItem = { to: string; label: MessageKey; icon: LucideIcon };
/** `ops` limits an operator item to those admin roles; it mirrors the API's OperatorAccess table. */
export type NavGroup = { label: MessageKey; roles: Role[]; items: (NavItem & { roles?: Role[]; ops?: OperatorRole[] })[] };

export const SUPPORT: OperatorRole[] = ['Owner', 'Support'];
export const CONTENT: OperatorRole[] = ['Owner', 'Content'];
export const FINANCE: OperatorRole[] = ['Owner', 'Finance'];
export const OWNER: OperatorRole[] = ['Owner'];

/** Sidebar sections. Only screens that work for the role are listed. */
export const NAV: NavGroup[] = [
  { label: 'groupOverview', roles: ['PlatformOperator'], items: [{ to: '/overview', label: 'navOverview', icon: LayoutDashboard }] },
  {
    label: 'groupCatalog',
    roles: ['PlatformOperator'],
    items: [
      { to: '/categories', label: 'navCategories', icon: Shapes, ops: SUPPORT },
      { to: '/venues', label: 'navVenues', icon: Building2, ops: SUPPORT },
      { to: '/all-customers', label: 'navCustomers', icon: UserSearch, ops: SUPPORT },
    ],
  },
  {
    label: 'groupContent',
    roles: ['PlatformOperator'],
    items: [
      { to: '/app-content', label: 'navAppContent', icon: Smartphone, ops: CONTENT },
      { to: '/website', label: 'navWebsite', icon: Globe, ops: CONTENT },
      { to: '/legal', label: 'navLegal', icon: Scale, ops: CONTENT },
    ],
  },
  {
    label: 'groupFinance',
    roles: ['PlatformOperator'],
    items: [{ to: '/accounting', label: 'navAccounting', icon: Wallet, ops: FINANCE }],
  },
  {
    label: 'groupPlatform',
    roles: ['PlatformOperator'],
    items: [
      { to: '/leads', label: 'navLeads', icon: Inbox, ops: SUPPORT },
      { to: '/rules', label: 'navRules', icon: ShieldCheck, ops: FINANCE },
    ],
  },
  {
    label: 'groupAdministration',
    roles: ['PlatformOperator'],
    items: [
      { to: '/team', label: 'navTeam', icon: BookUser, ops: OWNER },
      { to: '/audit', label: 'navAudit', icon: History, ops: OWNER },
      { to: '/errors', label: 'navErrors', icon: Bug, ops: OWNER },
    ],
  },
  {
    label: 'groupOperations',
    roles: ['VenueManager', 'DeskStaff'],
    items: [
      { to: '/today', label: 'navToday', icon: LayoutDashboard },
      { to: '/desk', label: 'navDesk', icon: ConciergeBell },
      { to: '/schedule', label: 'navSchedule', icon: CalendarDays },
      { to: '/bookings', label: 'navBookings', icon: ListChecks },
      { to: '/customers', label: 'navCustomers', icon: Contact },
      { to: '/tournaments', label: 'navTournaments', icon: Trophy },
    ],
  },
  {
    label: 'groupInsights',
    roles: ['VenueManager'],
    items: [{ to: '/reports', label: 'navReports', icon: ChartColumnBig }],
  },
  {
    label: 'groupSetup',
    roles: ['VenueManager', 'DeskStaff'],
    items: [
      { to: '/venue', label: 'navVenue', icon: MapPin },
      { to: '/prices', label: 'navPrices', icon: Tag },
      { to: '/staff', label: 'navStaff', icon: Users, roles: ['VenueManager'] },
    ],
  },
];

/** An operator without a role on record (older token) counts as Owner, as the API does. */
export const opRoleOf = (staff: Staff): OperatorRole => staff.operatorRole ?? 'Owner';

export function navFor(staff: Staff): { label: MessageKey; items: NavItem[] }[] {
  return NAV.filter((g) => g.roles.includes(staff.role))
    .map((g) => ({
      label: g.label,
      items: g.items.filter(
        (i) => (!i.roles || i.roles.includes(staff.role)) && (!i.ops || i.ops.includes(opRoleOf(staff))),
      ),
    }))
    .filter((g) => g.items.length > 0);
}

/** The section and item the current path belongs to (a detail page counts as its list). */
export function navMatch(staff: Staff, path: string) {
  for (const g of navFor(staff)) {
    const item = g.items.find((i) => path === i.to || path.startsWith(`${i.to}/`));
    if (item) return { group: g.label, item };
  }
  return null;
}

export const initials = (s: string) =>
  s
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0]!.toUpperCase())
    .join('') || '?';
