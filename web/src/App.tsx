import { Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './features/auth/ProtectedRoute'
import { AppLayout } from './components/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { DashboardPage } from './pages/DashboardPage'
import { DonorsPage } from './pages/DonorsPage'
import { RequestsPage } from './pages/RequestsPage'
import { InventoryPage } from './pages/InventoryPage'
import { CampsPage } from './pages/CampsPage'
import { WorkflowsPage } from './pages/WorkflowsPage'
import { HospitalsPage } from './pages/HospitalsPage'
import { DonorPortalPage } from './pages/DonorPortalPage'
import { WelcomePage } from './pages/WelcomePage'

export function App() {
  return <Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route path="/register" element={<RegisterPage />} />
    <Route element={<ProtectedRoute roles={['BloodBankAdmin']} />}>
      <Route element={<AppLayout />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="donors" element={<DonorsPage />} />
        <Route path="hospitals" element={<HospitalsPage />} />
        <Route path="requests" element={<RequestsPage />} />
        <Route path="inventory" element={<InventoryPage />} />
        <Route path="camps" element={<CampsPage />} />
        <Route path="workflows" element={<WorkflowsPage />} />
      </Route>
    </Route>
    <Route element={<ProtectedRoute roles={['Donor']} />}><Route path="donor" element={<DonorPortalPage />} /></Route>
    <Route element={<ProtectedRoute roles={['HospitalRequester', 'CampCoordinator']} />}><Route path="welcome" element={<WelcomePage />} /></Route>
    <Route path="*" element={<Navigate to="/dashboard" replace />} />
  </Routes>
}
