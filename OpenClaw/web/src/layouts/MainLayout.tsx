import { useState, useEffect } from "react"
import { Outlet, Navigate, useLocation } from "react-router-dom"
import { useAuth } from "../hooks/useAuth"
import { useData } from "../hooks/useData"
import { Sidebar } from "../components/Sidebar"
import { Header } from "../components/Header"

export function MainLayout() {
  const { me, loading } = useAuth()
  const { loadCommonData, loadAdminData } = useData()
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const location = useLocation()

  useEffect(() => {
    const saved = window.localStorage.getItem("openclaw.sidebar.collapsed")
    setSidebarCollapsed(saved === "1")
  }, [])

  useEffect(() => {
    window.localStorage.setItem("openclaw.sidebar.collapsed", sidebarCollapsed ? "1" : "0")
  }, [sidebarCollapsed])

  useEffect(() => {
    if (!me) return
    void loadCommonData()
    if (me.role === "Admin") void loadAdminData()
  }, [me, loadCommonData, loadAdminData])

  if (loading) {
    return <div className="flex min-h-svh items-center justify-center text-sm text-muted-foreground">加载中...</div>
  }

  if (!me) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  return (
    <div className={`min-h-svh bg-background text-foreground ${sidebarCollapsed ? "lg:pl-20" : "lg:pl-72"}`}>
      <Sidebar collapsed={sidebarCollapsed} setCollapsed={setSidebarCollapsed} />
      <div className="flex min-h-svh min-w-0 flex-col">
        <Header collapsed={sidebarCollapsed} setCollapsed={setSidebarCollapsed} />
        <main className="flex-1 overflow-y-auto p-6">
          <div className="mx-auto max-w-[1600px] space-y-6">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
