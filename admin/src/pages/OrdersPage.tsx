import { FormEvent, useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/modules/auth/AuthContext";
import { formatDateTime, formatFen } from "@/lib/format";
import { EmailStatusBadge, OrderStatusBadge } from "@/lib/status";
import { selectClassName } from "@/lib/utils";
import { getApiErrorMessage } from "@/services/api/client";
import { fetchOrder, fetchOrders, markOrderPaid, resendOrderEmail, testLicenseEmail } from "@/services/api/resources";
import { PaymentOrder } from "@/types/admin";

const PAGE_SIZE = 20;
const INITIAL_SEARCH = { order_id: "", email: "", status: "", email_status: "" };

export function OrdersPage() {
  const { token } = useAuth();
  const [items, setItems] = useState<PaymentOrder[]>([]);
  const [selected, setSelected] = useState<PaymentOrder | null>(null);
  const [search, setSearch] = useState(INITIAL_SEARCH);
  const [testEmail, setTestEmail] = useState("");
  const [page, setPage] = useState(1);
  const [pages, setPages] = useState(1);
  const [total, setTotal] = useState(0);
  const [listLoading, setListLoading] = useState(true);
  const [busyKey, setBusyKey] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  useEffect(() => {
    if (!token) {
      return;
    }
    void load();
  }, [page, token]);

  async function run(key: string, action: () => Promise<void>) {
    setBusyKey(key);
    setError("");
    setMessage("");
    try {
      await action();
    } catch (taskError) {
      setError(getApiErrorMessage(taskError, "订单操作失败"));
    } finally {
      setBusyKey("");
    }
  }

  async function load(overrides?: Partial<typeof INITIAL_SEARCH> & { page?: number }) {
    if (!token) {
      return;
    }
    const nextPage = overrides?.page ?? page;
    const { page: _ignored, ...searchOverrides } = overrides ?? {};
    const nextSearch = { ...search, ...searchOverrides };
    setListLoading(true);
    setError("");
    try {
      const response = await fetchOrders(token, {
        ...Object.fromEntries(Object.entries(nextSearch).filter(([, value]) => value.trim())),
        page: nextPage,
        size: PAGE_SIZE,
      });
      setItems(response.items);
      setTotal(response.total);
      setPages(response.pages || 1);
      setSelected((current) => {
        if (!current) {
          return current;
        }
        return response.items.find((item) => item.order_id === current.order_id) ?? current;
      });
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, "订单列表加载失败"));
    } finally {
      setListLoading(false);
    }
  }

  if (!token) {
    return null;
  }

  return (
    <div className="space-y-6">
      <Card className="page-shell overflow-hidden">
        <PageHeader title="支付订单" description="查询订单，补记已支付，重发授权邮件。" />
        <CardContent className="space-y-4 pt-6">
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          {message ? <p className="text-sm text-success">{message}</p> : null}
          <form
            className="grid gap-4 rounded-lg border bg-muted/20 p-4 md:grid-cols-2 xl:grid-cols-5"
            onSubmit={(event: FormEvent<HTMLFormElement>) => {
              event.preventDefault();
              if (page === 1) {
                void load({ page: 1 });
                return;
              }
              setPage(1);
            }}
          >
            <div className="space-y-2">
              <Label>订单号</Label>
              <Input value={search.order_id} placeholder="SBX" onChange={(event) => setSearch((current) => ({ ...current, order_id: event.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>邮箱</Label>
              <Input value={search.email} placeholder="按购买邮箱筛选" onChange={(event) => setSearch((current) => ({ ...current, email: event.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>订单状态</Label>
              <select className={selectClassName} value={search.status} onChange={(event) => setSearch((current) => ({ ...current, status: event.target.value }))}>
                <option value="">全部</option>
                <option value="pending">待支付</option>
                <option value="paid">已支付</option>
                <option value="expired">已过期</option>
                <option value="failed">失败</option>
                <option value="cancelled">已取消</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>邮件状态</Label>
              <select className={selectClassName} value={search.email_status} onChange={(event) => setSearch((current) => ({ ...current, email_status: event.target.value }))}>
                <option value="">全部</option>
                <option value="pending">待发送</option>
                <option value="sent">已发送</option>
                <option value="failed">失败</option>
                <option value="skipped">已跳过</option>
              </select>
            </div>
            <div className="flex items-end gap-3">
              <Button type="submit">查询</Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setSearch(INITIAL_SEARCH);
                  if (page === 1) {
                    void load({ ...INITIAL_SEARCH, page: 1 });
                    return;
                  }
                  setPage(1);
                }}
              >
                重置
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <section className="grid gap-6 xl:grid-cols-[1.45fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>订单列表</CardTitle>
            <CardDescription>共 {total} 条。</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="overflow-hidden rounded-lg border">
              {listLoading ? (
                <div className="flex min-h-[280px] items-center justify-center">
                  <Spinner />
                </div>
              ) : items.length === 0 ? (
                <div className="p-6 text-sm text-muted-foreground">当前筛选条件下没有订单。</div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y">
                    <thead className="bg-muted/30">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">订单</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">邮箱</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">状态</th>
                        <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">操作</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y bg-background">
                      {items.map((item) => (
                        <tr key={item.order_id} className="align-top">
                          <td className="px-4 py-4 text-sm">
                            <p className="font-medium">{item.order_id}</p>
                            <p className="mt-1 text-xs text-muted-foreground">{formatFen(item.amount, item.currency)}</p>
                          </td>
                          <td className="px-4 py-4 text-sm">{item.email}</td>
                          <td className="px-4 py-4 text-sm">
                            <div className="flex flex-wrap gap-2">
                              <OrderStatusBadge status={item.status} />
                              <EmailStatusBadge status={item.email_status} />
                            </div>
                            <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(item.created_at)}</p>
                          </td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end">
                              <Button
                                size="sm"
                                variant={selected?.order_id === item.order_id ? "default" : "outline"}
                                disabled={busyKey === "detail"}
                                onClick={() => {
                                  void run("detail", async () => {
                                    setSelected(await fetchOrder(token, item.order_id));
                                  });
                                }}
                              >
                                查看
                              </Button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                第 {page} / {pages} 页
              </p>
              <div className="flex gap-2">
                <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                  上一页
                </Button>
                <Button size="sm" variant="outline" disabled={page >= pages} onClick={() => setPage((current) => current + 1)}>
                  下一页
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>订单详情</CardTitle>
              <CardDescription>{selected ? selected.order_id : "从左侧选择订单。"}</CardDescription>
            </CardHeader>
            <CardContent>
              {selected ? (
                <div className="space-y-3 text-sm">
                  <div className="rounded-lg border bg-muted/20 p-4">
                    <p><span className="font-medium">状态：</span><OrderStatusBadge status={selected.status} /></p>
                    <p><span className="font-medium">金额：</span>{formatFen(selected.amount, selected.currency)}</p>
                    <p><span className="font-medium">邮箱：</span>{selected.email}</p>
                    <p><span className="font-medium">SKU：</span>{selected.sku}</p>
                    <p><span className="font-medium">支付通道：</span>{selected.provider ?? "--"}</p>
                  </div>
                  <div className="rounded-lg border bg-muted/20 p-4">
                    <p><span className="font-medium">激活码：</span>{selected.license_code ?? "--"}</p>
                    <p><span className="font-medium">邮件：</span><EmailStatusBadge status={selected.email_status} /></p>
                    <p><span className="font-medium">支付时间：</span>{formatDateTime(selected.paid_at)}</p>
                    <p><span className="font-medium">过期时间：</span>{formatDateTime(selected.expires_at)}</p>
                    <p><span className="font-medium">微信交易号：</span>{selected.wechat_transaction_id ?? "--"}</p>
                  </div>
                  <div className="rounded-lg border bg-muted/20 p-4">
                    <p className="font-medium">收款链接</p>
                    <p className="mt-2 break-all text-muted-foreground">{selected.code_url || "--"}</p>
                  </div>
                  {selected.email_error ? (
                    <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
                      <p className="font-medium">邮件错误</p>
                      <p className="mt-2 break-all">{selected.email_error}</p>
                    </div>
                  ) : null}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">选择订单后可标记已支付或重发邮件。</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>补救操作</CardTitle>
              <CardDescription>标记已支付会发码；已过期订单也会补开。</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <Button
                className="w-full"
                disabled={!selected || selected.status === "paid" || busyKey === "mark-paid"}
                onClick={() => {
                  if (!selected || !window.confirm(`确定将 ${selected.order_id} 标记为已支付？这会发放激活码。`)) {
                    return;
                  }
                  void run("mark-paid", async () => {
                    setSelected(await markOrderPaid(token, selected.order_id));
                    setMessage("已标记支付并生成激活码");
                    await load();
                  });
                }}
              >
                {busyKey === "mark-paid" ? <Spinner /> : null}
                标记已支付
              </Button>
              <Button
                className="w-full"
                variant="outline"
                disabled={!selected?.license_code || busyKey === "resend"}
                onClick={() => {
                  if (!selected) {
                    return;
                  }
                  void run("resend", async () => {
                    setSelected(await resendOrderEmail(token, selected.order_id));
                    setMessage("已重新触发授权邮件");
                    await load();
                  });
                }}
              >
                {busyKey === "resend" ? <Spinner /> : null}
                重发授权邮件
              </Button>
              <form
                className="space-y-3"
                onSubmit={(event) => {
                  event.preventDefault();
                  void run("test-email", async () => {
                    const response = await testLicenseEmail(token, testEmail);
                    setMessage(`测试邮件：${response.email_status}`);
                  });
                }}
              >
                <Label htmlFor="test-email">测试 SMTP</Label>
                <Input id="test-email" value={testEmail} placeholder="测试邮箱" onChange={(event) => setTestEmail(event.target.value)} />
                <Button type="submit" variant="outline" className="w-full" disabled={busyKey === "test-email"}>
                  {busyKey === "test-email" ? <Spinner /> : null}
                  发送测试邮件
                </Button>
              </form>
            </CardContent>
          </Card>
        </div>
      </section>
    </div>
  );
}
