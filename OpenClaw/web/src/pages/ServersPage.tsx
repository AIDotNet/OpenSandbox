import { useState } from "react"
import type { FormEvent } from "react"
import { Server, SearchCheck, Network } from "lucide-react"
import { useData } from "../hooks/useData"
import { api } from "../services/api"
import { Card, MetricCard, TableWrap, Th, Td, StatusBadge, EmptyState, primaryButtonClass, tinyButtonClass, secondaryButtonClass, inputClass } from "../components/ui/common"
import { formatTime, healthTone } from "../lib/utils"
import type { SandboxServer } from "../types"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"

const emptyServerForm = { name: "", baseUrl: "", apiToken: "", persistentRootPath: "", isEnabled: true }

export default function ServersPage() {
  const { servers, loadAdminData, loadCommonData } = useData()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [serverForm, setServerForm] = useState(emptyServerForm)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  const stats = {
    servers: servers.length,
    healthyServers: servers.filter((item: SandboxServer) => item.healthStatus === "Healthy").length,
  }

  async function handleCreateServer(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      await api("/api/admin/sandbox-servers", { method: "POST", body: JSON.stringify(serverForm) })
      setServerForm(emptyServerForm)
      setDialogOpen(false)
      await Promise.all([loadAdminData(), loadCommonData()])
      setMessage("沙盒服务端已保存")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "保存失败")
    } finally {
      setBusy(false)
    }
  }

  async function refreshServerHealth(id: string) {
    setBusy(true)
    try {
      await api(`/api/admin/sandbox-servers/${id}/health`, { method: "POST" })
      await Promise.all([loadAdminData(), loadCommonData()])
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "检测失败")
    } finally {
      setBusy(false)
    }
  }

  async function deleteServer(id: string) {
    setBusy(true)
    try {
      await api(`/api/admin/sandbox-servers/${id}`, { method: "DELETE" })
      await Promise.all([loadAdminData(), loadCommonData()])
      setMessage("服务端已删除")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除失败")
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-6">
      {message ? <div className="rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}
      
      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard icon={Server} label="节点总数" value={String(stats.servers)} hint="已录入服务端" />
        <MetricCard icon={SearchCheck} label="健康节点" value={String(stats.healthyServers)} hint="健康检查通过" />
        <MetricCard icon={Network} label="接入方式" value="BaseUrl" hint="Token 授权 + 轮询健康" />
      </div>

      <Card title="服务端目录" description="每个菜单都有独立路由，节点页专注做节点管理。" actions={<button className={primaryButtonClass} onClick={() => setDialogOpen(true)} type="button">新增服务端</button>}>
        {servers.length === 0 ? (
          <EmptyState icon={Server} title="还没有服务端" description="新增后会自动参与健康轮询。" />
        ) : (
          <TableWrap>
            <table className="min-w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <Th>名称</Th>
                  <Th>BaseUrl</Th>
                  <Th>状态</Th>
                  <Th>最近检查</Th>
                  <Th>消息</Th>
                  <Th className="text-right">操作</Th>
                </tr>
              </thead>
              <tbody>
                {servers.map((server: SandboxServer) => (
                  <tr className="border-t border-border" key={server.id}>
                    <Td>{server.name}</Td>
                    <Td>{server.baseUrl || "-"}</Td>
                    <Td><StatusBadge label={server.healthStatus} tone={healthTone(server.healthStatus)} /></Td>
                    <Td>{formatTime(server.lastCheckedAt)}</Td>
                    <Td>{server.lastHealthMessage || "-"}</Td>
                    <Td className="text-right">
                      <div className="flex justify-end gap-2">
                        <button className={tinyButtonClass} onClick={() => void refreshServerHealth(server.id)} type="button">检测</button>
                        <button className={tinyButtonClass} onClick={() => void deleteServer(server.id)} type="button">删除</button>
                      </div>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableWrap>
        )}
      </Card>

      <Dialog onOpenChange={setDialogOpen} open={dialogOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>新增服务端</DialogTitle>
            <DialogDescription>录入服务端名称、BaseUrl、Token 与持久化根目录。</DialogDescription>
          </DialogHeader>
          <form className="grid gap-3" onSubmit={handleCreateServer}>
            <input className={inputClass} placeholder="名称" value={serverForm.name} onChange={(event) => setServerForm((value) => ({ ...value, name: event.target.value }))} />
            <input className={inputClass} placeholder="BaseUrl" value={serverForm.baseUrl} onChange={(event) => setServerForm((value) => ({ ...value, baseUrl: event.target.value }))} />
            <input className={inputClass} placeholder="Token" value={serverForm.apiToken} onChange={(event) => setServerForm((value) => ({ ...value, apiToken: event.target.value }))} />
            <input className={inputClass} placeholder="持久化根目录" value={serverForm.persistentRootPath} onChange={(event) => setServerForm((value) => ({ ...value, persistentRootPath: event.target.value }))} />
            <label className="inline-flex items-center gap-2 rounded-xl border border-border bg-muted/30 px-3 py-3 text-sm">
              <input checked={serverForm.isEnabled} type="checkbox" onChange={(event) => setServerForm((value) => ({ ...value, isEnabled: event.target.checked }))} />启用该服务端
            </label>
            <DialogFooter>
              <button className={secondaryButtonClass} onClick={() => setDialogOpen(false)} type="button">取消</button>
              <button className={primaryButtonClass} disabled={busy} type="submit">保存服务端</button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
