import { useEffect, useMemo, useRef, useState } from "react"
import { TerminalSquare } from "lucide-react"
import { Terminal as XTerm } from "@xterm/xterm"
import { FitAddon } from "@xterm/addon-fit"
import "@xterm/xterm/css/xterm.css"
import { Card, primaryButtonClass, secondaryButtonClass, StatusBadge } from "../ui/common"
import { buildWsUrl } from "../../lib/utils"

type ContainerTerminalPanelProps = {
  containerId: string
}

export function ContainerTerminalPanel({ containerId }: ContainerTerminalPanelProps) {
  const mountRef = useRef<HTMLDivElement | null>(null)
  const terminalRef = useRef<XTerm | null>(null)
  const fitAddonRef = useRef<FitAddon | null>(null)
  const socketRef = useRef<WebSocket | null>(null)
  const resizeObserverRef = useRef<ResizeObserver | null>(null)
  const [message, setMessage] = useState("默认进入不自动连接，请手动建立 WebSocket 会话。")
  const [connected, setConnected] = useState(false)

  const terminalTheme = useMemo(
    () => ({
      background: "#09090b",
      foreground: "#f4f4f5",
      cursor: "#fafafa",
      black: "#18181b",
      red: "#f87171",
      green: "#4ade80",
      yellow: "#facc15",
      blue: "#60a5fa",
      magenta: "#c084fc",
      cyan: "#22d3ee",
      white: "#e4e4e7",
      brightBlack: "#3f3f46",
      brightWhite: "#ffffff",
    }),
    [],
  )

  useEffect(() => {
    const terminal = new XTerm({
      convertEol: true,
      cursorBlink: true,
      fontFamily: 'Consolas, "Courier New", monospace',
      fontSize: 13,
      lineHeight: 1.35,
      scrollback: 5000,
      theme: terminalTheme,
    })
    const fitAddon = new FitAddon()
    terminal.loadAddon(fitAddon)
    terminal.open(mountRef.current!)
    terminal.write("\u001b[1;37mOpenClaw Terminal\u001b[0m\r\n")
    terminal.write("点击上方“手动连接”后开始交互。\r\n")

    terminalRef.current = terminal
    fitAddonRef.current = fitAddon
    fitAddon.fit()

    resizeObserverRef.current = new ResizeObserver(() => {
      fitAddonRef.current?.fit()
    })
    if (mountRef.current) resizeObserverRef.current.observe(mountRef.current)

    const disposable = terminal.onData((data) => {
      if (socketRef.current?.readyState === WebSocket.OPEN) {
        socketRef.current.send(data)
      }
    })

    return () => {
      disposable.dispose()
      resizeObserverRef.current?.disconnect()
      resizeObserverRef.current = null
      socketRef.current?.close()
      socketRef.current = null
      fitAddonRef.current = null
      terminal.dispose()
      terminalRef.current = null
    }
  }, [terminalTheme])

  useEffect(() => {
    if (!terminalRef.current) return
    terminalRef.current.reset()
    terminalRef.current.write("\u001b[1;37mOpenClaw Terminal\u001b[0m\r\n")
    terminalRef.current.write(`容器：${containerId}\r\n`)
    terminalRef.current.write("点击上方“手动连接”后开始交互。\r\n")
    disconnect()
    setConnected(false)
    setMessage("默认进入不自动连接，请手动建立 WebSocket 会话。")
  }, [containerId])

  function connect() {
    disconnect()
    const terminal = terminalRef.current
    if (!terminal) return

    terminal.write("\r\n[connecting...]\r\n")
    const socket = new WebSocket(buildWsUrl(`/api/containers/${encodeURIComponent(containerId)}/terminal/ws`))
    socket.binaryType = "arraybuffer"
    socket.onopen = () => {
      setConnected(true)
      setMessage("终端已连接")
      fitAddonRef.current?.fit()
      terminal.focus()
      terminal.write("[connected]\r\n")
    }
    socket.onmessage = (event) => {
      if (typeof event.data === "string") {
        terminal.write(event.data)
        return
      }
      if (event.data instanceof ArrayBuffer) {
        terminal.write(new Uint8Array(event.data))
      }
    }
    socket.onerror = () => {
      setMessage("终端连接失败")
      terminal.write("\r\n[connection failed]\r\n")
    }
    socket.onclose = () => {
      setConnected(false)
      socketRef.current = null
      terminal.write("\r\n[disconnected]\r\n")
    }
    socketRef.current = socket
  }

  function disconnect() {
    socketRef.current?.close()
    socketRef.current = null
    setConnected(false)
  }

  return (
    <Card title="容器终端" description="真正的 xterm 终端，默认不自动连接，手动建立 WebSocket 会话。">
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <button className={primaryButtonClass} onClick={connect} type="button">手动连接</button>
        <button className={secondaryButtonClass} onClick={disconnect} type="button">断开连接</button>
        <StatusBadge label={connected ? "已连接" : "未连接"} tone={connected ? "success" : "neutral"} />
      </div>
      <div className="mb-4 rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div>
      <div className="rounded-3xl border border-border bg-zinc-950 p-3">
        <div className="mb-2 flex items-center gap-2 text-xs text-zinc-400"><TerminalSquare className="h-4 w-4" />WebSocket Terminal Session</div>
        <div className="h-[34rem] overflow-hidden rounded-2xl bg-zinc-950" ref={mountRef} />
      </div>
    </Card>
  )
}
