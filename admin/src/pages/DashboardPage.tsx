import { useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/modules/auth/AuthContext";
import { formatDateTime, formatFen } from "@/lib/format";
import { EmailStatusBadge, OrderStatusBadge } from "@/lib/status";
import { getApiErrorMessage } from "@/services/api/client";
import { fetchConfig, fetchSummary } from "@/services/api/resources";
import { AdminConfig, AdminSummary } from "@/types/admin";

export function DashboardPage() {
  const { token } = useAuth();
  const [summary, setSummary] = useState<AdminSummary | null>(null);
  const [config, setConfig] = useState<AdminConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!token) {
      return;
    }

    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError("");
      try {
        const [nextSummary, nextConfig] = await Promise.all([fetchSummary(token), fetchConfig(token)]);
        if (!cancelled) {
          setSummary(nextSummary);
          setConfig(nextConfig);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(getApiErrorMessage(loadError, "仪表盘加载失败"));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [token]);

  return (
    <div className="space-y-6">
      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <section className="grid gap-6 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>激活码</CardTitle>
            <CardDescription>按当前是否仍可用拆分。</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <Stat label="专业版有效" value={summary?.licenses.pro_active} loading={loading} />
            <Stat label="试用有效" value={summary?.licenses.trial_active} loading={loading} />
            <Stat label="已过期 / 作废" value={summary?.licenses.expired_or_revoked} loading={loading} />
            <Stat label="全部" value={summary?.licenses.total} loading={loading} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>订单</CardTitle>
            <CardDescription>今日按东八区自然日统计已支付。</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <Stat label="待支付" value={summary?.orders.pending} loading={loading} />
            <Stat label="已支付" value={summary?.orders.paid} loading={loading} />
            <Stat label="今日笔数" value={summary?.orders.today_paid_count} loading={loading} />
            <Stat label="今日收款" value={loading ? undefined : formatFen(summary?.orders.today_paid_amount)} loading={loading} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>运行配置</CardTitle>
            <CardDescription>只读，改 .env 后重启后端生效。</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <Row label="支付模式" value={config?.provider ?? "--"} />
            <Row
              label="定价"
              value={
                config?.skus?.length
                  ? config.skus.map((item) => `${item.device_limit} 台 ${formatFen(item.amount)}`).join(" / ")
                  : config
                    ? formatFen(config.pro_price_fen)
                    : "--"
              }
            />
            <Row
              label="增购"
              value={
                config?.addon
                  ? `${config.addon.extra_devices} 台 ${formatFen(config.addon.amount)}`
                  : "1 台 ¥10.00"
              }
            />
            <Row label="试用天数" value={config ? String(config.trial_days) : "--"} />
            <Row label="套餐最多" value="3 台" />
            <Row label="SMTP" value={config ? (config.smtp_configured ? "已配置" : "未配置") : "--"} />
            <Row label="微信收款" value={config ? (config.wechat_ready ? "已就绪" : "未就绪") : "--"} />
            <Row label="试用领取" value={summary ? String(summary.trials) : "--"} />
            <Row label="设备绑定" value={summary ? String(summary.device_bindings) : "--"} />
          </CardContent>
        </Card>
      </section>

      <Card>
        <CardHeader>
          <CardTitle>最近已支付订单</CardTitle>
          <CardDescription>最多显示 10 条。</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-10">
              <Spinner />
            </div>
          ) : !summary?.recent_orders.length ? (
            <EmptyState title="还没有已支付订单" description="支付完成或在订单页标记已支付后，会出现在这里。" />
          ) : (
            <div className="overflow-x-auto rounded-lg border">
              <table className="w-full min-w-[640px] text-left text-sm">
                <thead className="border-b bg-muted/40 text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 font-medium">订单</th>
                    <th className="px-4 py-3 font-medium">邮箱</th>
                    <th className="px-4 py-3 font-medium">金额</th>
                    <th className="px-4 py-3 font-medium">状态</th>
                    <th className="px-4 py-3 font-medium">激活码</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.recent_orders.map((item) => (
                    <tr key={item.order_id} className="border-b last:border-0">
                      <td className="px-4 py-3">
                        <p className="font-medium">{item.order_id}</p>
                        <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(item.paid_at)}</p>
                      </td>
                      <td className="px-4 py-3">{item.email}</td>
                      <td className="px-4 py-3">{formatFen(item.amount, item.currency)}</td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap gap-2">
                          <OrderStatusBadge status={item.status} />
                          <EmailStatusBadge status={item.email_status} />
                        </div>
                      </td>
                      <td className="px-4 py-3 font-medium">{item.license_code ?? "--"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function Stat({ label, value, loading }: { label: string; value?: string | number; loading: boolean }) {
  return (
    <div className="rounded-lg border bg-muted/40 p-4">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-2 text-xl font-semibold">{loading ? "--" : (value ?? "--")}</p>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}
