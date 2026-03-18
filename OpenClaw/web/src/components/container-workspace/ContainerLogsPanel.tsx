import { useEffect, useRef, useState } from "react"
import { Card, primaryButtonClass, secondaryButtonClass, tinyButtonClass } from "../ui/common"
import { buildWsUrl } from "../../lib/utils"
import { api } from "../../services/api"
import type { LogsResult } from "../../types"

type ContainerLogsPanelProps = {
  containerId: string
}

export function ContainerLogsPanel({ containerId }: ContainerLogsPanelProps) {
  const [logLines, setLogLines] = useState<string[]>([])
  const [message, setMessage] = useState("")
  const logsSocketRef = useRef<WebSocket | null>(null)
  const logsViewportRef = useRef<HTMLPreElement | null>(null)

  useEffect(() => {
    void loadLogs()
    return () => {
      logsSocketRef.current?.close()
      logsSocketRef.current = null
    }
  }, [containerId])

  useEffect(() => {
    if (logsViewportRef.current) {
      logsViewportRef.current.scrollTop = logsViewportRef.current.scrollHeight
    }
  }, [logLines])

  async function loadLogs() {
    try {
      const logs = await api<LogsResult>(`/api/containers/${encodeURIComponent(containerId)}/logs`)
      setLogLines(logs.lines)
      setMessage("")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "加载日志失败")
    }
  }

  function connectStream() {
    logsSocketRef.current?.close()
    const socket = new WebSocket(buildWsUrl(`/api/containers/${encodeURIComponent(containerId)}/logs/ws`))
    socket.onmessage = (event) => setLogLines((value) => [...value, String(event.data)])
    socket.onerror = () => setMessage("日志实时流连接失败")
    socket.onclose = () => {
      logsSocketRef.current = null
    }
    logsSocketRef.current = socket
  }

  return (
    <Card title="容器日志" description="支持最近 N 行读取与 WebSocket 实时流。">
      {message ? <div className="mb-4 rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}
      <div className="mb-4 flex flex-wrap gap-2">
        <button className={secondaryButtonClass} onClick={() => void loadLogs()} type="button">读取最近日志</button>
        <button className={primaryButtonClass} onClick={connectStream} type="button">连接实时流</button>
        <button className={secondaryButtonClass} onClick={() => logsSocketRef.current?.close()} type="button">关闭流</button>
        <button className={tinyButtonClass} onClick={() => setLogLines([])} type="button">清空面板</button>
      </div>
      <pre className="min-h-[28rem] overflow-auto rounded-3xl border border-border bg-zinc-950 p-4 text-xs leading-6 text-zinc-100" ref={logsViewportRef}>{logLines.join("\n") || "暂无日志输出"}</pre>
    </Card>
  )
}
