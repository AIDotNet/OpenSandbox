import type { ContainerTab } from "../../types"
import { primaryButtonClass, secondaryButtonClass } from "../ui/common"

type ContainerRouteTabsProps = {
  activeTab: ContainerTab
  onChange: (tab: ContainerTab) => void
}

export function ContainerRouteTabs({ activeTab, onChange }: ContainerRouteTabsProps) {
  const tabs: Array<{ key: ContainerTab; label: string }> = [
    { key: "overview", label: "现有概况" },
    { key: "logs", label: "日志" },
    { key: "terminal", label: "SSH / 终端" },
    { key: "files", label: "文件管理" },
  ]

  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => (
        <button className={activeTab === tab.key ? primaryButtonClass : secondaryButtonClass} key={tab.key} onClick={() => onChange(tab.key)} type="button">
          {tab.label}
        </button>
      ))}
    </div>
  )
}
