import { PortalShell } from '../components/PortalShell'
import { useAuth } from '../features/auth/AuthContext'

/** Hospital staff and camp coordinators work in the LifeLink mobile app; the web explains that instead of a dead end. */
export function WelcomePage() {
  const { user } = useAuth()
  const hospital = user?.role === 'HospitalRequester'
  return <PortalShell roleLabel={hospital ? 'Hospital staff' : 'Camp coordinator'}>
    <header className="page-header"><div><p className="eyebrow">Your account is ready</p><h1>Continue in the LifeLink app</h1><p>{hospital ? 'Register your hospital, submit blood requests and track them from the LifeLink mobile app.' : 'Camp coordination tools are available to blood-bank administrators. Ask an administrator to schedule or update your camp.'}</p></div></header>
    <section className="panel portal-card">{hospital ? <ol className="steps-list"><li><strong>Open the LifeLink mobile app</strong> and sign in with {user?.email}.</li><li><strong>Register hospital</strong> with your facility name and registration number.</li><li>A blood-bank administrator <strong>verifies your hospital</strong>; after that you can submit and escalate blood requests.</li></ol> : <p>Your coordinator account is active. A blood-bank administrator manages camp schedules, capacity and attendance in the admin workspace.</p>}</section>
  </PortalShell>
}
