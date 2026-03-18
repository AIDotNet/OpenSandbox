import { LogOut, PanelLeftClose, PanelLeftOpen } from "lucide-react"
import { useAuth } from "../hooks/useAuth"
import { useData } from "../hooks/useData"
import { tinyButtonClass, secondaryButtonClass } from "./ui/common"
import { useLocation } from "react-router-dom"

export function Header({ collapsed, setCollapsed }: { collapsed: boolean; setCollapsed: (val: boolean) => void }) {
  const { me, logout } = useAuth()
  const { refreshAll } = useData()
  const location = useLocation()

  const isAdmin = me?.role === "Admin"

  function getPageMeta() {
    const path = location.pathname
    if (path.startsWith("/containers")) {
      return { title: "容器工作台", description: "容器级概况、日志、终端与文件管理。" }
    }
    if (path.startsWith("/users")) return { title: "用户管理", description: "菜单独立路由，账号维护集中在当前页面。" }
    if (path.startsWith("/servers")) return { title: "沙盒服务端", description: "节点列表为主，服务端状态由独立页面维护。" }
    if (path.startsWith("/templates")) return { title: "模板与版本", description: "模板列表 + 版本历史的双栏布局。" }
    if (path.startsWith("/settings")) return { title: "系统设置", description: "维护默认资源与系统规则。" }
    if (path.startsWith("/deployments")) return { title: "部署实例", description: "卡片化展示所有容器，点击进入容器级工作台。" }
    
    return { title: "控制台概览", description: "集中查看用户、服务端、模板和实例状态。" }
  }

  const pageMeta = getPageMeta()

  return (
    <header className="sticky top-0 z-20 border-b border-border bg-background/90 px-6 py-4 backdrop-blur">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-start gap-3">
          <button className={`${tinyButtonClass} hidden lg:inline-flex`} onClick={() => setCollapsed(!collapsed)} type="button">
            {collapsed ? <PanelLeftOpen className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
          </button>
          <div>
            <div className="text-[11px] uppercase tracking-[0.22em] text-muted-foreground">
              {location.pathname.startsWith("/containers") ? "容器级工作台" : isAdmin ? "管理端" : "员工端"}
            </div>
            <div className="mt-1 text-2xl font-semibold tracking-tight">{pageMeta.title}</div>
            <div className="mt-1 text-sm text-muted-foreground">{pageMeta.description}</div>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="rounded-2xl border border-border bg-background px-4 py-2 text-right">
            <div className="text-sm font-medium">{me?.displayName}</div>
            <div className="text-xs text-muted-foreground">{me?.userName} · {me?.role}</div>
          </div>
          <button className={secondaryButtonClass} onClick={() => void refreshAll()} type="button">刷新</button>
          <button className={secondaryButtonClass} onClick={() => void logout()} type="button"><LogOut className="mr-2 h-4 w-4" />退出</button>
        </div>
      </div>
    </header>
  )
}
