import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import {
  Bell,
  Building2,
  CalendarCheck,
  ChevronDown,
  ChevronRight,
  CornerDownLeft,
  Inbox,
  LogOut,
  Moon,
  Search,
  Sun,
} from 'lucide-react';
import { LanguageToggle } from '../components/LanguageToggle';
import { api } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import type { AdminCustomerList, StaffBooking, Today, VenueCustomer, VenueLead, VenueSummary } from '../lib/types';
import { OP_ROLE_KEY, ROLE_KEY } from '../lib/types';
import type { MessageKey } from '../locales/en';
import { useDropdown } from '../lib/useDropdown';
import { initials, navFor, navMatch, opRoleOf, SUPPORT } from './nav';

export function Topbar() {
  const { staff } = useAuth();
  const { t } = useI18n();
  const { pathname } = useLocation();
  const [searching, setSearching] = useState(false);

  // Ctrl+K / ⌘K opens search from anywhere.
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setSearching(true);
      }
    };
    document.addEventListener('keydown', key);
    return () => document.removeEventListener('keydown', key);
  }, []);

  if (!staff) return null;
  const here = navMatch(staff, pathname);

  return (
    <header className="topbar">
      <nav className="crumbs" aria-label={t('breadcrumb')}>
        {here && (
          <>
            <span>{t(here.group)}</span>
            <ChevronRight aria-hidden size={14} className="flip-rtl" />
            <span aria-current="page">{t(here.item.label)}</span>
          </>
        )}
      </nav>
      <button type="button" className="topbar-search" onClick={() => setSearching(true)}>
        <Search aria-hidden size={16} />
        <span>{t('searchEverything')}</span>
        <kbd>Ctrl K</kbd>
      </button>
      <div className="topbar-actions">
        <LanguageToggle />
        <ThemeToggle />
        {/* Venue requests are Support work: other operator roles get no bell. */}
        {(staff.role !== 'PlatformOperator' || SUPPORT.includes(opRoleOf(staff))) && <Notifications />}
        <UserMenu />
      </div>
      {searching && <SearchDialog onClose={() => setSearching(false)} />}
    </header>
  );
}

/** Light / dark. The choice is remembered; index.html applies it before the first paint. */
function ThemeToggle() {
  const { t } = useI18n();
  const [dark, setDark] = useState(() => document.documentElement.dataset.theme === 'dark');
  function toggle() {
    const next = !dark;
    setDark(next);
    document.documentElement.dataset.theme = next ? 'dark' : 'light';
    try {
      localStorage.setItem('mg-theme', next ? 'dark' : 'light');
    } catch {
      /* storage blocked: the choice lasts until reload */
    }
  }
  const label = t(dark ? 'lightMode' : 'darkMode');
  return (
    <button type="button" className="icon-button" aria-label={label} title={label} onClick={toggle}>
      {dark ? <Sun aria-hidden size={18} /> : <Moon aria-hidden size={18} />}
    </button>
  );
}

function UserMenu() {
  const { staff, signOut } = useAuth();
  const { t } = useI18n();
  const menu = useDropdown();
  if (!staff) return null;
  return (
    <div className="dropdown" ref={menu.ref}>
      <button type="button" className="user-button" aria-expanded={menu.open} onClick={() => menu.setOpen(!menu.open)}>
        <span className="avatar" aria-hidden="true">
          {initials(staff.displayName)}
        </span>
        <span className="user-button-name">{staff.displayName}</span>
        <ChevronDown aria-hidden size={16} />
      </button>
      {menu.open && (
        <div className="dropdown-panel" role="menu">
          <div className="dropdown-head">
            <strong>{staff.displayName}</strong>
            <span>{staff.email}</span>
            <span className="mg-pill mg-pill--info">
              {t(staff.role === 'PlatformOperator' ? OP_ROLE_KEY[opRoleOf(staff)] : ROLE_KEY[staff.role])}
            </span>
          </div>
          <button type="button" role="menuitem" className="dropdown-item" onClick={() => void signOut()}>
            <LogOut aria-hidden size={16} className="flip-rtl" />
            {t('signOut')}
          </button>
        </div>
      )}
    </div>
  );
}

/** Each part is shown in its own <bdi>, so an Arabic name beside an English date keeps its order. */
type Note = { id: string; title: string[]; sub: string[] };

const Parts = ({ parts }: { parts: string[] }) => (
  <>
    {parts.map((p, i) => (
      <span key={i}>
        {i > 0 && ' · '}
        <bdi>{p}</bdi>
      </span>
    ))}
  </>
);

/** Operators: venue requests nobody has called yet. Venue staff: bookings still to come today. */
function Notifications() {
  const { staff } = useAuth();
  const { t, lang, name } = useI18n();
  const menu = useDropdown();
  const [notes, setNotes] = useState<Note[] | null>(null);
  const [error, setError] = useState(false);
  const operator = staff?.role === 'PlatformOperator';

  // Loads on page load, each time the bell opens, every minute, and when the tab comes back into view.
  function load() {
    const time = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', {
      dateStyle: operator ? 'medium' : undefined,
      timeStyle: 'short',
    });
    setError(false);
    const request: Promise<Note[]> = operator
      ? api<VenueLead[]>('/admin/leads').then((leads) =>
          leads
            .filter((l) => l.contactedAt === null)
            .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
            .map((l) => ({
              id: l.id,
              title: [l.venueName, l.city],
              sub: [l.contactName, time.format(new Date(l.createdAt))],
            })),
        )
      : api<Today>('/venue/today').then((today) =>
          today.next.map((b) => ({
            id: b.id,
            title: [b.customerName ?? t('guest')],
            sub: [name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr }), time.format(new Date(b.startsAt))],
          })),
        );
    request.then(setNotes, () => setError(true));
  }
  const loadRef = useRef(load);
  loadRef.current = load;
  useEffect(() => {
    loadRef.current();
    const tick = setInterval(() => loadRef.current(), 60_000);
    const back = () => {
      if (document.visibilityState === 'visible') loadRef.current();
    };
    document.addEventListener('visibilitychange', back);
    return () => {
      clearInterval(tick);
      document.removeEventListener('visibilitychange', back);
    };
  }, [operator]);

  const count = notes?.length ?? 0;
  const label = t(operator ? 'notesLeadsTitle' : 'notesTodayTitle');
  return (
    <div className="dropdown" ref={menu.ref}>
      <button
        type="button"
        className="icon-button"
        aria-label={count > 0 ? t('notificationsCount', { n: count }) : t('notifications')}
        aria-expanded={menu.open}
        onClick={() => {
          if (!menu.open) load();
          menu.setOpen(!menu.open);
        }}
      >
        <Bell aria-hidden size={18} />
        {count > 0 && <span className="badge-count">{count > 9 ? '9+' : count}</span>}
      </button>
      {menu.open && (
        <div className="dropdown-panel dropdown-panel--wide" role="dialog" aria-label={label}>
          <div className="dropdown-title">
            <strong>{label}</strong>
            {count > 0 && <span className="mg-pill mg-pill--info">{count}</span>}
          </div>
          {error ? (
            <p className="dropdown-empty">{t('notesError')}</p>
          ) : !notes ? (
            <p className="dropdown-empty">{t('loading')}</p>
          ) : notes.length === 0 ? (
            <p className="dropdown-empty">{t(operator ? 'notesLeadsEmpty' : 'notesTodayEmpty')}</p>
          ) : (
            <ul className="note-list">
              {notes.slice(0, 6).map((n) => (
                <li key={n.id}>
                  <span className="note-icon" aria-hidden="true">
                    <Inbox size={16} />
                  </span>
                  <span>
                    <strong>
                      <Parts parts={n.title} />
                    </strong>
                    <small>
                      <Parts parts={n.sub} />
                    </small>
                  </span>
                </li>
              ))}
            </ul>
          )}
          <Link className="dropdown-footer" to={operator ? '/leads' : '/schedule'} onClick={() => menu.setOpen(false)}>
            {t(operator ? 'openVenueRequests' : 'openSchedule')}
          </Link>
        </div>
      )}
    </div>
  );
}

type Hit = { key: string; group: MessageKey; label: string; sub?: string; to: string; icon?: ReactNode };

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const shift = (n: number) => {
  const d = new Date();
  return iso(new Date(d.getFullYear(), d.getMonth(), d.getDate() + n));
};

/** Ctrl+K search: pages for everyone; venues + customers for operators; bookings + customers for venue staff. */
function SearchDialog({ onClose }: { onClose: () => void }) {
  const { staff } = useAuth();
  const { t, name } = useI18n();
  const navigate = useNavigate();
  const ref = useRef<HTMLDialogElement>(null);
  const [q, setQ] = useState('');
  const [remote, setRemote] = useState<Hit[]>([]);
  const [busy, setBusy] = useState(false);
  const [active, setActive] = useState(0);
  const [venues, setVenues] = useState<VenueSummary[]>([]);
  const operator = staff?.role === 'PlatformOperator';

  useEffect(() => {
    ref.current?.showModal();
    if (operator) api<VenueSummary[]>('/admin/venues').then(setVenues, () => undefined);
  }, [operator]);

  // Server searches wait for a pause in typing and at least 2 characters.
  useEffect(() => {
    const term = q.trim();
    if (term.length < 2) {
      setRemote([]);
      setBusy(false);
      return;
    }
    let live = true;
    setBusy(true);
    const timer = setTimeout(() => {
      const e = encodeURIComponent(term);
      // The bookings list allows up to 62 days: the last 30 and the next 31.
      const from = shift(-30);
      const to = shift(31);
      const requests: Promise<Hit[]>[] = operator
        ? [
            api<AdminCustomerList>(`/admin/customers?page=1&q=${e}`).then((r) =>
              r.items.slice(0, 5).map((c) => ({
                key: `c${c.id}`,
                group: 'resultsCustomers' as MessageKey,
                label: c.name ?? t('noName'),
                sub: c.phone,
                to: `/all-customers/${c.id}`,
              })),
            ),
          ]
        : [
            api<StaffBooking[]>(`/venue/bookings?from=${from}&to=${to}&q=${e}`).then((r) =>
              r.slice(0, 5).map((b) => ({
                key: `b${b.id}`,
                group: 'resultsBookings' as MessageKey,
                label: `${b.reference} · ${b.customerName ?? t('guest')}`,
                sub: `${name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })} · ${b.startsAt.slice(0, 10)}`,
                to: `/bookings?${new URLSearchParams({ q: b.reference, from, to })}`,
                icon: <CalendarCheck aria-hidden size={16} />,
              })),
            ),
            api<VenueCustomer[]>(`/venue/customers?q=${e}`).then((r) =>
              r.slice(0, 5).map((c) => ({
                key: `c${c.id}`,
                group: 'resultsCustomers' as MessageKey,
                label: c.name ?? t('noName'),
                sub: c.phoneMasked,
                to: `/customers?q=${encodeURIComponent(c.name ?? term)}`,
              })),
            ),
          ];
      void Promise.all(requests.map((r) => r.catch(() => [] as Hit[]))).then((lists) => {
        if (!live) return;
        setRemote(lists.flat());
        setBusy(false);
      });
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [q, operator, t, name]);

  if (!staff) return null;
  const term = q.trim().toLocaleLowerCase();
  const pages: Hit[] = navFor(staff)
    .flatMap((g) => g.items)
    .filter((i) => !term || t(i.label).toLocaleLowerCase().includes(term))
    .map((i) => ({ key: `p${i.to}`, group: 'resultsPages', label: t(i.label), to: i.to, icon: <i.icon aria-hidden size={16} /> }));
  const venueHits: Hit[] = term
    ? venues
        .filter((v) => `${v.nameEn} ${v.nameAr}`.toLocaleLowerCase().includes(term))
        .slice(0, 5)
        .map((v) => ({ key: `v${v.id}`, group: 'resultsVenues', label: name(v), to: `/venues?q=${encodeURIComponent(v.nameEn)}`, icon: <Building2 aria-hidden size={16} /> }))
    : [];
  const hits = [...pages, ...venueHits, ...remote];
  const groups = [...new Set(hits.map((h) => h.group))];

  function go(hit: Hit | undefined) {
    if (!hit) return;
    ref.current?.close();
    navigate(hit.to);
  }

  return (
    <dialog ref={ref} className="search-dialog" aria-label={t('searchEverything')} onClose={onClose}>
      <div className="search-input">
        <Search aria-hidden size={18} />
        <input
          autoFocus
          type="search"
          role="combobox"
          aria-expanded="true"
          aria-controls="search-results"
          aria-activedescendant={hits[active] ? `hit-${hits[active].key}` : undefined}
          placeholder={t(operator ? 'searchOperatorHint' : 'searchVenueHint')}
          value={q}
          onChange={(e) => {
            setQ(e.target.value);
            setActive(0);
          }}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') {
              e.preventDefault();
              setActive((a) => Math.min(hits.length - 1, a + 1));
            } else if (e.key === 'ArrowUp') {
              e.preventDefault();
              setActive((a) => Math.max(0, a - 1));
            } else if (e.key === 'Enter') {
              e.preventDefault();
              go(hits[active]);
            }
          }}
        />
        <kbd>Esc</kbd>
      </div>
      <div id="search-results" role="listbox" className="search-results" aria-label={t('searchEverything')}>
        {groups.map((g) => (
          <div key={g} role="group" aria-label={t(g)}>
            <p className="search-group">{t(g)}</p>
            {hits
              .filter((h) => h.group === g)
              .map((h) => {
                const i = hits.indexOf(h);
                return (
                  <div
                    key={h.key}
                    id={`hit-${h.key}`}
                    role="option"
                    aria-selected={i === active}
                    className="search-hit"
                    onMouseEnter={() => setActive(i)}
                    onClick={() => go(h)}
                  >
                    {h.icon ?? (
                      <span className="avatar avatar--sm" aria-hidden="true">
                        {initials(h.label)}
                      </span>
                    )}
                    <span className="search-hit-text">
                      {h.label}
                      {h.sub && <small>{h.sub}</small>}
                    </span>
                    {i === active && <CornerDownLeft aria-hidden size={14} className="search-hit-enter" />}
                  </div>
                );
              })}
          </div>
        ))}
        {hits.length === 0 && <p className="dropdown-empty">{busy ? t('loading') : t('noMatches')}</p>}
        {busy && hits.length > 0 && <p className="search-busy">{t('searching')}</p>}
      </div>
    </dialog>
  );
}
