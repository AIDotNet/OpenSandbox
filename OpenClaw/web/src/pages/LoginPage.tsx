import { useState } from "react"
import type { FormEvent } from "react"
import { Shield } from "lucide-react"
import { useNavigate } from "react-router-dom"
import { useAuth } from "../hooks/useAuth"
import { api } from "../services/api"
import { inputClass, primaryButtonClass } from "../components/ui/common"

export default function LoginPage() {
  const [userName, setUserName] = useState("")
  const [password, setPassword] = useState("")
  const [busy, setBusy] = useState<string | null>(null)
  const [message, setMessage] = useState("")
  const { refreshSession } = useAuth()
  const navigate = useNavigate()

  async function handleLogin(event: FormEvent) {
    event.preventDefault()
    setBusy("登录中")
    setMessage("")
    try {
      await api("/api/auth/login", { method: "POST", body: JSON.stringify({ userName, password }) })
      await refreshSession()
      navigate("/")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "登录失败")
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="flex min-h-svh bg-background text-foreground">
      <div className="hidden flex-1 items-center justify-center border-r border-border bg-muted/20 p-10 lg:flex">
        <div className="max-w-lg space-y-4">
          <div className="inline-flex h-14 w-14 items-center justify-center rounded-2xl border border-border bg-card text-foreground"><Shield className="h-7 w-7" /></div>
          <div className="space-y-2">
            <h1 className="text-4xl font-semibold tracking-tight">OpenClaw</h1>
            <p className="text-sm leading-7 text-muted-foreground">单机版沙盒控制面，集中管理多个 OpenSandbox 服务端、模板版本和员工实例。</p>
          </div>
        </div>
      </div>
      <div className="flex flex-1 items-center justify-center px-6 py-10">
        <form className="w-full max-w-md space-y-5 rounded-3xl border border-border bg-card p-8 shadow-sm" onSubmit={handleLogin}>
          <div>
            <div className="text-2xl font-semibold">账号登录</div>
            <div className="mt-2 text-sm text-muted-foreground">使用本地用户名密码登录控制台</div>
          </div>
          <input className={inputClass} placeholder="用户名" value={userName} onChange={(event) => setUserName(event.target.value)} />
          <input className={inputClass} placeholder="密码" type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          <button className={`${primaryButtonClass} w-full`} disabled={!!busy} type="submit">{busy ?? "登录"}</button>
          {message ? <div className="rounded-2xl border border-destructive/20 bg-destructive/10 px-4 py-3 text-sm text-destructive">{message}</div> : null}
        </form>
      </div>
    </div>
  )
}
