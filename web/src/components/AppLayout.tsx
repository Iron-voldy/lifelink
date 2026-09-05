import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../features/auth/AuthContext'
import { Icon, type IconName } from './Icon'
import { ThemeToggle } from './ThemeToggle'
const nav: [string, string, IconName][] = [['/dashboard', 'Overview', 'overview'], ['/donors', 'Donors', 'donors'], ['/inventory', 'Inventory', 'inventory'], ['/camps', 'Donation camps', 'calendar'], ['/workflows', 'AI Workflows', 'workflow']]
export function AppLayout() {
 const { user, logout } = useAuth(); const navigate = useNavigate(); const location = useLocation()
 const [menuOpen, setMenuOpen] = useState(false)
 useEffect(() => { const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setMenuOpen(false) }; window.addEventListener('keydown', close); return () => window.removeEventListener('keydown', close) }, [])
 return <div className="app-frame">
  <a className="skip-link" href="#main-content">Skip to content</a>
  <aside className={`sidebar ${menuOpen ? 'is-open' : ''}`} id="main-navigation">
   <NavLink to="/dashboard" className="brand" onClick={() => setMenuOpen(false)}><span className="brand-mark"><Icon name="drop" /></span><span>LifeLink<small>Care. Connected.</small></span></NavLink>
   <p className="nav-caption">WORKSPACE</p>
   <nav aria-label="Main navigation">{nav.map(([to, label, icon]) => <NavLink key={to} to={to} onClick={() => setMenuOpen(false)}><Icon name={icon} /><span>{label}</span><span className="nav-dot" /></NavLink>)}</nav>
   <div className="sidebar-note"><Icon name="shield" /><strong>People at the heart.</strong><p>Every critical decision stays in human hands.</p></div>
   <div className="sidebar-foot"><div className="avatar">{user?.email.slice(0, 2).toUpperCase()}</div><div className="identity"><strong>{user?.email}</strong><small>Blood bank administrator</small></div><button className="icon-button" aria-label="Sign out" onClick={() => { logout(); navigate('/login', { replace: true }) }}><Icon name="logout" /></button></div>
  </aside>
  {menuOpen && <button className="menu-backdrop" aria-label="Close navigation" onClick={() => setMenuOpen(false)} />}
  <div className="main-shell"><header className="topbar"><div className="topbar-left"><button className="icon-button mobile-menu" aria-label={menuOpen ? 'Close menu' : 'Open menu'} aria-expanded={menuOpen} aria-controls="main-navigation" onClick={() => setMenuOpen(!menuOpen)}><Icon name={menuOpen ? 'close' : 'menu'} /></button><span className="breadcrumb">Workspace <span>/</span> <strong>{nav.find(([path]) => path === location.pathname)?.[1]}</strong></span></div><div className="topbar-right"><span className="region-label">Sri Lanka network</span><ThemeToggle /></div></header>
   <main className="workspace" id="main-content" tabIndex={-1}><div className="page-enter" key={location.pathname}><Outlet /></div><footer className="workspace-footer"><span>LifeLink · Connected care, every day.</span><span>Human-led. Technology-supported.</span></footer></main>
  </div>
 </div>
}
