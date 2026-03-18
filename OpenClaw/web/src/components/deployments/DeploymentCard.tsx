import type { Deployment } from "../../types"
import { StatusBadge, primaryButtonClass, secondaryButtonClass, tinyButtonClass } from "../ui/common"
import { formatTime, shortId } from "../../lib/utils"

type DeploymentCardProps = {
  deployment: Deployment
  isAdmin: boolean
  onOpen: (containerId: string, tab?: "overview" | "logs") => void
  onDelete: (deploymentId: string) => void
}

export function DeploymentCard({ deployment, isAdmin, onOpen, onDelete }: DeploymentCardProps) {
  return (
    <article className="rounded-3xl border border-border bg-card p-5 shadow-sm transition hover:border-ring/50 hover:bg-accent/20">
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="text-lg font-semibold tracking-tight">{deployment.model || "未命名实例"}</div>
          <div className="text-sm text-muted-foreground">{deployment.serverName || "未知服务端"}</div>
        </div>
        <StatusBadge label={deployment.apiType || "chat"} tone="neutral" />
      </div>
      <div className="mt-4 space-y-2 text-sm text-muted-foreground">
        <div className="truncate">Endpoint：{deployment.apiEndpoint || "-"}</div>
        <div>容器 ID：{deployment.containerId ? shortId(deployment.containerId) : "未分配"}</div>
        {isAdmin ? <div>用户：{deployment.userName || "-"}</div> : null}
        <div>更新时间：{formatTime(deployment.updatedAt)}</div>
      </div>
      <div className="mt-5 flex flex-wrap gap-2">
        <button className={primaryButtonClass} disabled={!deployment.containerId} onClick={() => deployment.containerId && onOpen(deployment.containerId, "overview")} type="button">进入容器</button>
        <button className={secondaryButtonClass} disabled={!deployment.containerId} onClick={() => deployment.containerId && onOpen(deployment.containerId, "logs")} type="button">查看日志</button>
        <button className={tinyButtonClass} onClick={() => onDelete(deployment.id)} type="button">删除</button>
      </div>
    </article>
  )
}
