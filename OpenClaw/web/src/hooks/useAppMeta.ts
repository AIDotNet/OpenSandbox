import { useEffect, useState } from "react"
import type { AppMeta } from "../types"

export function useAppMeta() {
  const [appMeta, setAppMeta] = useState<AppMeta | null>(null)

  useEffect(() => {
    async function loadMeta() {
      try {
        const response = await fetch("/api/meta", { credentials: "include" })
        if (response.ok) setAppMeta((await response.json()) as AppMeta)
      } catch {
      }
    }
    void loadMeta()
  }, [])

  return appMeta
}
