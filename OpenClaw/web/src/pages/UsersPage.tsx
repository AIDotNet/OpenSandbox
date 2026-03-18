import { useState } from "react"
import type { FormEvent } from "react"
import { useData } from "../hooks/useData"
import { api } from "../services/api"
import { Card, TableWrap, Th, Td, StatusBadge, primaryButtonClass, tinyButtonClass, secondaryButtonClass, inputClass } from "../components/ui/common"
import { formatTime } from "../lib/utils"
import type { Role, AdminUser } from "../types"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"

const emptyUserForm = { userName: "", displayName: "", password: "", role: "Employee" as Role }

export default function UsersPage() {
  const { users, loadAdminData } = useData()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [userForm, setUserForm] = useState(emptyUserForm)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  async function handleCreateUser(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      await api("/api/admin/users", { method: "POST", body: JSON.stringify(userForm) })
      setUserForm(emptyUserForm)
      setDialogOpen(false)
      await loadAdminData()
      setMessage("用户已创建")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "创建失败")
    } finally {
      setBusy(false)
    }
  }

  async function deleteUser(id: string) {
    setBusy(true)
    try {
      await api(`/api/admin/users/${id}`, { method: "DELETE" })
      await loadAdminData()
      setMessage("用户已删除")
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "删除失败")
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      {message ? <div className="mb-4 rounded-2xl border border-border bg-muted/40 px-4 py-3 text-sm text-foreground">{message}</div> : null}
      
      <Card title="账号列表" description="统一维护管理员和员工账号。" actions={<button className={primaryButtonClass} onClick={() => setDialogOpen(true)} type="button">新增用户</button>}>
        <TableWrap>
          <table className="min-w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <Th>用户名</Th>
                <Th>显示名</Th>
                <Th>角色</Th>
                <Th>状态</Th>
                <Th>创建时间</Th>
                <Th className="text-right">操作</Th>
              </tr>
            </thead>
            <tbody>
              {users.map((user: AdminUser) => (
                <tr className="border-t border-border" key={user.id}>
                  <Td>{user.userName}</Td>
                  <Td>{user.displayName}</Td>
                  <Td><StatusBadge label={user.role} tone={user.role === "Admin" ? "brand" : "neutral"} /></Td>
                  <Td><StatusBadge label={user.status} tone={user.status === "Active" ? "success" : "danger"} /></Td>
                  <Td>{formatTime(user.createdAt)}</Td>
                  <Td className="text-right"><button className={tinyButtonClass} onClick={() => void deleteUser(user.id)} type="button">删除</button></Td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableWrap>
      </Card>

      <Dialog onOpenChange={setDialogOpen} open={dialogOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>新增用户</DialogTitle>
            <DialogDescription>在弹窗中创建管理员或员工账号。</DialogDescription>
          </DialogHeader>
          <form className="grid gap-3" onSubmit={handleCreateUser}>
            <input className={inputClass} placeholder="用户名" value={userForm.userName} onChange={(event) => setUserForm((value) => ({ ...value, userName: event.target.value }))} />
            <input className={inputClass} placeholder="显示名" value={userForm.displayName} onChange={(event) => setUserForm((value) => ({ ...value, displayName: event.target.value }))} />
            <input className={inputClass} placeholder="密码" type="password" value={userForm.password} onChange={(event) => setUserForm((value) => ({ ...value, password: event.target.value }))} />
            <select className={inputClass} value={userForm.role} onChange={(event) => setUserForm((value) => ({ ...value, role: event.target.value as Role }))}>
              <option value="Employee">员工</option>
              <option value="Admin">管理员</option>
            </select>
            <DialogFooter>
              <button className={secondaryButtonClass} onClick={() => setDialogOpen(false)} type="button">取消</button>
              <button className={primaryButtonClass} disabled={busy} type="submit">创建用户</button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}
