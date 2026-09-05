import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { UserRole } from '../../api/types'
import { useAuth } from './AuthContext'

/** Where each role lands after signing in. */
export function homeFor(role: UserRole) { return role === 'BloodBankAdmin' ? '/dashboard' : role === 'Donor' ? '/donor' : '/welcome' }

export function ProtectedRoute({ roles }: { roles?: UserRole[] }) {
  const { user, initializing } = useAuth(); const location = useLocation()
  if (initializing) return <div className="center-state"><span className="spinner" /> Restoring session…</div>
  if (!user) return <Navigate to="/login" state={{ from: location }} replace />
  // Send people to their own area instead of a dead end they cannot sign out of.
  if (roles && !roles.includes(user.role)) return <Navigate to={homeFor(user.role)} replace />
  return <Outlet />
}
