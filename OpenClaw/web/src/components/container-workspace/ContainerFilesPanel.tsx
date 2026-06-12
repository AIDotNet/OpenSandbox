import { useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from "react"
import { ArrowRight, Copy, Download, FilePlus, FileText, Folder, FolderOpen, FolderPlus, PencilLine, RefreshCcw, Save, Trash2, Upload } from "lucide-react"
import { toast } from "sonner"
import { api } from "../../services/api"
import type { FileContentResult, FileEntry, FileListResult } from "../../types"
import { decodeBase64ToText, encodeTextToBase64, formatBytes, formatTime, joinPath, normalizeUnixPath, parentPath } from "../../lib/utils"
import { Card, inputClass, primaryButtonClass, secondaryButtonClass, tinyButtonClass } from "../ui/common"
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "../ui/resizable"

type ContainerFilesPanelProps = {
  containerId: string
  workspacePath?: string
}

function encodeBytesToBase64(bytes: Uint8Array) {
  let binary = ""
  const chunkSize = 0x8000

  for (let index = 0; index < bytes.length; index += chunkSize) {
    const chunk = bytes.subarray(index, index + chunkSize)
    for (let chunkIndex = 0; chunkIndex < chunk.length; chunkIndex += 1) {
      binary += String.fromCharCode(chunk[chunkIndex])
    }
  }

  return window.btoa(binary)
}

function decodeBase64ToBytes(contentBase64: string) {
  const binary = window.atob(contentBase64)
  const bytes = new Uint8Array(binary.length)

  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}

function looksBinaryFile(bytes: Uint8Array) {
  if (bytes.length === 0) {
    return false
  }

  const sample = bytes.subarray(0, Math.min(bytes.length, 8192))
  let suspiciousCount = 0

  for (const byte of sample) {
    if (byte === 0) {
      return true
    }

    const isAllowedControl = byte === 9 || byte === 10 || byte === 13
    const isTextLike = (byte >= 32 && byte <= 126) || byte >= 128
    if (!isAllowedControl && !isTextLike) {
      suspiciousCount += 1
    }
  }

  return suspiciousCount / sample.length > 0.12
}

function downloadFile(fileName: string, bytes: Uint8Array) {
  const normalizedBytes = Uint8Array.from(bytes)
  const blob = new Blob([normalizedBytes])
  const url = window.URL.createObjectURL(blob)
  const anchor = document.createElement("a")
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  window.URL.revokeObjectURL(url)
}

function buildDuplicatePath(path: string, isDirectory: boolean) {
  const normalizedPath = normalizeUnixPath(path)
  const directory = parentPath(normalizedPath)
  const name = normalizedPath.split("/").filter(Boolean).pop() || "copy"
  const nextName = isDirectory
    ? `${name}-copy`
    : name.includes(".")
      ? `${name.slice(0, name.lastIndexOf("."))}-copy${name.slice(name.lastIndexOf("."))}`
      : `${name}-copy`

  return joinPath(directory, nextName)
}

export function ContainerFilesPanel({ containerId, workspacePath }: ContainerFilesPanelProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const rootDirectory = useMemo(() => normalizeUnixPath(workspacePath || "/"), [workspacePath])
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")
  const [currentDirectory, setCurrentDirectory] = useState(rootDirectory)
  const [pathInput, setPathInput] = useState(rootDirectory)
  const [entries, setEntries] = useState<FileEntry[]>([])
  const [selectedFilePath, setSelectedFilePath] = useState("")
  const [selectedFileName, setSelectedFileName] = useState("")
  const [selectedFileBase64, setSelectedFileBase64] = useState("")
  const [editorValue, setEditorValue] = useState("")
  const [savedEditorValue, setSavedEditorValue] = useState("")
  const [newDirectoryName, setNewDirectoryName] = useState("")
  const [newFileName, setNewFileName] = useState("")
  const [isBinaryFile, setIsBinaryFile] = useState(false)

  const isDirty = !!selectedFilePath && !isBinaryFile && editorValue !== savedEditorValue
  const breadcrumbs = useMemo(() => {
    const rootParts = rootDirectory.split("/").filter(Boolean)
    const currentParts = currentDirectory.split("/").filter(Boolean)
    const extraParts = currentParts.slice(rootParts.length)

    return [
      { label: "工作区", path: rootDirectory },
      ...extraParts.map((_, index) => ({
        label: extraParts[index],
        path: `${rootDirectory === "/" ? "" : rootDirectory}/${extraParts.slice(0, index + 1).join("/")}` || "/",
      })),
    ]
  }, [currentDirectory, rootDirectory])

  useEffect(() => {
    clearSelectedFile()
    setCurrentDirectory(rootDirectory)
    setPathInput(rootDirectory)
    setEntries([])
    setMessage("")
    void loadDirectory(rootDirectory, true)
  }, [containerId, rootDirectory])

  useEffect(() => {
    if (!isDirty) {
      return undefined
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ""
    }

    window.addEventListener("beforeunload", handleBeforeUnload)
    return () => window.removeEventListener("beforeunload", handleBeforeUnload)
  }, [isDirty])

  function confirmDiscard(nextAction: string) {
    if (!isDirty) {
      return true
    }

    return window.confirm(`当前文件尚未保存，确定继续${nextAction}吗？`)
  }

  function clearSelectedFile() {
    setSelectedFilePath("")
    setSelectedFileName("")
    setSelectedFileBase64("")
    setEditorValue("")
    setSavedEditorValue("")
    setIsBinaryFile(false)
  }

  async function loadDirectory(path: string, force = false) {
    if (!force && !confirmDiscard(`切换到 ${path}`)) {
      return
    }

    setBusy(true)
    try {
      const requestedPath = path.trim() ? path.trim() : rootDirectory
      const result = await api<FileListResult>(`/api/containers/${encodeURIComponent(containerId)}/files?path=${encodeURIComponent(requestedPath)}`)
      const nextPath = normalizeUnixPath(result.path || requestedPath)
      setCurrentDirectory(nextPath)
      setPathInput(nextPath)
      setEntries(result.entries ?? [])
      setMessage("")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "加载文件列表失败")
    } finally {
      setBusy(false)
    }
  }

  async function openFile(path: string) {
    if (selectedFilePath !== path && !confirmDiscard(`打开 ${path}`)) {
      return
    }

    setBusy(true)
    try {
      const result = await api<FileContentResult>(`/api/containers/${encodeURIComponent(containerId)}/files/content?path=${encodeURIComponent(path)}`)
      const bytes = decodeBase64ToBytes(result.contentBase64)
      const binaryFile = looksBinaryFile(bytes)

      setSelectedFilePath(result.path)
      setSelectedFileName(result.fileName)
      setSelectedFileBase64(result.contentBase64)
      setIsBinaryFile(binaryFile)

      if (binaryFile) {
        setEditorValue("")
        setSavedEditorValue("")
        toast.info("检测到二进制文件，已切换为只读下载模式")
      } else {
        const nextValue = decodeBase64ToText(result.contentBase64)
        setEditorValue(nextValue)
        setSavedEditorValue(nextValue)
      }

      setMessage("")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "读取文件失败")
    } finally {
      setBusy(false)
    }
  }

  async function saveFile() {
    if (!selectedFilePath || isBinaryFile) {
      return
    }

    setBusy(true)
    try {
      const contentBase64 = encodeTextToBase64(editorValue)
      await api(`/api/containers/${encodeURIComponent(containerId)}/files/content`, {
        method: "POST",
        body: JSON.stringify({ path: selectedFilePath, contentBase64 }),
      })
      setSelectedFileBase64(contentBase64)
      setSavedEditorValue(editorValue)
      await loadDirectory(currentDirectory, true)
      setMessage("")
      toast.success("文件已保存")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "保存文件失败")
    } finally {
      setBusy(false)
    }
  }

  async function createDirectory() {
    const trimmed = newDirectoryName.trim()
    if (!trimmed) {
      return
    }

    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/directories`, {
        method: "POST",
        body: JSON.stringify({ path: joinPath(currentDirectory, trimmed) }),
      })
      setNewDirectoryName("")
      await loadDirectory(currentDirectory, true)
      setMessage("")
      toast.success("目录已创建")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "创建目录失败")
    } finally {
      setBusy(false)
    }
  }

  async function createFile() {
    const trimmed = newFileName.trim()
    if (!trimmed) {
      return
    }

    const path = joinPath(currentDirectory, trimmed)
    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/files/content`, {
        method: "POST",
        body: JSON.stringify({ path, contentBase64: "" }),
      })
      setNewFileName("")
      await loadDirectory(currentDirectory, true)
      await openFile(path)
      setMessage("")
      toast.success("文件已创建")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "创建文件失败")
    } finally {
      setBusy(false)
    }
  }

  async function uploadFiles(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) {
      return
    }

    setBusy(true)
    try {
      for (const file of Array.from(fileList)) {
        const bytes = new Uint8Array(await file.arrayBuffer())
        await api(`/api/containers/${encodeURIComponent(containerId)}/files/content`, {
          method: "POST",
          body: JSON.stringify({
            path: joinPath(currentDirectory, file.name),
            contentBase64: encodeBytesToBase64(bytes),
          }),
        })
      }

      if (fileInputRef.current) {
        fileInputRef.current.value = ""
      }
      await loadDirectory(currentDirectory, true)
      setMessage("")
      toast.success(`已上传 ${fileList.length} 个文件`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "上传文件失败")
    } finally {
      setBusy(false)
    }
  }

  async function movePath(sourcePath: string, destinationPath: string, isDirectory: boolean) {
    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/files/move`, {
        method: "POST",
        body: JSON.stringify({ sourcePath, destinationPath }),
      })

      if (selectedFilePath === sourcePath && !isDirectory) {
        await openFile(destinationPath)
      } else if (selectedFilePath === sourcePath) {
        clearSelectedFile()
      }

      await loadDirectory(currentDirectory, true)
      setMessage("")
      toast.success("路径已移动")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "移动路径失败")
    } finally {
      setBusy(false)
    }
  }

  async function copyPath(sourcePath: string, destinationPath: string) {
    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/files/copy`, {
        method: "POST",
        body: JSON.stringify({ sourcePath, destinationPath }),
      })
      await loadDirectory(currentDirectory, true)
      setMessage("")
      toast.success("路径已复制")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "复制路径失败")
    } finally {
      setBusy(false)
    }
  }

  async function deletePath(path: string, recursive: boolean) {
    const confirmed = window.confirm(recursive ? `确定递归删除 ${path} 吗？` : `确定删除 ${path} 吗？`)
    if (!confirmed) {
      return
    }

    setBusy(true)
    try {
      await api(`/api/containers/${encodeURIComponent(containerId)}/files?path=${encodeURIComponent(path)}&recursive=${recursive}`, { method: "DELETE" })
      if (selectedFilePath === path) {
        clearSelectedFile()
      }
      await loadDirectory(currentDirectory, true)
      setMessage("")
      toast.success("路径已删除")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除路径失败")
    } finally {
      setBusy(false)
    }
  }

  function handleRename(entry: FileEntry) {
    const userInput = window.prompt("输入新的名称或完整路径", entry.path)
    if (!userInput || userInput.trim() === entry.path) {
      return
    }

    const destinationPath = userInput.includes("/") ? userInput.trim() : joinPath(parentPath(entry.path), userInput.trim())
    void movePath(entry.path, destinationPath, entry.isDirectory)
  }

  function handleDuplicate(entry: FileEntry) {
    const suggestedPath = buildDuplicatePath(entry.path, entry.isDirectory)
    const userInput = window.prompt("输入复制后的完整路径", suggestedPath)
    if (!userInput || userInput.trim() === entry.path) {
      return
    }

    void copyPath(entry.path, userInput.trim())
  }

  function downloadSelectedFile() {
    if (!selectedFilePath) {
      return
    }

    const contentBase64 = isBinaryFile || !isDirty ? selectedFileBase64 : encodeTextToBase64(editorValue)
    const bytes = decodeBase64ToBytes(contentBase64)
    downloadFile(selectedFileName || "download.bin", bytes)
    toast.success("文件已下载")
  }

  function handleEditorKeyDown(event: ReactKeyboardEvent<HTMLTextAreaElement>) {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault()
      void saveFile()
    }
  }

  return (
    <Card title="文件管理" description="默认收口到工作区，补全目录浏览、创建文件/目录、上传下载、文本编辑和二进制保护。">
      {message ? <div className="mb-4 rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}
      <ResizablePanelGroup className="min-h-[44rem] rounded-3xl border border-border bg-background" orientation="horizontal">
        <ResizablePanel defaultSize={42} minSize={28}>
          <div className="flex h-full flex-col border-r border-border">
            <div className="border-b border-border p-4">
              <div className="mb-3 flex flex-wrap items-center gap-2">
                <button className={secondaryButtonClass} disabled={busy} onClick={() => void loadDirectory(currentDirectory, true)} type="button">
                  <RefreshCcw className="mr-2 h-4 w-4" />刷新
                </button>
                <button className={secondaryButtonClass} disabled={busy || currentDirectory === rootDirectory} onClick={() => void loadDirectory(parentPath(currentDirectory))} type="button">
                  返回上级
                </button>
                <button className={secondaryButtonClass} disabled={busy || currentDirectory === rootDirectory} onClick={() => void loadDirectory(rootDirectory)} type="button">
                  回到工作区
                </button>
              </div>

              <div className="mb-3 flex flex-wrap gap-2 text-xs text-muted-foreground">
                {breadcrumbs.map(crumb => (
                  <button className="rounded-lg px-2 py-1 transition hover:bg-accent hover:text-accent-foreground" key={crumb.path} onClick={() => void loadDirectory(crumb.path)} type="button">
                    {crumb.label}
                  </button>
                ))}
              </div>

              <div className="mb-2 text-xs text-muted-foreground">工作区根目录：{rootDirectory}</div>

              <div className="mb-3 grid gap-2 md:grid-cols-[1fr_auto]">
                <input className={inputClass} onChange={event => setPathInput(event.target.value)} placeholder="输入工作区内路径，例如 logs 或 /home/node/.openclaw/workspace" value={pathInput} />
                <button className={secondaryButtonClass} disabled={busy} onClick={() => void loadDirectory(pathInput)} type="button">
                  <ArrowRight className="mr-2 h-4 w-4" />跳转
                </button>
              </div>

              <div className="grid gap-2 md:grid-cols-[1fr_auto]">
                <input className={inputClass} onChange={event => setNewDirectoryName(event.target.value)} placeholder="新目录名称" value={newDirectoryName} />
                <button className={primaryButtonClass} disabled={busy || !newDirectoryName.trim()} onClick={() => void createDirectory()} type="button">
                  <FolderPlus className="mr-2 h-4 w-4" />新建目录
                </button>
              </div>

              <div className="mt-2 grid gap-2 md:grid-cols-[1fr_auto]">
                <input className={inputClass} onChange={event => setNewFileName(event.target.value)} placeholder="新文件名称" value={newFileName} />
                <button className={primaryButtonClass} disabled={busy || !newFileName.trim()} onClick={() => void createFile()} type="button">
                  <FilePlus className="mr-2 h-4 w-4" />新建文件
                </button>
              </div>

              <div className="mt-2 flex flex-wrap gap-2">
                <input className="hidden" multiple onChange={event => void uploadFiles(event.target.files)} ref={fileInputRef} type="file" />
                <button className={secondaryButtonClass} disabled={busy} onClick={() => fileInputRef.current?.click()} type="button">
                  <Upload className="mr-2 h-4 w-4" />上传文件
                </button>
              </div>
            </div>

            <div className="flex items-center justify-between border-b border-border px-4 py-3 text-xs text-muted-foreground">
              <span>当前目录：{currentDirectory}</span>
              <span>{entries.length} 项</span>
            </div>

            <div className="flex-1 overflow-auto p-3">
              <div className="space-y-2">
                {entries.length === 0 ? (
                  <div className="rounded-2xl border border-dashed border-border bg-muted/20 px-4 py-8 text-center text-sm text-muted-foreground">当前目录为空</div>
                ) : entries.map(entry => {
                  const isSelected = selectedFilePath === entry.path
                  return (
                    <div className={`flex items-center gap-3 rounded-2xl border px-3 py-3 ${isSelected ? "border-primary bg-accent/40" : "border-border bg-card"}`} key={entry.path}>
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
                      <button className={tinyButtonClass} onClick={() => handleRename(entry)} title="重命名 / 移动" type="button">
                        <PencilLine className="h-4 w-4" />
                      </button>
                      <button className={tinyButtonClass} onClick={() => handleDuplicate(entry)} title="复制" type="button">
                        <Copy className="h-4 w-4" />
                      </button>
                      <button className={tinyButtonClass} onClick={() => void deletePath(entry.path, entry.isDirectory)} title="删除" type="button">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  )
                })}
              </div>
            </div>
          </div>
        </ResizablePanel>

        <ResizableHandle withHandle />

        <ResizablePanel defaultSize={58} minSize={32}>
          <div className="flex h-full flex-col">
            <div className="border-b border-border p-4">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="flex items-center gap-2 text-sm font-medium">
                    <span>{selectedFileName || "文件预览 / 编辑"}</span>
                    {isDirty ? <span className="rounded-full border border-amber-500/40 bg-amber-500/10 px-2 py-0.5 text-[11px] text-amber-600">未保存</span> : null}
                    {isBinaryFile ? <span className="rounded-full border border-blue-500/40 bg-blue-500/10 px-2 py-0.5 text-[11px] text-blue-600">二进制</span> : null}
                  </div>
                  <div className="mt-1 text-xs text-muted-foreground">{selectedFilePath || "从左侧选择文件后即可查看、编辑、下载。"}</div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <button className={secondaryButtonClass} disabled={!selectedFilePath || busy} onClick={() => selectedFilePath && void openFile(selectedFilePath)} type="button">重新读取</button>
                  <button className={secondaryButtonClass} disabled={!selectedFilePath} onClick={downloadSelectedFile} type="button">
                    <Download className="mr-2 h-4 w-4" />下载
                  </button>
                  <button className={primaryButtonClass} disabled={!selectedFilePath || busy || !isDirty || isBinaryFile} onClick={() => void saveFile()} type="button">
                    <Save className="mr-2 h-4 w-4" />保存文件
                  </button>
                </div>
              </div>
            </div>

            <div className="flex-1 p-4">
              {!selectedFilePath ? (
                <div className="flex h-full flex-col items-center justify-center rounded-3xl border border-dashed border-border bg-muted/20 text-center">
                  <Folder className="h-10 w-10 text-muted-foreground" />
                  <div className="mt-4 text-sm font-medium">未选择文件</div>
                  <div className="mt-2 text-sm text-muted-foreground">左侧点击文件后，在这里编辑或下载。</div>
                </div>
              ) : isBinaryFile ? (
                <div className="flex h-full flex-col items-center justify-center rounded-3xl border border-dashed border-border bg-muted/20 px-8 text-center">
                  <FileText className="h-10 w-10 text-muted-foreground" />
                  <div className="mt-4 text-sm font-medium">检测到二进制文件</div>
                  <div className="mt-2 max-w-lg text-sm text-muted-foreground">当前文件不提供在线编辑，避免误改导致内容损坏。你可以直接下载，或在本地二进制工具中处理后再上传覆盖。</div>
                </div>
              ) : (
                <textarea
                  className="h-full min-h-[36rem] w-full rounded-3xl border border-border bg-background px-4 py-3 font-mono text-sm outline-none transition focus:border-ring focus:ring-2 focus:ring-ring/20"
                  onChange={event => setEditorValue(event.target.value)}
                  onKeyDown={handleEditorKeyDown}
                  spellCheck={false}
                  value={editorValue}
                />
              )}
            </div>
          </div>
        </ResizablePanel>
      </ResizablePanelGroup>
    </Card>
  )
}
