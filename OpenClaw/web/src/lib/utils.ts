import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function splitLines(value: string) {
  return value.split(/\r?\n/g).map((item) => item.trim()).filter(Boolean)
}

export function buildWsUrl(path: string) {
  const protocol = window.location.protocol === "https:" ? "wss" : "ws"
  return `${protocol}://${window.location.host}${path}`
}

export function formatTime(value?: string | null) {
  if (!value) return "-"
  return new Date(value).toLocaleString("zh-CN", { hour12: false })
}

export function healthTone(status: string): "success" | "danger" | "neutral" {
  if (status === "Healthy") return "success"
  if (status === "Unhealthy") return "danger"
  return "neutral"
}

export function normalizePath(pathname: string) {
  if (!pathname || pathname === "/") return "/"
  const normalized = pathname.replace(/\/+$/, "")
  return normalized || "/"
}

export function shortId(value: string) {
  return value.length <= 12 ? value : value.slice(0, 12)
}

export function parentPath(path: string) {
  const normalized = normalizeUnixPath(path)
  if (normalized === "/") return "/"
  const parts = normalized.split("/").filter(Boolean)
  parts.pop()
  return parts.length === 0 ? "/" : `/${parts.join("/")}`
}

export function joinPath(basePath: string, name: string) {
  const normalizedBase = normalizeUnixPath(basePath)
  const segment = name.trim().replace(/^\/+|\/+$/g, "")
  if (!segment) return normalizedBase
  return normalizedBase === "/" ? `/${segment}` : `${normalizedBase}/${segment}`
}

export function normalizeUnixPath(path: string) {
  const normalized = (path || "/").replace(/\\/g, "/")
  const trimmed = normalized.replace(/\/+$/, "")
  if (!trimmed || trimmed === "") return "/"
  return trimmed.startsWith("/") ? trimmed : `/${trimmed}`
}

export function decodeBase64ToText(contentBase64: string) {
  const binary = window.atob(contentBase64)
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
  return new TextDecoder().decode(bytes)
}

export function encodeTextToBase64(value: string) {
  const bytes = new TextEncoder().encode(value)
  let binary = ""
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte)
  })
  return window.btoa(binary)
}

export function formatBytes(value?: number | null) {
  if (value == null) return "-"
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
}
