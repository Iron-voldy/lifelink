import { PortalShell } from '../components/PortalShell'
import { CampsPage } from './CampsPage'

/** Camp coordinators schedule and run their own camps; the API limits the list and actions to camps they organise. */
export function WelcomePage() {
  return <PortalShell roleLabel="Camp coordinator"><CampsPage /></PortalShell>
}
