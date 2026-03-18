import { useMemo } from "react"
import { Users, Server, FileCog, Boxes, Network } from "lucide-react"
import { useNavigate } from "react-router-dom"
import { useData } from "../hooks/useData"
import { Card, MetricCard, EmptyState, StatusBadge } from "../components/ui/common"
import { formatTime, healthTone, shortId } from "../lib/utils"
import type { AdminUser, Deployment, SandboxServer } from "../types"

export default function OverviewPage() {
  const { users, servers, templates, deployments } = useData()
  const navigate = useNavigate()

  const stats = useMemo(
    () => ({
      users: users.length,
      servers: servers.length,
      templates: templates.length,
      deployments: deployments.length,
      healthyServers: servers.filter((item: SandboxServer) => item.healthStatus === "Healthy").length,
      activeUsers: users.filter((item: AdminUser) => item.status === "Active").length,
    }),
    [deployments.length, servers, templates.length, users],
  )

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={Users} label="用户总数" value={String(stats.users)} hint={`启用 ${stats.activeUsers}`} />
        <MetricCard icon={Server} label="服务端总数" value={String(stats.servers)} hint={`健康 ${stats.healthyServers}`} />
        <MetricCard icon={FileCog} label="模板总数" value={String(stats.templates)} hint="含版本快照" />
        <MetricCard icon={Boxes} label="实例总数" value={String(stats.deployments)} hint="按服务端唯一实例" />
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <Card title="最近实例" description="点击容器卡片进入独立工作台。">
          <div className="space-y-3">
            {deployments.length === 0 ? (
              <EmptyState icon={Boxes} title="还没有实例" description="先创建服务端和模板，再进行部署。" />
            ) : (
              deployments.slice(0, 6).map((deployment: Deployment) => (
                <button className="w-full rounded-2xl border border-border bg-background p-4 text-left transition hover:bg-accent/40" key={deployment.id} onClick={() => deployment.containerId && navigate(`/containers/${encodeURIComponent(deployment.containerId)}/overview`)} type="button">
                  <div className="flex items-center justify-between gap-3">
                    <div className="font-medium">{deployment.serverName} · {deployment.model || "未命名实例"}</div>
                    <StatusBadge label={deployment.containerId ? shortId(deployment.containerId) : "未分配"} tone="brand" />
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">{deployment.apiEndpoint || "-"}</div>
                  <div className="mt-3 text-xs text-muted-foreground">更新时间：{formatTime(deployment.updatedAt)}</div>
                </button>
              ))
            )}
          </div>
        </Card>

        <Card title="服务端健康" description="查看所有已启用节点的当前状态。">
          <div className="space-y-3">
            {servers.length === 0 ? (
              <EmptyState icon={Network} title="还没有服务端" description="录入 BaseUrl 和 Token 后即可接入。" />
            ) : (
              servers.map((server: SandboxServer) => (
                <div className="rounded-2xl border border-border bg-background p-4" key={server.id}>
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-medium">{server.name}</div>
                      <div className="mt-1 text-xs text-muted-foreground">{server.baseUrl ?? "员工视图"}</div>
                    </div>
                    <StatusBadge label={server.healthStatus} tone={healthTone(server.healthStatus)} />
                  </div>
                  <div className="mt-3 text-xs text-muted-foreground">{server.lastHealthMessage || "尚未检查"}</div>
                </div>
              ))
            )}
          </div>
        </Card>
      </div>
    </div>
  )
}
