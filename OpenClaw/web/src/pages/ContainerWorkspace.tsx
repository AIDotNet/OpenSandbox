import { useEffect, useState } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { MonitorCog } from "lucide-react"
import { api } from "../services/api"
import { useData } from "../hooks/useData"
import type { ContainerTab, Deployment, DeploymentDetail } from "../types"
import { Card, DetailCard, EmptyState, StatusBadge, secondaryButtonClass, tinyButtonClass } from "../components/ui/common"
import { ContainerRouteTabs } from "../components/container-workspace/ContainerRouteTabs"
import { ContainerOverviewPanel } from "../components/container-workspace/ContainerOverviewPanel"
import { ContainerLogsPanel } from "../components/container-workspace/ContainerLogsPanel"
import { ContainerTerminalPanel } from "../components/container-workspace/ContainerTerminalPanel"
import { ContainerFilesPanel } from "../components/container-workspace/ContainerFilesPanel"

export default function ContainerWorkspace() {
  const { containerId = "", "*": tabPath = "overview" } = useParams()
  const navigate = useNavigate()
  const { deployments, loadCommonData } = useData()

  const tab = normalizeTab(tabPath)
  const [containerDetail, setContainerDetail] = useState<DeploymentDetail | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  const selectedDeployment = deployments.find((item: Deployment) => item.containerId === containerId) ?? null

  useEffect(() => {
    if (containerId) {
      void loadContainerDetail(containerId)
    }
  }, [containerId])

  async function loadContainerDetail(id: string) {
    try {
      const detail = await api<DeploymentDetail>(`/api/containers/${encodeURIComponent(id)}`)
      setContainerDetail(detail)
      setMessage("")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "加载容器详情失败")
    }
  }

  async function deleteDeployment(id: string) {
    setBusy(true)
    try {
      await api(`/api/deployments/${id}`, { method: "DELETE" })
      await loadCommonData()
      navigate("/deployments")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除实例失败")
    } finally {
      setBusy(false)
    }
  }

  function changeTab(nextTab: ContainerTab) {
    navigate(`/containers/${encodeURIComponent(containerId)}/${nextTab}`)
  }

  if (!containerDetail) {
    return <EmptyState icon={MonitorCog} title="正在加载容器" description="容器详情加载后，会展示概况、日志、终端和文件管理。" />
  }

  return (
    <div className="space-y-6">
      {message ? <div className="rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}

      <div className="rounded-3xl border border-border bg-card p-6 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="text-xs uppercase tracking-[0.22em] text-muted-foreground">容器工作台</div>
            <div className="mt-2 flex flex-wrap items-center gap-3">
              <h2 className="text-2xl font-semibold tracking-tight">{containerDetail.configSummary?.model || "容器详情"}</h2>
              <StatusBadge label={containerDetail.status || "未知"} tone={containerDetail.status === "Running" ? "success" : "neutral"} />
            </div>
            <div className="mt-2 text-sm text-muted-foreground">路径：/containers/{containerId}/{tab}</div>
          </div>
          <div className="flex flex-wrap gap-2">
            <button className={secondaryButtonClass} onClick={() => void loadContainerDetail(containerId)} type="button">刷新概况</button>
            {selectedDeployment ? <button className={tinyButtonClass} disabled={busy} onClick={() => void deleteDeployment(selectedDeployment.id)} type="button">删除实例</button> : null}
          </div>
        </div>

        <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <DetailCard label="容器 ID" value={containerDetail.containerId || "-"} />
          <DetailCard label="服务端" value={containerDetail.server?.name || "-"} />
          <DetailCard label="CPU" value={containerDetail.cpuPercent != null ? `${containerDetail.cpuPercent}%` : "-"} />
          <DetailCard label="内存" value={containerDetail.memoryPercent != null ? `${containerDetail.memoryPercent}%` : "-"} />
        </div>
      </div>

      <ContainerRouteTabs activeTab={tab} onChange={changeTab} />

      {tab === "overview" ? <ContainerOverviewPanel detail={containerDetail} /> : null}
      {tab === "logs" ? <ContainerLogsPanel containerId={containerId} /> : null}
      {tab === "terminal" ? <ContainerTerminalPanel containerId={containerId} /> : null}
      {tab === "files" ? <ContainerFilesPanel containerId={containerId} /> : null}

      <Card title="模板快照" description="保留实例部署时的模板快照，便于核对运行配置。">
        {containerDetail.templateSnapshot ? (
          <pre className="max-h-[28rem] overflow-auto rounded-2xl border border-border bg-muted/20 p-4 text-xs text-foreground">{JSON.stringify(containerDetail.templateSnapshot, null, 2)}</pre>
        ) : (
          <div className="rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-8 text-sm text-muted-foreground">当前实例还没有可展示的模板快照。</div>
        )}
      </Card>
    </div>
  )
}

function normalizeTab(value: string): ContainerTab {
  return ["overview", "logs", "terminal", "files"].includes(value) ? (value as ContainerTab) : "overview"
}
