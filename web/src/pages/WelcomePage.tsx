import { PortalShell } from '../components/PortalShell'

/** Camp coordinators have no self-service tools yet; the web explains that instead of a dead end. */
export function WelcomePage() {
  return <PortalShell roleLabel="Camp coordinator">
    <header className="page-header"><div><p className="eyebrow">Your account is ready</p><h1>Camp coordination</h1><p>Camp coordination tools are available to blood-bank administrators. Ask an administrator to schedule or update your camp.</p></div></header>
    <section className="panel portal-card"><p>Your coordinator account is active. A blood-bank administrator manages camp schedules, capacity and attendance in the admin workspace.</p></section>
  </PortalShell>
}
