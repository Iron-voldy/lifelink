import { useState, type FormEvent } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { useAuth } from '../features/auth/AuthContext'
import { homeFor } from '../features/auth/ProtectedRoute'
import { Icon } from '../components/Icon'
import { ThemeToggle } from '../components/ThemeToggle'

/** Mirrors AuthService.IsStrongPassword so people see every missing rule before the request is sent. */
export function passwordProblems(value: string) {
  return [value.length < 12 && 'at least 12 characters', !/[A-Z]/.test(value) && 'an uppercase letter', !/[a-z]/.test(value) && 'a lowercase letter', !/[0-9]/.test(value) && 'a digit', !/[^A-Za-z0-9]/.test(value) && 'a symbol'].filter(Boolean) as string[]
}

export function RegisterPage() {
  const { user, register } = useAuth()
  const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [confirm, setConfirm] = useState('')
  const [role, setRole] = useState<'Donor' | 'HospitalRequester'>('Donor'); const [visible, setVisible] = useState(false)
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  if (user) return <Navigate to={homeFor(user.role)} replace />
  const problems = passwordProblems(password)

  async function submit(event: FormEvent) {
    event.preventDefault(); setError('')
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) { setError('Enter a valid email address, for example name@example.com.'); return }
    if (problems.length) { setError(`Password needs ${problems.join(', ')}.`); return }
    if (password !== confirm) { setError('Passwords do not match.'); return }
    setBusy(true)
    try { await register(email.trim(), password, role) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Registration failed.') } finally { setBusy(false) }
  }

  return <main className="login-page"><section className="login-story"><div className="brand"><span className="brand-mark"><Icon name="drop" /></span><span>LifeLink<small>Care. Connected.</small></span></div><div><p className="eyebrow">Join the network</p><h1>One donation.<br />Up to three<br /><em>lives saved.</em></h1><p>Register as a donor to keep your eligibility up to date and book a seat at the next donation camp near you.</p></div><small>THE BLOOD & EMERGENCY NETWORK · SRI LANKA</small></section>
    <section className="login-card"><div className="login-theme"><ThemeToggle /></div><form onSubmit={submit} noValidate>
      <div className="brand mobile-brand"><span className="brand-mark"><Icon name="drop" /></span><span>LifeLink<small>Care. Connected.</small></span></div>
      <p className="eyebrow">Create your account</p><h2>Join LifeLink</h2><p>Choose how you will take part. You can complete your profile right after.</p>
      <div className="segmented role-choice" role="radiogroup" aria-label="Account type">
        <button type="button" role="radio" aria-checked={role === 'Donor'} className={role === 'Donor' ? 'active' : ''} onClick={() => setRole('Donor')}>Blood donor</button>
        <button type="button" role="radio" aria-checked={role === 'HospitalRequester'} className={role === 'HospitalRequester' ? 'active' : ''} onClick={() => setRole('HospitalRequester')}>Hospital staff</button>
      </div>
      <label>Email address<input autoComplete="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required placeholder="you@example.com" /></label>
      <label>Password<span className="password-field"><input aria-label="Password" autoComplete="new-password" type={visible ? 'text' : 'password'} value={password} onChange={e => setPassword(e.target.value)} required aria-describedby="password-rules" /><button type="button" aria-label={visible ? 'Hide password' : 'Show password'} onClick={() => setVisible(!visible)}>{visible ? 'Hide' : 'Show'}</button></span><small id="password-rules" className={password && problems.length ? 'field-hint danger-text' : 'field-hint'}>{password && problems.length ? `Still needs ${problems.join(', ')}.` : '12+ characters with upper, lower, digit and symbol.'}</small></label>
      <label>Confirm password<input aria-label="Confirm password" autoComplete="new-password" type={visible ? 'text' : 'password'} value={confirm} onChange={e => setConfirm(e.target.value)} required /></label>
      {error && <div className="form-error" role="alert">{error}</div>}
      <button className="primary-button" disabled={busy}>{busy ? 'Creating account…' : 'Create account'}<Icon name="arrow" /></button>
      <p className="login-switch">Already registered? <Link to="/login">Sign in</Link></p>
    </form></section></main>
}
