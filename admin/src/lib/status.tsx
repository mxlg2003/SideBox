import { Badge } from "@/components/ui/badge";

const LICENSE_LABELS: Record<string, string> = {
  valid: "有效",
  expired: "已过期",
  revoked: "已作废",
};

const LICENSE_VARIANTS: Record<string, "success" | "warning" | "destructive" | "muted"> = {
  valid: "success",
  expired: "warning",
  revoked: "destructive",
};

const ORDER_LABELS: Record<string, string> = {
  pending: "待支付",
  paid: "已支付",
  expired: "已过期",
  failed: "失败",
  cancelled: "已取消",
};

const ORDER_VARIANTS: Record<string, "success" | "warning" | "destructive" | "muted" | "default"> = {
  pending: "warning",
  paid: "success",
  expired: "muted",
  failed: "destructive",
  cancelled: "muted",
};

const EMAIL_LABELS: Record<string, string> = {
  pending: "待发送",
  sent: "已发送",
  failed: "失败",
  skipped: "已跳过",
};

export function LicenseStatusBadge({ status }: { status: string }) {
  return <Badge variant={LICENSE_VARIANTS[status] ?? "muted"}>{LICENSE_LABELS[status] ?? status}</Badge>;
}

export function OrderStatusBadge({ status }: { status: string }) {
  return <Badge variant={ORDER_VARIANTS[status] ?? "muted"}>{ORDER_LABELS[status] ?? status}</Badge>;
}

export function EmailStatusBadge({ status }: { status: string }) {
  return <Badge variant={status === "failed" ? "destructive" : status === "sent" ? "success" : "muted"}>{EMAIL_LABELS[status] ?? status}</Badge>;
}
