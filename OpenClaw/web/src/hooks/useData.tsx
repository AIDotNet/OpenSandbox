import { createContext, useContext, useState, useCallback } from "react"
import type { ReactNode } from "react"
import type { AdminUser, Deployment, SandboxServer, SystemSettings, Template } from "../types"
import { api } from "../services/api"
import { useAuth } from "./useAuth"

type DataContextType = {
  users: AdminUser[]
  servers: SandboxServer[]
  templates: Template[]
  settings: SystemSettings | null
  deployments: Deployment[]
  loading: boolean
  error: string | null
  loadCommonData: () => Promise<void>
  loadAdminData: () => Promise<void>
  refreshAll: () => Promise<void>
  setError: (error: string | null) => void
}

const DataContext = createContext<DataContextType | undefined>(undefined)

export function DataProvider({ children }: { children: ReactNode }) {
  const { me } = useAuth()
  const [users, setUsers] = useState<AdminUser[]>([])
  const [servers, setServers] = useState<SandboxServer[]>([])
  const [templates, setTemplates] = useState<Template[]>([])
  const [settings, setSettings] = useState<SystemSettings | null>(null)
  const [deployments, setDeployments] = useState<Deployment[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadCommonData = useCallback(async () => {
    try {
      const [nextServers, nextTemplates, nextDeployments] = await Promise.all([
        api<SandboxServer[]>("/api/sandbox-servers"),
        api<Template[]>("/api/templates"),
        api<Deployment[]>("/api/deployments"),
      ])
      setServers(nextServers)
      setTemplates(nextTemplates)
      setDeployments(nextDeployments)
    } catch (err) {
      setError(err instanceof Error ? err.message : "加载公共数据失败")
    }
  }, [])

  const loadAdminData = useCallback(async () => {
    if (me?.role !== "Admin") return
    try {
      const [nextUsers, nextServers, nextTemplates, nextSettings] = await Promise.all([
        api<AdminUser[]>("/api/admin/users"),
        api<SandboxServer[]>("/api/admin/sandbox-servers"),
        api<Template[]>("/api/admin/templates"),
        api<SystemSettings>("/api/admin/settings"),
      ])
      setUsers(nextUsers)
      setServers(nextServers)
      setTemplates(nextTemplates)
      setSettings(nextSettings)
    } catch (err) {
      setError(err instanceof Error ? err.message : "加载管理数据失败")
    }
  }, [me?.role])

  const refreshAll = useCallback(async () => {
    setLoading(true)
    setError(null)
    await Promise.all([loadCommonData(), loadAdminData()])
    setLoading(false)
  }, [loadCommonData, loadAdminData])

  return (
    <DataContext.Provider value={{
      users, servers, templates, settings, deployments,
      loading, error, loadCommonData, loadAdminData, refreshAll, setError
    }}>
      {children}
    </DataContext.Provider>
  )
}

export function useData() {
  const context = useContext(DataContext)
  if (context === undefined) {
    throw new Error("useData must be used within a DataProvider")
  }
  return context
}
