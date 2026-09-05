import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, onSessionEnded } from '../../api/client'
import type { AuthenticatedUser } from '../../api/types'

type SelfServiceRole = 'Donor' | 'HospitalRequester'
interface AuthState { user: AuthenticatedUser | null; initializing: boolean; sessionExpired: boolean; login(email: string, password: string): Promise<void>; register(email: string, password: string, role: SelfServiceRole): Promise<void>; logout(): void }
const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [initializing, setInitializing] = useState(api.auth.hasRefreshToken())
  const [sessionExpired, setSessionExpired] = useState(false)
  useEffect(() => { if (!api.auth.hasRefreshToken()) return; api.auth.restore().then(setUser).catch(() => api.auth.logout()).finally(() => setInitializing(false)) }, [])
  // The server rejected the refresh token: drop the user so ProtectedRoute sends them to sign in with an explanation.
  useEffect(() => onSessionEnded(() => { setUser(null); setSessionExpired(true); queryClient.clear() }), [queryClient])
  const value = useMemo<AuthState>(() => {
    const signedIn = (next: AuthenticatedUser) => { queryClient.clear(); setSessionExpired(false); setUser(next) }
    return {
      user, initializing, sessionExpired,
      login: async (email, password) => signedIn(await api.auth.login(email, password)),
      register: async (email, password, role) => signedIn(await api.auth.register(email, password, role)),
      // Cached records belong to the person who just left; never show them to the next person on this browser.
      logout: () => { api.auth.logout(); setUser(null); setSessionExpired(false); queryClient.clear() },
    }
  }, [user, initializing, sessionExpired, queryClient])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
export function useAuth() { const context = useContext(AuthContext); if (!context) throw new Error('useAuth must be used inside AuthProvider'); return context }
