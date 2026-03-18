import { useMemo } from "react"
import { Shield, PanelLeftClose, PanelLeftOpen, LayoutDashboard, Users, Server, FileCog, Settings, Boxes, HardDrive } from "lucide-react"
import { useNavigate, useLocation } from "react-router-dom"
import { useAuth } from "../hooks/useAuth"
import { useAppMeta } from "../hooks/useAppMeta"
import { tinyButtonClass } from "../components/ui/common"

type NavItem = {
  key: string
  label: string
  description: string
  icon: typeof LayoutDashboard
  path: string
}

export function Sidebar({ collapsed, setCollapsed }: { collapsed: boolean; setCollapsed: (val: boolean) => void }) {
  const { me } = useAuth()
  const appMeta = useAppMeta()
  const navigate = useNavigate()
  const location = useLocation()
  
  const isAdmin = me?.role === "Admin"
  const APP_VERSION = "v0.0.1"

  const navItems = useMemo<NavItem[]>(() => {
    const common: NavItem[] = [{ key: "deployments", label: "部署实例", description: "容器卡片与工作台", icon: Boxes, path: "/deployments" }]
    if (!isAdmin) return common
    return [
      { key: "overview", label: "控制台", description: "总览与最近状态", icon: LayoutDashboard, path: "/overview" },
      { key: "users", label: "用户管理", description: "管理员与员工", icon: Users, path: "/users" },
      { key: "servers", label: "服务端管理", description: "节点与健康检查", icon: Server, path: "/servers" },
      { key: "templates", label: "模板中心", description: "模板与版本", icon: FileCog, path: "/templates" },
      { key: "settings", label: "系统设置", description: "默认资源与规则", icon: Settings, path: "/settings" },
      ...common,
    ]
  }, [isAdmin])

  // Simple active check
  const isActive = (path: string) => location.pathname.startsWith(path)

  return (
    <aside className={`fixed inset-y-0 left-0 z-30 hidden flex-col border-r border-border bg-card transition-all lg:flex ${collapsed ? "w-20" : "w-72"}`}>
      <div className="border-b border-border px-4 py-5">
        <div className={`flex items-center ${collapsed ? "justify-center" : "justify-between gap-3"}`}>
          <div className={`flex items-center ${collapsed ? "justify-center" : "gap-3"}`}>
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl border border-border bg-muted text-foreground"><Shield className="h-6 w-6" /></div>
            {!collapsed ? (
              <div>
                <div className="text-base font-semibold tracking-tight">OpenClaw</div>
                <div className="text-xs text-muted-foreground">Sandbox Control Panel</div>
              </div>
            ) : null}
          </div>
          {!collapsed ? <button className={tinyButtonClass} onClick={() => setCollapsed(true)} type="button"><PanelLeftClose className="h-4 w-4" /></button> : null}
        </div>
      </div>

      <div className={`flex-1 overflow-y-auto ${collapsed ? "p-2" : "p-3"}`}>
        {!collapsed ? <div className="mb-3 px-3 text-[11px] uppercase tracking-[0.2em] text-muted-foreground">导航</div> : null}
        <nav className="space-y-1">
          {navItems.map((item) => (
            <SidebarItem 
              key={item.key} 
              active={isActive(item.path) && !location.pathname.startsWith("/containers")} 
              collapsed={collapsed} 
              description={item.description} 
              icon={item.icon} 
              label={item.label} 
              onClick={() => navigate(item.path)} 
            />
          ))}
          {location.pathname.startsWith("/containers") ? (
            <SidebarItem 
              active 
              collapsed={collapsed} 
              description="当前容器" 
              icon={HardDrive} 
              label="容器工作台" 
              onClick={() => {}} 
            />
          ) : null}
        </nav>
      </div>

      <div className={`border-t border-border py-4 ${collapsed ? "px-2" : "px-5"}`}>
        {collapsed ? (
          <div className="flex flex-col items-center gap-2">
            <button className={tinyButtonClass} onClick={() => setCollapsed(false)} type="button"><PanelLeftOpen className="h-4 w-4" /></button>
            <div className="text-center text-[10px] text-muted-foreground">{appMeta?.version ?? APP_VERSION}</div>
          </div>
        ) : (
          <>
            <div className="text-[11px] uppercase tracking-[0.2em] text-muted-foreground">Version</div>
            <div className="mt-2 text-sm font-medium">{appMeta?.version ?? APP_VERSION}</div>
          </>
        )}
      </div>
    </aside>
  )
}

function SidebarItem(props: { active: boolean; collapsed: boolean; label: string; description: string; icon: typeof LayoutDashboard; onClick: () => void }) {
  const Icon = props.icon
  return (
    <button className={`w-full rounded-2xl px-3 py-3 text-left transition ${props.active ? "bg-accent text-accent-foreground ring-1 ring-inset ring-border" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`} onClick={props.onClick} title={props.collapsed ? props.label : undefined} type="button">
      <div className={`flex ${props.collapsed ? "justify-center" : "items-start gap-3"}`}>
        <div className={`mt-0.5 flex h-9 w-9 items-center justify-center rounded-xl ${props.active ? "bg-background text-foreground" : "bg-muted text-muted-foreground"}`}><Icon className="h-4 w-4" /></div>
        {!props.collapsed ? (
          <div className="min-w-0">
            <div className="text-sm font-medium">{props.label}</div>
            <div className="mt-1 text-xs text-muted-foreground">{props.description}</div>
          </div>
        ) : null}
      </div>
    </button>
  )
}
