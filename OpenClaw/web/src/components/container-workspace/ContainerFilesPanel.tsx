import { useEffect, useMemo, useState } from "react"
import { FileText, Folder, FolderOpen, RefreshCcw, Trash2 } from "lucide-react"
import { api } from "../../services/api"
import type { FileContentResult, FileEntry, FileListResult } from "../../types"
import { decodeBase64ToText, encodeTextToBase64, formatBytes, formatTime, joinPath, parentPath } from "../../lib/utils"
import { Card, inputClass, primaryButtonClass, secondaryButtonClass, tinyButtonClass } from "../ui/common"
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "../ui/resizable"

type ContainerFilesPanelProps = {
  containerId: string
}

export function ContainerFilesPanel({ containerId }: ContainerFilesPanelProps) {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")
  const [currentDirectory, setCurrentDirectory] = useState("/")
  const [entries, setEntries] = useState<FileEntry[]>([])
  const [selectedFilePath, setSelectedFilePath] = useState("")
  const [selectedFileName, setSelectedFileName] = useState("")
  const [editorValue, setEditorValue] = useState("")
  const [newDirectoryName, setNewDirectoryName] = useState("")
  const breadcrumbs = useMemo(() => {
    const parts = currentDirectory.split("/").filter(Boolean)
    return [{ label: "/", path: "/" }, ...parts.map((_, index) => ({ label: parts[index], path: `/${parts.slice(0, index + 1).join("/")}` }))]
  }, [currentDirectory])

  useEffect(() => {
    void loadDirectory("/")
    setSelectedFilePath("")
    setSelectedFileName("")
    setEditorValue("")
  }, [containerId])

  async function loadDirectory(path: string) {
    try {
      const result = await api<FileListResult>(`/api/containers/${encodeURIComponent(containerId)}/files?path=${encodeURIComponent(path || "/")}`)
      setCurrentDirectory(result.path || "/")
      setEntries(result.entries ?? [])
      setMessage("")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "加载文件列表失败")
    }
  }

  async function openFile(path: string) {
    setBusy(true)
    try {
      const result = await api<FileContentResult>(`/api/containers/${encodeURIComponent(containerId)}/files/content?path=${encodeURIComponent(path)}`)
      setSelectedFilePath(result.path)
      setSelectedFileName(result.fileName)
      setEditorValue(decodeBase64ToText(result.contentBase64))
      setMessage("")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "读取文件失败")
    } finally {
      setBusy(false)
    }
  }

  async function saveFile() {
    if (!selectedFilePath) return
    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/files/content`, {
        method: "POST",
        body: JSON.stringify({ path: selectedFilePath, contentBase64: encodeTextToBase64(editorValue) }),
      })
      await loadDirectory(currentDirectory)
      setMessage("文件已保存")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "保存文件失败")
    } finally {
      setBusy(false)
    }
  }

  async function createDirectory() {
    const trimmed = newDirectoryName.trim()
    if (!trimmed) return
    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/directories`, {
        method: "POST",
        body: JSON.stringify({ path: joinPath(currentDirectory, trimmed) }),
      })
      setNewDirectoryName("")
      await loadDirectory(currentDirectory)
      setMessage("目录已创建")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "创建目录失败")
    } finally {
      setBusy(false)
    }
  }

  async function deletePath(path: string, recursive: boolean) {
    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/files?path=${encodeURIComponent(path)}&recursive=${recursive}`, { method: "DELETE" })
      if (selectedFilePath === path) {
        setSelectedFilePath("")
        setSelectedFileName("")
        setEditorValue("")
      }
      await loadDirectory(currentDirectory)
      setMessage("路径已删除")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除路径失败")
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card title="文件管理系统" description="左右分栏资源管理器，左侧目录浏览，右侧文件内容编辑。">
      {message ? <div className="mb-4 rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}
      <ResizablePanelGroup className="min-h-[42rem] rounded-3xl border border-border bg-background">
        <ResizablePanel defaultSize={42} minSize={28}>
          <div className="flex h-full flex-col border-r border-border">
            <div className="border-b border-border p-4">
              <div className="mb-3 flex flex-wrap items-center gap-2">
                <button className={secondaryButtonClass} onClick={() => void loadDirectory(currentDirectory)} type="button"><RefreshCcw className="mr-2 h-4 w-4" />刷新</button>
                <button className={secondaryButtonClass} disabled={currentDirectory === "/"} onClick={() => void loadDirectory(parentPath(currentDirectory))} type="button">返回上级</button>
              </div>
              <div className="mb-3 flex flex-wrap gap-2 text-xs text-muted-foreground">
                {breadcrumbs.map((crumb, index) => (
                  <button className="rounded-lg px-2 py-1 transition hover:bg-accent hover:text-accent-foreground" key={crumb.path} onClick={() => void loadDirectory(crumb.path)} type="button">
                    {index === 0 ? "/" : crumb.label}
                  </button>
                ))}
              </div>
              <div className="grid gap-2 md:grid-cols-[1fr_auto]">
                <input className={inputClass} placeholder="新目录名称" value={newDirectoryName} onChange={(event) => setNewDirectoryName(event.target.value)} />
                <button className={primaryButtonClass} disabled={busy || !newDirectoryName.trim()} onClick={() => void createDirectory()} type="button">新建目录</button>
              </div>
            </div>
            <div className="flex-1 overflow-auto p-3">
              <div className="space-y-2">
                {entries.length === 0 ? (
                  <div className="rounded-2xl border border-dashed border-border bg-muted/20 px-4 py-8 text-center text-sm text-muted-foreground">当前目录为空</div>
                ) : entries.map((entry) => (
                  <div className="flex items-center gap-3 rounded-2xl border border-border bg-card px-3 py-3" key={entry.path}>
                    <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
                      {entry.isDirectory ? <FolderOpen className="h-5 w-5" /> : <FileText className="h-5 w-5" />}
                    </div>
                    <button className="min-w-0 flex-1 text-left" onClick={() => entry.isDirectory ? void loadDirectory(entry.path) : void openFile(entry.path)} type="button">
                      <div className="truncate text-sm font-medium">{entry.name}</div>
                      <div className="mt-1 truncate text-xs text-muted-foreground">{entry.path}</div>
                      <div className="mt-1 flex flex-wrap gap-3 text-[11px] text-muted-foreground">
                        <span>{entry.isDirectory ? "目录" : formatBytes(entry.sizeBytes)}</span>
                        <span>{formatTime(entry.lastModifiedAt)}</span>
                      </div>
                    </button>
                    <button className={tinyButtonClass} onClick={() => void deletePath(entry.path, entry.isDirectory)} type="button"><Trash2 className="h-4 w-4" /></button>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </ResizablePanel>
        <ResizableHandle withHandle />
        <ResizablePanel defaultSize={58} minSize={32}>
          <div className="flex h-full flex-col">
            <div className="border-b border-border p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <div className="text-sm font-medium">{selectedFileName || "文件预览 / 编辑"}</div>
                  <div className="mt-1 text-xs text-muted-foreground">{selectedFilePath || "从左侧选择文件后即可查看或编辑"}</div>
                </div>
                <div className="flex gap-2">
                  <button className={secondaryButtonClass} disabled={!selectedFilePath || busy} onClick={() => selectedFilePath && void openFile(selectedFilePath)} type="button">重新读取</button>
                  <button className={primaryButtonClass} disabled={!selectedFilePath || busy} onClick={() => void saveFile()} type="button">保存文件</button>
                </div>
              </div>
            </div>
            <div className="flex-1 p-4">
              {!selectedFilePath ? (
                <div className="flex h-full flex-col items-center justify-center rounded-3xl border border-dashed border-border bg-muted/20 text-center">
                  <Folder className="h-10 w-10 text-muted-foreground" />
                  <div className="mt-4 text-sm font-medium">未选择文件</div>
                  <div className="mt-2 text-sm text-muted-foreground">左侧点击文件后在这里编辑。</div>
                </div>
              ) : (
                <textarea className="h-full min-h-[34rem] w-full rounded-3xl border border-border bg-background px-4 py-3 font-mono text-sm outline-none transition focus:border-ring focus:ring-2 focus:ring-ring/20" value={editorValue} onChange={(event) => setEditorValue(event.target.value)} />
              )}
            </div>
          </div>
        </ResizablePanel>
      </ResizablePanelGroup>
    </Card>
  )
}
