import { useState, useMemo } from "react"
import type { FormEvent } from "react"
import { FileCog, SearchCheck } from "lucide-react"
import { useData } from "../hooks/useData"
import { api } from "../services/api"
import { Card, TableWrap, Th, Td, StatusBadge, EmptyState, primaryButtonClass, secondaryButtonClass, inputClass, textareaClass } from "../components/ui/common"
import { formatTime, splitLines } from "../lib/utils"
import type { Template, TemplateVersion } from "../types"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"

const emptyTemplateForm = { name: "", description: "", isEnabled: true }
const emptyVersionForm = {
  templateId: "",
  version: "v1",
  image: "ghcr.io/openclaw/openclaw:latest",
  containerPort: 18789,
  command: "",
  configMountPath: "/home/node/.openclaw",
  configFileName: "openclaw.json",
  workspaceMountPath: "/home/node/.openclaw/workspace",
  isActive: true,
}

export default function TemplatesPage() {
  const { templates, loadAdminData, loadCommonData } = useData()
  const [selectedTemplateId, setSelectedTemplateId] = useState("")
  const [dialogs, setDialogs] = useState({ template: false, version: false })
  const [templateForm, setTemplateForm] = useState(emptyTemplateForm)
  const [versionForm, setVersionForm] = useState(emptyVersionForm)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  const selectedTemplate = useMemo(
    () => templates.find((item: Template) => item.id === selectedTemplateId) ?? templates[0] ?? null,
    [selectedTemplateId, templates],
  )
  const selectedVersions = selectedTemplate?.versions ?? []

  async function handleCreateTemplate(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      await api("/api/admin/templates", { method: "POST", body: JSON.stringify(templateForm) })
      setTemplateForm(emptyTemplateForm)
      setDialogs(prev => ({ ...prev, template: false }))
      await Promise.all([loadAdminData(), loadCommonData()])
      setMessage("模板已创建")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "创建失败")
    } finally {
      setBusy(false)
    }
  }

  async function handlePublishVersion(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      await api(`/api/admin/templates/${versionForm.templateId}/versions`, {
        method: "POST",
        body: JSON.stringify({
          version: versionForm.version,
          image: versionForm.image,
          containerPort: versionForm.containerPort,
          command: splitLines(versionForm.command),
          configMountPath: versionForm.configMountPath,
          configFileName: versionForm.configFileName,
          workspaceMountPath: versionForm.workspaceMountPath,
          isActive: versionForm.isActive,
        }),
      })
      setDialogs(prev => ({ ...prev, version: false }))
      await Promise.all([loadAdminData(), loadCommonData()])
      setMessage("模板版本已发布")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "发布失败")
    } finally {
      setBusy(false)
    }
  }

  async function deleteTemplate(id: string) {
    setBusy(true)
    try {
      await api(`/api/admin/templates/${id}`, { method: "DELETE" })
      await Promise.all([loadAdminData(), loadCommonData()])
      setMessage("模板已删除")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除失败")
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-6">
      {message ? <div className="rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}

      <div className="grid gap-6 xl:grid-cols-[0.82fr_1.18fr]">
        <Card
          title="模板目录"
          description="左侧选择模板，右侧查看版本历史。"
          actions={
            <>
              <button className={secondaryButtonClass} onClick={() => {
                 setVersionForm(prev => ({ ...prev, templateId: selectedTemplate?.id || templates[0]?.id || "" }))
                 setDialogs(prev => ({ ...prev, version: true }))
              }} type="button">发布版本</button>
              <button className={primaryButtonClass} onClick={() => setDialogs(prev => ({ ...prev, template: true }))} type="button">新增模板</button>
            </>
          }
        >
          <div className="space-y-3">
            {templates.length === 0 ? (
              <EmptyState icon={FileCog} title="还没有模板" description="先创建模板，再发布首个版本。" />
            ) : (
              templates.map((template: Template) => {
                const active = selectedTemplate?.id === template.id
                return (
                  <button className={`w-full rounded-2xl border p-4 text-left transition ${active ? "border-ring bg-accent/50" : "border-border bg-background hover:bg-accent/40"}`} key={template.id} onClick={() => setSelectedTemplateId(template.id)} type="button">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <div className="font-medium">{template.name}</div>
                          {template.currentVersionId ? <StatusBadge label="Current" tone="brand" /> : null}
                        </div>
                        <div className="mt-1 text-sm text-muted-foreground">{template.description}</div>
                        <div className="mt-2 text-xs text-muted-foreground">版本数：{template.versions?.length ?? 0}</div>
                      </div>
                      <span className="text-xs text-muted-foreground">查看</span>
                    </div>
                  </button>
                )
              })
            )}
          </div>
        </Card>

        <Card title={selectedTemplate ? `版本历史 · ${selectedTemplate.name}` : "版本历史"} description="右侧聚焦当前模板的版本历史。" actions={selectedTemplate ? <button className={secondaryButtonClass} onClick={() => void deleteTemplate(selectedTemplate.id)} type="button">删除模板</button> : null}>
          {selectedVersions.length === 0 ? (
            <EmptyState icon={SearchCheck} title="未选择模板" description="选择模板后可在这里查看版本历史。" />
          ) : (
            <TableWrap>
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50">
                  <tr>
                    <Th>版本</Th>
                    <Th>镜像</Th>
                    <Th>端口</Th>
                    <Th>状态</Th>
                    <Th>发布时间</Th>
                  </tr>
                </thead>
                <tbody>
                  {selectedVersions.map((version: TemplateVersion) => (
                    <tr className="border-t border-border" key={version.id}>
                      <Td>{version.version}</Td>
                      <Td>{version.image}</Td>
                      <Td>{version.containerPort}</Td>
                      <Td><StatusBadge label={version.isActive ? "Active" : "Inactive"} tone={version.isActive ? "success" : "neutral"} /></Td>
                      <Td>{formatTime(version.createdAt)}</Td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TableWrap>
          )}
        </Card>
      </div>

      <Dialog onOpenChange={(open) => setDialogs(prev => ({ ...prev, template: open }))} open={dialogs.template}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>新增模板</DialogTitle>
            <DialogDescription>创建模板基础信息。</DialogDescription>
          </DialogHeader>
          <form className="grid gap-3" onSubmit={handleCreateTemplate}>
            <input className={inputClass} placeholder="模板名称" value={templateForm.name} onChange={(event) => setTemplateForm((value) => ({ ...value, name: event.target.value }))} />
            <input className={inputClass} placeholder="模板说明" value={templateForm.description} onChange={(event) => setTemplateForm((value) => ({ ...value, description: event.target.value }))} />
            <DialogFooter>
              <button className={secondaryButtonClass} onClick={() => setDialogs(prev => ({ ...prev, template: false }))} type="button">取消</button>
              <button className={primaryButtonClass} disabled={busy} type="submit">创建模板</button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog onOpenChange={(open) => setDialogs(prev => ({ ...prev, version: open }))} open={dialogs.version}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>发布模板版本</DialogTitle>
            <DialogDescription>参考 OpenClaw 官方 Docker 文档填写镜像、端口与挂载目录。</DialogDescription>
          </DialogHeader>
          <form className="grid gap-3" onSubmit={handlePublishVersion}>
            <div className="rounded-2xl border border-border bg-muted/30 p-4 text-sm text-muted-foreground">
              <div className="font-medium text-foreground">官方 Docker 文档默认值</div>
              <div className="mt-2 grid gap-1">
                <div>镜像：`ghcr.io/openclaw/openclaw:latest`</div>
                <div>端口：`18789`</div>
                <div>OpenClaw Home：`/home/node/.openclaw`</div>
                <div>Workspace：`/home/node/.openclaw/workspace`</div>
              </div>
            </div>
            <select className={inputClass} value={versionForm.templateId} onChange={(event) => setVersionForm((value) => ({ ...value, templateId: event.target.value }))}>
              <option value="">选择模板</option>
              {templates.map((template: Template) => <option key={template.id} value={template.id}>{template.name}</option>)}
            </select>
            <div className="grid gap-3 md:grid-cols-2">
              <input className={inputClass} placeholder="版本号，例如 v1" value={versionForm.version} onChange={(event) => setVersionForm((value) => ({ ...value, version: event.target.value }))} />
              <input className={inputClass} placeholder="容器端口，官方默认 18789" type="number" value={versionForm.containerPort} onChange={(event) => setVersionForm((value) => ({ ...value, containerPort: Number(event.target.value) || 0 }))} />
            </div>
            <input className={inputClass} placeholder="镜像，官方默认 ghcr.io/openclaw/openclaw:latest" value={versionForm.image} onChange={(event) => setVersionForm((value) => ({ ...value, image: event.target.value }))} />
            <div className="grid gap-3 md:grid-cols-2">
              <input className={inputClass} placeholder="OpenClaw Home 挂载目录，官方默认 /home/node/.openclaw" value={versionForm.configMountPath} onChange={(event) => setVersionForm((value) => ({ ...value, configMountPath: event.target.value }))} />
              <input className={inputClass} placeholder="配置文件名（当前控制面生成）" value={versionForm.configFileName} onChange={(event) => setVersionForm((value) => ({ ...value, configFileName: event.target.value }))} />
            </div>
            <input className={inputClass} placeholder="Workspace 挂载目录，官方默认 /home/node/.openclaw/workspace" value={versionForm.workspaceMountPath} onChange={(event) => setVersionForm((value) => ({ ...value, workspaceMountPath: event.target.value }))} />
            <textarea className={textareaClass} placeholder="启动命令，每行一个参数" value={versionForm.command} onChange={(event) => setVersionForm((value) => ({ ...value, command: event.target.value }))} />
            <label className="inline-flex items-center gap-2 rounded-xl border border-border bg-muted/30 px-3 py-3 text-sm">
              <input checked={versionForm.isActive} type="checkbox" onChange={(event) => setVersionForm((value) => ({ ...value, isActive: event.target.checked }))} />设为当前活跃版本
            </label>
            <DialogFooter>
              <button className={secondaryButtonClass} onClick={() => setDialogs(prev => ({ ...prev, version: false }))} type="button">取消</button>
              <button className={primaryButtonClass} disabled={busy || !versionForm.templateId} type="submit">发布版本</button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
