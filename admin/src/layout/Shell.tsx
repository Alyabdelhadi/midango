import { useState } from 'react';
import { ChevronsLeft, ChevronsRight, LogOut } from 'lucide-react';
import { NavLink, Outlet, useNavigate } from 'react-router';
import { Button } from '../components/Button';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { OP_ROLE_KEY, ROLE_KEY } from '../lib/types';
import { initials, navFor, opRoleOf } from './nav';
import { Topbar } from './Topbar';

export function Shell() {
  const { staff, signOut, acting, exitVenue } = useAuth();
  const { t, name } = useI18n();
  const navigate = useNavigate();
  // Remembered per browser; only affects wide screens (narrow ones lay the nav out in a row).
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem('mg-nav') === 'collapsed';
    } catch {
      return false;
    }
  });
  if (!staff) return null;

  function toggleNav() {
    setCollapsed((c) => {
      try {
        localStorage.setItem('mg-nav', c ? 'open' : 'collapsed');
      } catch {
        /* storage blocked */
      }
      return !c;
    });
  }

  return (
    <div className={collapsed ? 'shell shell--collapsed' : 'shell'}>
      <aside className="shell-nav">
        <div className="shell-brand">
          <img src="/midango-icon.png" alt="Midango" height={28} />
          <span>
            Midango
            <small>{t('appBusiness')}</small>
          </span>
        </div>
        <nav aria-label={t('navLabel')} className="shell-groups">
          {navFor(staff).map((group) => (
            <div key={group.label} className="shell-group">
              <p className="shell-group-label">{t(group.label)}</p>
              {group.items.map((item) => (
                <NavLink key={item.to} to={item.to} className="shell-link" title={collapsed ? t(item.label) : undefined}>
                  <item.icon size={18} aria-hidden="true" />
                  <span className="shell-link-text">{t(item.label)}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <button
          type="button"
          className="nav-toggle"
          aria-label={t(collapsed ? 'expandSidebar' : 'collapseSidebar')}
          title={collapsed ? t('expandSidebar') : undefined}
          aria-pressed={collapsed}
          onClick={toggleNav}
        >
          {collapsed ? <ChevronsRight aria-hidden size={18} className="flip-rtl" /> : <ChevronsLeft aria-hidden size={18} className="flip-rtl" />}
          <span className="shell-link-text">{t('collapseSidebar')}</span>
        </button>
        <div className="shell-user">
          <span className="avatar" aria-hidden="true">
            {initials(staff.displayName)}
          </span>
          <span className="shell-user-text">
            <span className="shell-user-name">{staff.displayName}</span>
            <span className="shell-user-role">
              {t(staff.role === 'PlatformOperator' ? OP_ROLE_KEY[opRoleOf(staff)] : ROLE_KEY[staff.role])}
            </span>
          </span>
          <button type="button" className="icon-button" aria-label={t('signOut')} title={t('signOut')} onClick={() => void signOut()}>
            <LogOut aria-hidden size={18} className="flip-rtl" />
          </button>
        </div>
      </aside>
      <div className="shell-body">
        <Topbar />
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
    </div>
  );
}
