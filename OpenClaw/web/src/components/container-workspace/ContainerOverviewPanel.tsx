import type { DeploymentDetail } from "../../types"
import { Card, DetailRow, InfoBlock } from "../ui/common"
import { formatTime } from "../../lib/utils"

type ContainerOverviewPanelProps = {
  detail: DeploymentDetail
}

export function ContainerOverviewPanel({ detail }: ContainerOverviewPanelProps) {
  return (
    <div className="grid gap-6 xl:grid-cols-[1fr_0.9fr]">
      <Card title="运行概况" description="当前容器的运行状态与资源摘要。">
        <div className="grid gap-4 md:grid-cols-2">
          <DetailRow label="状态" value={detail.status || "未知"} />
          <DetailRow label="状态原因" value={detail.statusReason || "-"} />
          <DetailRow label="状态消息" value={detail.statusMessage || "-"} />
          <DetailRow label="创建时间" value={formatTime(detail.runtimeCreatedAt || detail.createdAt)} />
          <DetailRow label="不过期" value={detail.neverExpires ? "是" : "否"} />
          <DetailRow label="到期时间" value={formatTime(detail.expiresAt)} />
          <DetailRow label="内存用量" value={detail.memoryUsage || "-"} />
          <DetailRow label="内存限制" value={detail.memoryLimit || "-"} />
        </div>
      </Card>

      <Card title="部署配置摘要" description="部署时保存的业务配置与挂载路径。">
        <div className="space-y-4">
          <InfoBlock title="Endpoint" description={detail.configSummary?.apiEndpoint || "-"} />
          <InfoBlock title="API Type" description={detail.configSummary?.apiType || "-"} />
          <InfoBlock title="Model" description={detail.configSummary?.model || "-"} />
          <InfoBlock title="持久化目录" description={detail.configSummary?.persistentDirectory || "-"} />
          <InfoBlock title="配置文件" description={detail.configSummary?.configFilePath || "-"} />
        </div>
      </Card>
    </div>
  )
}
