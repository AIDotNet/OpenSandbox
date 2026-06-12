import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { Clipboard, Copy, Expand, Minimize, Plug, Power, RotateCcw, TerminalSquare } from "lucide-react"
import { Terminal as XTerm, type ITheme } from "@xterm/xterm"
import { FitAddon } from "@xterm/addon-fit"
import { toast } from "sonner"
import "@xterm/xterm/css/xterm.css"
import { api } from "../../services/api"
import { Card, primaryButtonClass, secondaryButtonClass, StatusBadge, tinyButtonClass } from "../ui/common"

type ContainerTerminalPanelProps = {
  containerId: string
}

type ConnectionStatus = "disconnected" | "connecting" | "connected" | "error"

const DARK_THEME: ITheme = {
  background: "#0b0b0b",
  foreground: "#e5e7eb",
  cursor: "#e5e7eb",
  cursorAccent: "#0b0b0b",
  selectionBackground: "rgba(148, 163, 184, 0.35)",
  black: "#0b0b0b",
  red: "#ef4444",
  green: "#22c55e",
  yellow: "#eab308",
  blue: "#3b82f6",
  magenta: "#a855f7",
  cyan: "#06b6d4",
  white: "#e5e7eb",
  brightBlack: "#6b7280",
  brightRed: "#f87171",
  brightGreen: "#4ade80",
  brightYellow: "#facc15",
  brightBlue: "#60a5fa",
  brightMagenta: "#c084fc",
  brightCyan: "#22d3ee",
  brightWhite: "#f9fafb",
}

const LIGHT_THEME: ITheme = {
  background: "#ffffff",
  foreground: "#0f172a",
  cursor: "#0f172a",
  cursorAccent: "#ffffff",
  selectionBackground: "rgba(15, 23, 42, 0.15)",
  black: "#0f172a",
  red: "#dc2626",
  green: "#16a34a",
  yellow: "#ca8a04",
  blue: "#2563eb",
  magenta: "#7c3aed",
  cyan: "#0891b2",
  white: "#e2e8f0",
  brightBlack: "#64748b",
  brightRed: "#ef4444",
  brightGreen: "#22c55e",
  brightYellow: "#eab308",
  brightBlue: "#3b82f6",
  brightMagenta: "#a855f7",
  brightCyan: "#06b6d4",
  brightWhite: "#f8fafc",
}

function resolveTheme() {
  if (typeof document === "undefined") {
    return DARK_THEME
  }

  return document.documentElement.classList.contains("dark") ? DARK_THEME : LIGHT_THEME
}

async function convertSocketDataToText(data: Blob | ArrayBuffer | string) {
  if (typeof data === "string") {
    return data
  }

  if (data instanceof Blob) {
    return await data.text()
  }

  return new TextDecoder().decode(data)
}

export function ContainerTerminalPanel({ containerId }: ContainerTerminalPanelProps) {
  const mountRef = useRef<HTMLDivElement | null>(null)
  const terminalRef = useRef<XTerm | null>(null)
  const fitAddonRef = useRef<FitAddon | null>(null)
  const socketRef = useRef<WebSocket | null>(null)
  const closeIntentRef = useRef<"manual" | "reconnect" | null>(null)
  const [status, setStatus] = useState<ConnectionStatus>("disconnected")
  const [hasSelection, setHasSelection] = useState(false)
  const [isFullscreen, setIsFullscreen] = useState(false)

  const isConnected = status === "connected"
  const isConnecting = status === "connecting"
  const statusTone = isConnected ? "success" : status === "error" ? "danger" : "neutral"
  const statusLabel = isConnected ? "已连接" : isConnecting ? "连接中" : status === "error" ? "异常" : "未连接"

  const sendTerminalResize = useCallback((cols: number, rows: number) => {
    const socket = socketRef.current
    if (!socket || socket.readyState !== WebSocket.OPEN || cols <= 0 || rows <= 0) {
      return false
    }

    socket.send(JSON.stringify({ type: "resize", cols, rows }))
    return true
  }, [])

  const fitTerminal = useCallback(() => {
    requestAnimationFrame(() => {
      try {
        fitAddonRef.current?.fit()
        const terminal = terminalRef.current
        if (terminal) {
          sendTerminalResize(terminal.cols, terminal.rows)
        }
      } catch (error) {
        void error
      }
    })
  }, [sendTerminalResize])

  const writeInfoLine = useCallback((message: string) => {
    terminalRef.current?.writeln(`\r\n${message}`)
  }, [])

  const sendTerminalInput = useCallback((data: string) => {
    const socket = socketRef.current
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      return false
    }

    socket.send(JSON.stringify({ type: "input", data }))
    return true
  }, [])

  const disconnect = useCallback(() => {
    const socket = socketRef.current
    if (!socket) {
      setStatus("disconnected")
      return
    }

    closeIntentRef.current = "manual"
    socket.close()
  }, [])

  const handleCopy = useCallback(() => {
    const selection = terminalRef.current?.getSelection()
    if (!selection) {
      return
    }

    navigator.clipboard.writeText(selection).then(
      () => toast.success("终端内容已复制"),
      () => toast.error("复制失败")
    )
  }, [])

  const handlePaste = useCallback(async () => {
    try {
      const text = await navigator.clipboard.readText()
      if (!text) {
        return
      }

      if (!sendTerminalInput(text)) {
        toast.error("终端未连接")
        return
      }

      terminalRef.current?.focus()
    } catch {
      toast.error("读取剪贴板失败")
    }
  }, [sendTerminalInput])

  const handleClear = useCallback(() => {
    terminalRef.current?.clear()
    terminalRef.current?.focus()
  }, [])

  const handleInterrupt = useCallback(() => {
    if (!sendTerminalInput("\u0003")) {
      toast.error("终端未连接")
      return
    }

    terminalRef.current?.focus()
  }, [sendTerminalInput])

  const connect = useCallback(() => {
    const existingSocket = socketRef.current
    if (existingSocket && existingSocket.readyState < WebSocket.CLOSING) {
      closeIntentRef.current = "reconnect"
      existingSocket.close()
    }

    setStatus("connecting")
    closeIntentRef.current = null

    const terminal = terminalRef.current
    if (!terminal) {
      setStatus("error")
      return
    }

    terminal.focus()
    writeInfoLine("[ssh connecting]")

    void api<{ url: string }>(`/api/containers/${encodeURIComponent(containerId)}/terminal/access-link`, {
      method: "POST",
    }).then(({ url }) => {
      const socket = new WebSocket(url)
      socket.binaryType = "arraybuffer"
      socketRef.current = socket

      socket.onopen = () => {
        if (socketRef.current !== socket) {
          return
        }

        setStatus("connected")
        fitTerminal()
        sendTerminalResize(terminal.cols, terminal.rows)
        terminal.focus()
        writeInfoLine("[ssh connected]")
      }

      socket.onmessage = event => {
        if (socketRef.current !== socket) {
          return
        }

        void convertSocketDataToText(event.data).then(text => {
          terminalRef.current?.write(text)
        })
      }

      socket.onerror = () => {
        if (socketRef.current !== socket) {
          return
        }

        setStatus("error")
        toast.error("终端连接失败")
        writeInfoLine("[ssh connection error]")
      }

      socket.onclose = () => {
        const isCurrentSocket = socketRef.current === socket
        if (isCurrentSocket) {
          socketRef.current = null
        }

        if (!isCurrentSocket && socketRef.current != null) {
          return
        }

        const closeIntent = closeIntentRef.current
        closeIntentRef.current = null
        setStatus(current => (closeIntent ? "disconnected" : current === "connecting" ? "error" : "disconnected"))
        writeInfoLine(closeIntent ? "[ssh disconnected]" : "[ssh closed]")
      }
    }).catch(error => {
      setStatus("error")
      toast.error(error instanceof Error ? error.message : "创建终端连接失败")
    })
  }, [containerId, fitTerminal, sendTerminalResize, writeInfoLine])

  useEffect(() => {
    const terminal = new XTerm({
      cursorBlink: true,
      cursorStyle: "block",
      fontSize: 14,
      fontFamily: "'Cascadia Code', 'Fira Code', 'JetBrains Mono', Consolas, 'Courier New', monospace",
      scrollback: 10000,
      convertEol: true,
      theme: resolveTheme(),
      allowTransparency: false,
    })

    const fitAddon = new FitAddon()
    terminal.loadAddon(fitAddon)
    terminal.open(mountRef.current!)
    terminalRef.current = terminal
    fitAddonRef.current = fitAddon
    fitAddon.fit()

    terminal.writeln("OpenClaw SSH")
    terminal.writeln(`容器：${containerId}`)
    terminal.writeln("点击“连接 SSH”建立 WebSocket 会话。")
    terminal.writeln("")

    const selectionDisposable = terminal.onSelectionChange(() => {
      setHasSelection((terminal.getSelection() || "").length > 0)
    })

    const dataDisposable = terminal.onData((data: string) => {
      sendTerminalInput(data)
    })

    const binaryDisposable = terminal.onBinary((data: string) => {
      sendTerminalInput(data)
    })

    const resizeDisposable = terminal.onResize(({ cols, rows }) => {
      sendTerminalResize(cols, rows)
    })

    terminal.attachCustomKeyEventHandler((event: KeyboardEvent) => {
      if (event.type !== "keydown") {
        return true
      }

      if (event.ctrlKey && event.shiftKey && event.code === "KeyC") {
        handleCopy()
        return false
      }

      if (event.ctrlKey && event.shiftKey && event.code === "KeyV") {
        void handlePaste()
        return false
      }

      if (event.ctrlKey && !event.shiftKey && !event.altKey && event.code === "KeyC") {
        handleInterrupt()
        return false
      }

      return true
    })

    const handleKeyDown = (event: KeyboardEvent) => {
      if (!mountRef.current?.contains(document.activeElement)) {
        return
      }

      if (event.ctrlKey && event.shiftKey && event.code === "KeyC") {
        event.preventDefault()
        handleCopy()
        return
      }

      if (event.ctrlKey && event.shiftKey && event.code === "KeyV") {
        event.preventDefault()
        void handlePaste()
        return
      }

      if (event.ctrlKey && !event.shiftKey && !event.altKey && event.code === "KeyC") {
        event.preventDefault()
        handleInterrupt()
      }
    }

    const handleFallbackInput = (event: KeyboardEvent) => {
      if (!mountRef.current?.contains(document.activeElement)) {
        return
      }

      const target = event.target as HTMLElement | null
      if (target?.classList.contains("xterm-helper-textarea")) {
        return
      }

      if (event.ctrlKey || event.metaKey || event.altKey) {
        return
      }

      const keyMap: Record<string, string> = {
        Enter: "\r",
        Backspace: "\u007f",
        Tab: "\t",
        Escape: "\u001b",
        ArrowUp: "\u001b[A",
        ArrowDown: "\u001b[B",
        ArrowRight: "\u001b[C",
        ArrowLeft: "\u001b[D",
      }

      const data = keyMap[event.key] ?? (event.key.length === 1 ? event.key : "")
      if (!data) {
        return
      }

      if (sendTerminalInput(data)) {
        event.preventDefault()
        terminal.focus()
      }
    }

    const handleWindowResize = () => {
      fitTerminal()
    }

    window.addEventListener("resize", handleWindowResize)
    window.addEventListener("keydown", handleKeyDown, true)
    window.addEventListener("keydown", handleFallbackInput, true)

    const resizeObserver = new ResizeObserver(() => {
      fitTerminal()
    })
    if (mountRef.current) {
      resizeObserver.observe(mountRef.current)
    }

    const intersectionObserver = new IntersectionObserver(entries => {
      if (entries[0]?.isIntersecting) {
        fitTerminal()
      }
    })
    if (mountRef.current) {
      intersectionObserver.observe(mountRef.current)
    }

    const themeObserver = new MutationObserver(() => {
      terminal.options.theme = resolveTheme()
    })
    themeObserver.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["class"],
    })

    const focusTimer = window.setTimeout(() => {
      fitTerminal()
      terminal.focus()
    }, 50)

    return () => {
      window.clearTimeout(focusTimer)
      closeIntentRef.current = "manual"
      socketRef.current?.close()
      selectionDisposable.dispose()
      dataDisposable.dispose()
      binaryDisposable.dispose()
      resizeDisposable.dispose()
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      themeObserver.disconnect()
      window.removeEventListener("resize", handleWindowResize)
      window.removeEventListener("keydown", handleKeyDown, true)
      window.removeEventListener("keydown", handleFallbackInput, true)
      terminal.dispose()
      terminalRef.current = null
      fitAddonRef.current = null
    }
  }, [containerId, fitTerminal, handleCopy, handleInterrupt, handlePaste, sendTerminalInput, sendTerminalResize])

  useEffect(() => {
    if (!isFullscreen) {
      return
    }

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"

    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [isFullscreen])

  const toolbar = useMemo(
    () => (
      <div className="flex flex-wrap gap-2">
        <button className={primaryButtonClass} disabled={isConnecting || isConnected} onClick={connect} type="button">
          <Plug className="mr-2 h-4 w-4" />
          {isConnecting ? "连接中" : "连接 SSH"}
        </button>
        <button className={secondaryButtonClass} disabled={isConnecting} onClick={connect} type="button">
          <RotateCcw className="mr-2 h-4 w-4" />重连
        </button>
        <button className={secondaryButtonClass} disabled={!isConnected && !isConnecting} onClick={disconnect} type="button">
          <Power className="mr-2 h-4 w-4" />断开
        </button>
        <button className={secondaryButtonClass} disabled={!isConnected} onClick={handleInterrupt} type="button">Ctrl+C</button>
        <button className={secondaryButtonClass} disabled={!hasSelection} onClick={handleCopy} type="button">
          <Copy className="mr-2 h-4 w-4" />复制
        </button>
        <button className={secondaryButtonClass} disabled={!isConnected} onClick={() => void handlePaste()} type="button">
          <Clipboard className="mr-2 h-4 w-4" />粘贴
        </button>
        <button className={secondaryButtonClass} onClick={handleClear} type="button">清屏</button>
        <button className={tinyButtonClass} onClick={() => setIsFullscreen(current => !current)} type="button">
          {isFullscreen ? <Minimize className="h-4 w-4" /> : <Expand className="h-4 w-4" />}
        </button>
      </div>
    ),
    [connect, disconnect, handleClear, handleCopy, handleInterrupt, handlePaste, hasSelection, isConnected, isConnecting, isFullscreen]
  )

  const terminalPane = useMemo(
    () => (
      <div className={`relative overflow-hidden rounded-3xl border border-border bg-black/95 ${isFullscreen ? "flex-1 min-h-0" : ""}`}>
        <div className="flex items-center gap-2 border-b border-white/10 px-3 py-2 text-xs text-zinc-300">
          <TerminalSquare className="h-4 w-4" />
          <span>SSH Shell</span>
          <span className="text-zinc-500">·</span>
          <span>{isConnected ? "已连接" : isConnecting ? "连接中" : "未连接"}</span>
        </div>
        <div
          className={`relative w-full ${isFullscreen ? "h-full min-h-0" : "h-[560px]"}`}
          onClick={() => terminalRef.current?.focus()}
          onFocus={() => terminalRef.current?.focus()}
          onMouseDown={() => terminalRef.current?.focus()}
          tabIndex={0}
        >
          <div className="h-full w-full px-2 py-2" ref={mountRef} />
          {!isConnected && !isConnecting ? (
            <div className="absolute inset-0 flex items-center justify-center bg-black/40 backdrop-blur-[1px]">
              <div className="flex flex-col items-center gap-3 text-center text-white">
                <StatusBadge label={status === "error" ? "连接异常，请重试" : "尚未建立 SSH 会话"} tone={status === "error" ? "danger" : "neutral"} />
                <button className={primaryButtonClass} onClick={connect} type="button">
                  <Plug className="mr-2 h-4 w-4" />连接 SSH
                </button>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    ),
    [connect, isConnected, isConnecting, isFullscreen, status]
  )

  return (
    <>
      <Card title="SSH 终端" description="复刻 Meteor 风格 SSH 交互：连接、重连、中断、复制、粘贴、清屏、全屏。">
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          {toolbar}
          <StatusBadge label={statusLabel} tone={statusTone} />
        </div>
        {terminalPane}
        <div className="mt-4 text-xs text-muted-foreground">快捷键：`Ctrl+Shift+C` 复制，`Ctrl+Shift+V` 粘贴，`Ctrl+C` 中断当前前台进程。</div>
      </Card>

      {isFullscreen ? (
        <div className="fixed inset-0 z-50 flex flex-col gap-4 bg-background p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div className="text-lg font-semibold">SSH 全屏终端</div>
              <div className="mt-1 text-sm text-muted-foreground">容器：{containerId}</div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <StatusBadge label={statusLabel} tone={statusTone} />
              <button className={secondaryButtonClass} onClick={() => setIsFullscreen(false)} type="button">
                <Minimize className="mr-2 h-4 w-4" />退出全屏
              </button>
            </div>
          </div>
          {toolbar}
          {terminalPane}
          <div className="text-xs text-muted-foreground">全屏模式下仍使用同一个 WebSocket 会话，不会重建连接。</div>
        </div>
      ) : null}
    </>
  )
}
