import { createContext, useContext, useEffect, useState } from "react"
import type { ReactNode } from "react"
import type { CurrentUser } from "../types"
import { api } from "../services/api"

type AuthContextType = {
  me: CurrentUser | null
  loading: boolean
  login: (me: CurrentUser) => void
  logout: () => Promise<void>
  refreshSession: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)

  async function refreshSession() {
    setLoading(true)
    try {
      const current = await api<CurrentUser>("/api/auth/me")
      setMe(current)
    } catch {
      setMe(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void refreshSession()
  }, [])

  const login = (user: CurrentUser) => {
    setMe(user)
  }

  const logout = async () => {
    try {
      await api("/api/auth/logout", { method: "POST" })
    } finally {
      setMe(null)
    }
  }

  return (
    <AuthContext.Provider value={{ me, loading, login, logout, refreshSession }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider")
  }
  return context
}
