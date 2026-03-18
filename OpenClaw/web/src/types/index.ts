export type Role = "Admin" | "Employee"
export type ViewKey = "overview" | "users" | "servers" | "templates" | "settings" | "deployments"
export type ContainerTab = "overview" | "logs" | "terminal" | "files"

export type CurrentUser = {
  id: string
  userName: string
  displayName: string
  role: Role
}

export type AdminUser = {
  id: string
  userName: string
  displayName: string
  role: Role
  status: "Active" | "Disabled"
  createdAt: string
}

export type SandboxServer = {
  id: string
  name: string
  baseUrl?: string
  apiToken?: string
  persistentRootPath?: string
  isEnabled?: boolean
  healthStatus: string
  lastCheckedAt?: string | null
  lastHealthMessage?: string | null
}

export type TemplateVersion = {
  id: string
  version: string
  image: string
  containerPort: number
  configMountPath: string
  configFileName: string
  workspaceMountPath: string
  isActive: boolean
  createdAt: string
  command: string[]
}

export type Template = {
  id: string
  name: string
  description: string
  isBuiltin?: boolean
  isEnabled?: boolean
  currentVersionId?: string | null
  versions?: TemplateVersion[]
}

export type SystemSettings = {
  id: number
  defaultCpu: string
  defaultMemory: string
  defaultLogTailLines: number
}

export type Deployment = {
  id: string
  sandboxId?: string | null
  containerId?: string | null
  apiEndpoint?: string
  apiType?: string
  model?: string
  createdAt: string
  updatedAt: string
  serverName?: string
  userName?: string
}

export type DeploymentDetail = {
  id: string
  sandboxId?: string | null
  containerId?: string | null
  createdAt: string
  updatedAt: string
  runtimeCreatedAt?: string | null
  expiresAt?: string | null
  neverExpires: boolean
  status?: string | null
  statusReason?: string | null
  statusMessage?: string | null
  cpuPercent?: number | null
  memoryPercent?: number | null
  memoryUsage?: string | null
  memoryLimit?: string | null
  server?: {
    id: string
    name: string
    healthStatus: string
  } | null
  user?: {
    id: string
    userName: string
    displayName: string
  } | null
  configSummary?: {
    apiEndpoint: string
    apiType: string
    model: string
    persistentDirectory: string
    configFilePath: string
  }
  templateSnapshot?: unknown
}

export type LogsResult = {
  lines: string[]
}

export type AppMeta = {
  application: string
  version: string
}

export type FileEntry = {
  name: string
  path: string
  isDirectory: boolean
  sizeBytes?: number | null
  lastModifiedAt?: string | null
}

export type FileListResult = {
  path: string
  entries: FileEntry[]
}

export type FileContentResult = {
  path: string
  fileName: string
  contentBase64: string
}
