import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../features/auth/AuthContext'
import { Icon } from './Icon'
import { ThemeToggle } from './ThemeToggle'

/** Lightweight frame for non-admin people: brand, who is signed in, theme and an always-visible sign out. */
export function PortalShell({ roleLabel, children }: { roleLabel: string; children: ReactNode }) {
  const { user, logout } = useAuth(); const navigate = useNavigate()
  return <div className="portal-frame">
    <a className="skip-link" href="#main-content">Skip to content</a>
    <header className="portal-bar"><div className="brand"><span className="brand-mark"><Icon name="drop" /></span><span>LifeLink<small>Care. Connected.</small></span></div>
      <div className="portal-actions"><div className="identity"><strong>{user?.email}</strong><small>{roleLabel}</small></div><ThemeToggle /><button className="secondary-button" onClick={() => { logout(); navigate('/login', { replace: true }) }}><Icon name="logout" />Sign out</button></div>
    </header>
    <main className="portal-main" id="main-content" tabIndex={-1}><div className="page-enter">{children}</div></main>
  </div>
}
