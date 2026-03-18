export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  headers.set("Content-Type", "application/json")
  const response = await fetch(path, { credentials: "include", ...init, headers })

  if (response.status === 401) {
    throw new Error("请先登录")
  }
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `请求失败: ${response.status}`)
  }
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
