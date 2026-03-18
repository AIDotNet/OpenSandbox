import type { FormEvent } from "react"
import type { SandboxServer, Template } from "../../types"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { inputClass, primaryButtonClass, secondaryButtonClass } from "../ui/common"

type DeployForm = {
  sandboxServerId: string
  templateId: string
  apiEndpoint: string
  apiType: string
  model: string
  apiKey: string
}

type DeploymentDialogProps = {
  open: boolean
  busy: boolean
  servers: SandboxServer[]
  templates: Template[]
  value: DeployForm
  onOpenChange: (open: boolean) => void
  onChange: (updater: (value: DeployForm) => DeployForm) => void
  onSubmit: (event: FormEvent) => void
}

export function DeploymentDialog({ open, busy, servers, templates, value, onOpenChange, onChange, onSubmit }: DeploymentDialogProps) {
  return (
    <Dialog onOpenChange={onOpenChange} open={open}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>新增部署</DialogTitle>
          <DialogDescription>填写部署参数并创建或更新容器。</DialogDescription>
        </DialogHeader>
        <form className="grid gap-3" onSubmit={onSubmit}>
          <div className="grid gap-3 md:grid-cols-2">
            <select className={inputClass} value={value.sandboxServerId} onChange={(event) => onChange((current) => ({ ...current, sandboxServerId: event.target.value }))}>
              <option value="">选择服务端</option>
              {servers.map((server) => <option key={server.id} value={server.id}>{server.name}</option>)}
            </select>
            <select className={inputClass} value={value.templateId} onChange={(event) => onChange((current) => ({ ...current, templateId: event.target.value }))}>
              <option value="">选择模板</option>
              {templates.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}
            </select>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            <select className={inputClass} value={value.apiType} onChange={(event) => onChange((current) => ({ ...current, apiType: event.target.value }))}>
              <option value="chat">chat</option>
              <option value="messages">messages</option>
            </select>
            <input className={inputClass} placeholder="Model" value={value.model} onChange={(event) => onChange((current) => ({ ...current, model: event.target.value }))} />
          </div>
          <input className={inputClass} placeholder="API Endpoint" value={value.apiEndpoint} onChange={(event) => onChange((current) => ({ ...current, apiEndpoint: event.target.value }))} />
          <input className={inputClass} placeholder="API Key" value={value.apiKey} onChange={(event) => onChange((current) => ({ ...current, apiKey: event.target.value }))} />
          <DialogFooter>
            <button className={secondaryButtonClass} onClick={() => onOpenChange(false)} type="button">取消</button>
            <button className={primaryButtonClass} disabled={busy} type="submit">开始部署</button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
