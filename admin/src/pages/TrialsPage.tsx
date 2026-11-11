import { FormEvent, useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/modules/auth/AuthContext";
import { formatDateTime } from "@/lib/format";
import { EmailStatusBadge, LicenseStatusBadge } from "@/lib/status";
import { selectClassName } from "@/lib/utils";
import { getApiErrorMessage } from "@/services/api/client";
import { createTrial, fetchTrials } from "@/services/api/resources";
import { LicenseRecord } from "@/types/admin";

const PAGE_SIZE = 20;
const INITIAL_SEARCH = { email: "", status: "" };

export function TrialsPage() {
  const { token } = useAuth();
  const [items, setItems] = useState<LicenseRecord[]>([]);
  const [search, setSearch] = useState(INITIAL_SEARCH);
  const [email, setEmail] = useState("");
  const [sendEmail, setSendEmail] = useState(true);
  const [result, setResult] = useState<LicenseRecord | null>(null);
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
      setError(getApiErrorMessage(taskError, "试用操作失败"));
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
      const response = await fetchTrials(token, {
        ...Object.fromEntries(Object.entries(nextSearch).filter(([, value]) => value.trim())),
        page: nextPage,
        size: PAGE_SIZE,
      });
      setItems(response.items);
      setTotal(response.total);
      setPages(response.pages || 1);
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, "试用列表加载失败"));
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
        <PageHeader title="试用" description="管理端代领只发卡，不占用设备名额。每个邮箱一次。" />
        <CardContent className="pt-6">
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          {message ? <p className="mt-3 text-sm text-success">{message}</p> : null}
        </CardContent>
      </Card>

      <section className="grid gap-6 xl:grid-cols-[1.5fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>申请记录</CardTitle>
            <CardDescription>共 {total} 条。</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              className="grid gap-4 rounded-lg border bg-muted/20 p-4 md:grid-cols-[1fr_220px_auto]"
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
                <Label>邮箱</Label>
                <Input value={search.email} placeholder="按申请邮箱筛选" onChange={(event) => setSearch((current) => ({ ...current, email: event.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>状态</Label>
                <select className={selectClassName} value={search.status} onChange={(event) => setSearch((current) => ({ ...current, status: event.target.value }))}>
                  <option value="">全部</option>
                  <option value="valid">有效</option>
                  <option value="expired">已过期</option>
                  <option value="revoked">已作废</option>
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

            <div className="overflow-hidden rounded-lg border">
              {listLoading ? (
                <div className="flex min-h-[280px] items-center justify-center">
                  <Spinner />
                </div>
              ) : items.length === 0 ? (
                <div className="p-6 text-sm text-muted-foreground">当前筛选条件下没有试用记录。</div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y">
                    <thead className="bg-muted/30">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">邮箱</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">激活码</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">状态</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">设备</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y bg-background">
                      {items.map((item) => (
                        <tr key={item.id} className="align-top">
                          <td className="px-4 py-4 text-sm">{item.email}</td>
                          <td className="px-4 py-4 text-sm font-medium">{item.code}</td>
                          <td className="px-4 py-4 text-sm">
                            <LicenseStatusBadge status={item.status} />
                            <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(item.expires_at)}</p>
                          </td>
                          <td className="px-4 py-4 text-sm">
                            {item.device_count}/{item.device_limit}
                            <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(item.created_at)}</p>
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

        <Card>
          <CardHeader>
            <CardTitle>代领试用</CardTitle>
            <CardDescription>只生成试用码，用户自己在设置里激活。</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <form
              className="space-y-4 rounded-lg border bg-muted/20 p-5"
              onSubmit={(event: FormEvent<HTMLFormElement>) => {
                event.preventDefault();
                void run("claim", async () => {
                  const response = await createTrial(token, email, sendEmail);
                  setResult(response);
                  setEmail("");
                  setMessage("试用码已发放");
                  if (page === 1) {
                    await load({ page: 1 });
                  } else {
                    setPage(1);
                  }
                });
              }}
            >
              <div className="space-y-2">
                <Label htmlFor="trial-email">邮箱</Label>
                <Input id="trial-email" value={email} placeholder="用户邮箱" onChange={(event) => setEmail(event.target.value)} />
              </div>
              <label className="flex items-center gap-3 text-sm">
                <input type="checkbox" checked={sendEmail} onChange={(event) => setSendEmail(event.target.checked)} />
                同时发邮件（未配置 SMTP 会标记为 skipped）
              </label>
              <Button type="submit" disabled={busyKey === "claim"}>
                {busyKey === "claim" ? <Spinner /> : null}
                发放试用码
              </Button>
            </form>

            <div className="rounded-lg border bg-background p-5">
              <h2 className="text-lg font-semibold">最近一次结果</h2>
              {result ? (
                <div className="mt-4 space-y-3 text-sm">
                  <p><span className="font-medium">邮箱：</span>{result.email}</p>
                  <p><span className="font-medium">激活码：</span>{result.code}</p>
                  <p><span className="font-medium">到期：</span>{formatDateTime(result.expires_at)}</p>
                  <p><span className="font-medium">设备：</span>{result.device_count}/{result.device_limit}</p>
                  {result.email_status ? <p><span className="font-medium">邮件：</span><EmailStatusBadge status={result.email_status} /></p> : null}
                </div>
              ) : (
                <p className="mt-4 text-sm text-muted-foreground">提交后会在这里显示激活码。</p>
              )}
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
