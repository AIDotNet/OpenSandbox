import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom"
import { AuthProvider } from "./hooks/useAuth"
import { DataProvider } from "./hooks/useData"
import { MainLayout } from "./layouts/MainLayout"
import LoginPage from "./pages/LoginPage"
import OverviewPage from "./pages/OverviewPage"
import UsersPage from "./pages/UsersPage"
import ServersPage from "./pages/ServersPage"
import TemplatesPage from "./pages/TemplatesPage"
import SettingsPage from "./pages/SettingsPage"
import DeploymentsPage from "./pages/DeploymentsPage"
import ContainerWorkspace from "./pages/ContainerWorkspace"

export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <DataProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<MainLayout />}>
              <Route index element={<Navigate to="/overview" replace />} />
              <Route path="overview" element={<OverviewPage />} />
              <Route path="users" element={<UsersPage />} />
              <Route path="servers" element={<ServersPage />} />
              <Route path="templates" element={<TemplatesPage />} />
              <Route path="settings" element={<SettingsPage />} />
              <Route path="deployments" element={<DeploymentsPage />} />
              <Route path="containers/:containerId/*" element={<ContainerWorkspace />} />
              <Route path="*" element={<Navigate to="/overview" replace />} />
            </Route>
          </Routes>
        </DataProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App
