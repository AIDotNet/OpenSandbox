import { useEffect, useState } from "react"
import type { FormEvent } from "react"
import { Boxes, Server, Users, HardDrive } from "lucide-react"
import { useNavigate } from "react-router-dom"
import { useData } from "../hooks/useData"
import { useAuth } from "../hooks/useAuth"
import { api } from "../services/api"
import { Card, MetricCard, EmptyState, primaryButtonClass } from "../components/ui/common"
import type { DeploymentDetail } from "../types"
import { DeploymentCard } from "../components/deployments/DeploymentCard"
import { DeploymentDialog } from "../components/deployments/DeploymentDialog"

const emptyDeployForm = {
  sandboxServerId: "",
  templateId: "",
  apiEndpoint: "",
  apiType: "chat",
  model: "",
  apiKey: "",
}

export default function DeploymentsPage() {
  const { deployments, servers, templates, loadCommonData } = useData()
  const { me } = useAuth()
  const navigate = useNavigate()

  const [dialogOpen, setDialogOpen] = useState(false)
  const [deployForm, setDeployForm] = useState(emptyDeployForm)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  const isAdmin = me?.role === "Admin"

  useEffect(() => {
    if (dialogOpen) {
      setDeployForm((prev) => ({
        ...prev,
        sandboxServerId: prev.sandboxServerId || servers[0]?.id || "",
        templateId: prev.templateId || templates[0]?.id || "",
      }))
    }
  }, [dialogOpen, servers, templates])

  async function handleDeploy(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      const detail = await api<DeploymentDetail>("/api/deployments", { method: "POST", body: JSON.stringify(deployForm) })
      setDialogOpen(false)
      await loadCommonData()
      if (detail.containerId) {
        navigate(`/containers/${encodeURIComponent(detail.containerId)}/overview`)
      }
      setMessage("部署已提交")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "部署失败")
    } finally {
      setBusy(false)
    }
  }

  async function deleteDeployment(id: string) {
    setBusy(true)
    try {
      await api(`/api/deployments/${id}`, { method: "DELETE" })
      await loadCommonData()
      setMessage("实例已删除")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除失败")
    } finally {
      setBusy(false)
    }
  }

  function openContainer(containerId: string, tab: "overview" | "logs" = "overview") {
    navigate(`/containers/${encodeURIComponent(containerId)}/${tab}`)
  }

  return (
    <div className="space-y-6">
      {message ? <div className="rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={Boxes} label="容器总数" value={String(deployments.length)} hint="卡片化展示" />
        <MetricCard icon={Server} label="所属服务端" value={String(new Set(deployments.map((item) => item.serverName).filter(Boolean)).size)} hint="按节点聚合" />
        <MetricCard icon={Users} label="涉及账号" value={String(new Set(deployments.map((item) => item.userName).filter(Boolean)).size)} hint="管理员可见全部" />
        <MetricCard icon={HardDrive} label="进入方式" value="/containers/:id" hint="容器级工作台" />
      </div>

      <Card title="部署实例" description="每个容器都以独立卡片显示，点击后进入容器工作台。" actions={<button className={primaryButtonClass} onClick={() => setDialogOpen(true)} type="button">新增部署</button>}>
        {deployments.length === 0 ? (
          <EmptyState icon={Boxes} title="暂无实例" description="提交部署后会在这里显示容器卡片。" />
        ) : (
          <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
            {deployments.map((deployment) => (
              <DeploymentCard deployment={deployment} isAdmin={!!isAdmin} key={deployment.id} onDelete={deleteDeployment} onOpen={openContainer} />
            ))}
          </div>
        )}
      </Card>

      <DeploymentDialog busy={busy} onChange={setDeployForm} onOpenChange={setDialogOpen} onSubmit={handleDeploy} open={dialogOpen} servers={servers} templates={templates} value={deployForm} />
    </div>
  )
}
