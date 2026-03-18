import { useState, useEffect } from "react"
import type { FormEvent } from "react"
import { useData } from "../hooks/useData"
import { api } from "../services/api"
import { Card, InfoBlock, primaryButtonClass, inputClass } from "../components/ui/common"
import type { SystemSettings } from "../types"

export default function SettingsPage() {
  const { settings: initialSettings, loadAdminData } = useData()
  const [settings, setSettings] = useState<SystemSettings | null>(initialSettings)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  useEffect(() => {
    setSettings(initialSettings)
  }, [initialSettings])

  async function handleSaveSettings(event: FormEvent) {
    event.preventDefault()
    if (!settings) return
    setBusy(true)
    try {
      const next = await api<SystemSettings>("/api/admin/settings", { method: "PUT", body: JSON.stringify(settings) })
      setSettings(next)
      await loadAdminData()
      setMessage("系统设置已保存")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "保存失败")
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-6">
      {message ? <div className="rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}

      <Card title="系统默认值" description="这里放可编辑的系统参数。" actions={<button className={primaryButtonClass} disabled={busy || !settings} form="settings-form" type="submit">保存系统设置</button>}>
        <form className="grid gap-4 md:grid-cols-3" id="settings-form" onSubmit={handleSaveSettings}>
          <div className="space-y-2">
            <div className="text-sm font-medium">默认 CPU</div>
            <input className={inputClass} placeholder="默认 CPU" value={settings?.defaultCpu ?? ""} onChange={(event) => setSettings((value) => value ? { ...value, defaultCpu: event.target.value } : value)} />
          </div>
          <div className="space-y-2">
            <div className="text-sm font-medium">默认内存</div>
            <input className={inputClass} placeholder="默认内存" value={settings?.defaultMemory ?? ""} onChange={(event) => setSettings((value) => value ? { ...value, defaultMemory: event.target.value } : value)} />
          </div>
          <div className="space-y-2">
            <div className="text-sm font-medium">默认日志行数</div>
            <input className={inputClass} placeholder="默认日志行数" type="number" value={settings?.defaultLogTailLines ?? 200} onChange={(event) => setSettings((value) => value ? { ...value, defaultLogTailLines: Number(event.target.value) || 200 } : value)} />
          </div>
        </form>
      </Card>

      <Card title="策略说明" description="只保留简洁的规则摘要。">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <InfoBlock title="账号体系" description="本地用户名密码登录，Cookie Session。" />
          <InfoBlock title="实例唯一性" description="每个员工在每个服���端最多 1 个实例。" />
          <InfoBlock title="模板快照" description="部署时保存模板版本快照。" />
          <InfoBlock title="过期策略" description="默认按不过期语义创建实例。" />
          <InfoBlock title="日志能力" description="支持最近 N 行和实时日志。" />
          <InfoBlock title="终端权限" description="员工仅自己的容器，管理员可看全部。" />
        </div>
      </Card>
    </div>
  )
}
