import { FormEvent, type ReactNode, useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useAuth } from "@/modules/auth/AuthContext";
import { formatDateTime } from "@/lib/format";
import { LicenseStatusBadge } from "@/lib/status";
import { selectClassName } from "@/lib/utils";
import { getApiErrorMessage } from "@/services/api/client";
import {
  extendLicense,
  fetchLicense,
  fetchLicenses,
  issueLicenses,
  resendLicenseEmail,
  revokeLicense,
  unbindLicenseDevice,
} from "@/services/api/resources";
import { LicenseRecord } from "@/types/admin";

const PAGE_SIZE = 20;
const INITIAL_SEARCH = { code: "", email: "", kind: "", status: "" };

export function LicensesPage() {
  const { token } = useAuth();
  const [items, setItems] = useState<LicenseRecord[]>([]);
  const [selected, setSelected] = useState<LicenseRecord | null>(null);
  const [search, setSearch] = useState(INITIAL_SEARCH);
  const [issue, setIssue] = useState({ email: "", kind: "pro", days: 14, count: 1, deviceLimit: 1 });
  const [extendDays, setExtendDays] = useState(14);
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
      setError(getApiErrorMessage(taskError, "操作失败"));
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
      const response = await fetchLicenses(token, {
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
        return response.items.find((item) => item.id === current.id) ?? current;
      });
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, "激活码列表加载失败"));
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
        <PageHeader title="激活码" description="查询、发放、解绑、延期和作废。试用每个邮箱只能发一张。" />
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
            <Field label="激活码">
              <Input value={search.code} placeholder="SBX-" onChange={(event) => setSearch((current) => ({ ...current, code: event.target.value }))} />
            </Field>
            <Field label="邮箱">
              <Input value={search.email} placeholder="按邮箱筛选" onChange={(event) => setSearch((current) => ({ ...current, email: event.target.value }))} />
            </Field>
            <Field label="类型">
              <select className={selectClassName} value={search.kind} onChange={(event) => setSearch((current) => ({ ...current, kind: event.target.value }))}>
                <option value="">全部</option>
                <option value="pro">专业版</option>
                <option value="trial">试用</option>
              </select>
            </Field>
            <Field label="状态">
              <select className={selectClassName} value={search.status} onChange={(event) => setSearch((current) => ({ ...current, status: event.target.value }))}>
                <option value="">全部</option>
                <option value="valid">有效</option>
                <option value="expired">已过期</option>
                <option value="revoked">已作废</option>
              </select>
            </Field>
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
            <CardTitle>列表</CardTitle>
            <CardDescription>共 {total} 条，按创建时间倒序。</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="overflow-hidden rounded-lg border">
              {listLoading ? (
                <div className="flex min-h-[280px] items-center justify-center">
                  <Spinner />
                </div>
              ) : items.length === 0 ? (
                <div className="p-4">
                  <EmptyState title="暂无激活码" description="可以在右侧发放一张，或等用户购买后再查询。" />
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y">
                    <thead className="bg-muted/30">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">激活码</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">邮箱</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">状态</th>
                        <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">操作</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y bg-background">
                      {items.map((item) => (
                        <tr key={item.id} className="align-top">
                          <td className="px-4 py-4 text-sm">
                            <p className="font-medium">{item.code}</p>
                            <p className="mt-1 text-xs text-muted-foreground">{item.kind === "pro" ? "专业版" : "试用"} · {item.device_count}/{item.device_limit} 台</p>
                          </td>
                          <td className="px-4 py-4 text-sm">{item.email}</td>
                          <td className="px-4 py-4 text-sm">
                            <LicenseStatusBadge status={item.status} />
                            <p className="mt-1 text-xs text-muted-foreground">{item.permanent ? "永久" : formatDateTime(item.expires_at)}</p>
                          </td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end">
                              <Button
                                size="sm"
                                variant={selected?.id === item.id ? "default" : "outline"}
                                disabled={busyKey === "detail"}
                                onClick={() => {
                                  void run("detail", async () => {
                                    setSelected(await fetchLicense(token, item.id));
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
            <Pager page={page} pages={pages} onPrev={() => setPage((current) => current - 1)} onNext={() => setPage((current) => current + 1)} />
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>发放激活码</CardTitle>
              <CardDescription>专业版可批量；试用一次一张，且不绑定设备。</CardDescription>
            </CardHeader>
            <CardContent>
              <form
                className="space-y-4"
                onSubmit={(event) => {
                  event.preventDefault();
                  void run("issue", async () => {
                    const response = await issueLicenses(token, {
                      email: issue.email || "gift@SideBox.local",
                      kind: issue.kind,
                      days: issue.days,
                      count: issue.kind === "pro" ? issue.count : 1,
                      device_limit: issue.kind === "pro" ? issue.deviceLimit : 1,
                    });
                    setMessage(`已发放 ${response.items.length} 张：${response.items.map((item) => item.code).join("、")}`);
                    setSelected(response.items[0] ?? null);
                    await load({ page: 1 });
                    setPage(1);
                  });
                }}
              >
                <Field label="邮箱">
                  <Input value={issue.email} placeholder="空则写入 gift@SideBox.local" onChange={(event) => setIssue((current) => ({ ...current, email: event.target.value }))} />
                </Field>
                <Field label="类型">
                  <select className={selectClassName} value={issue.kind} onChange={(event) => setIssue((current) => ({ ...current, kind: event.target.value }))}>
                    <option value="pro">永久专业版</option>
                    <option value="trial">试用</option>
                  </select>
                </Field>
                {issue.kind === "trial" ? (
                  <Field label="试用天数">
                    <Input type="number" min={1} max={3650} value={issue.days} onChange={(event) => setIssue((current) => ({ ...current, days: Number(event.target.value || 14) }))} />
                  </Field>
                ) : (
                  <>
                    <Field label="可绑台数">
                      <select className={selectClassName} value={issue.deviceLimit} onChange={(event) => setIssue((current) => ({ ...current, deviceLimit: Number(event.target.value || 1) }))}>
                        <option value={1}>1 台 · ¥29</option>
                        <option value={2}>2 台 · ¥39</option>
                        <option value={3}>3 台 · ¥49</option>
                      </select>
                    </Field>
                    <Field label="数量">
                      <Input type="number" min={1} max={50} value={issue.count} onChange={(event) => setIssue((current) => ({ ...current, count: Number(event.target.value || 1) }))} />
                    </Field>
                  </>
                )}
                <Button type="submit" disabled={busyKey === "issue"}>
                  {busyKey === "issue" ? <Spinner /> : null}
                  发放
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>详情与补救</CardTitle>
              <CardDescription>{selected ? selected.code : "从左侧选择一条激活码。"}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {selected ? (
                <>
                  <div className="space-y-2 rounded-lg border bg-muted/20 p-4 text-sm">
                    <p><span className="font-medium">邮箱：</span>{selected.email}</p>
                    <p><span className="font-medium">状态：</span><LicenseStatusBadge status={selected.status} /></p>
                    <p><span className="font-medium">到期：</span>{selected.permanent ? "永久" : formatDateTime(selected.expires_at)}</p>
                    <p><span className="font-medium">设备：</span>{selected.device_count}/{selected.device_limit}</p>
                  </div>
                  <div className="space-y-2">
                    {(selected.devices ?? []).length === 0 ? (
                      <p className="text-sm text-muted-foreground">还没有绑定设备。</p>
                    ) : (
                      selected.devices?.map((device) => (
                        <div key={device.device_id} className="flex items-center justify-between gap-3 rounded-lg border bg-background px-3 py-2 text-sm">
                          <div>
                            <p className="font-medium">{device.device_name || device.device_id}</p>
                            <p className="text-xs text-muted-foreground">{device.device_id} · {formatDateTime(device.bound_at)}</p>
                          </div>
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={busyKey === `unbind-${device.device_id}`}
                            onClick={() => {
                              if (!window.confirm(`解绑 ${device.device_id}？`)) {
                                return;
                              }
                              void run(`unbind-${device.device_id}`, async () => {
                                setSelected(await unbindLicenseDevice(token, selected.id, device.device_id));
                                setMessage("已解绑设备");
                                await load();
                              });
                            }}
                          >
                            解绑
                          </Button>
                        </div>
                      ))
                    )}
                  </div>
                  <div className="grid gap-3">
                    <Button
                      variant="outline"
                      disabled={busyKey === "resend"}
                      onClick={() => {
                        void run("resend", async () => {
                          const updated = await resendLicenseEmail(token, selected.id);
                          setSelected(updated);
                          setMessage(updated.email_status === "sent" ? "邮件已发送" : `邮件状态：${updated.email_status}`);
                        });
                      }}
                    >
                      {busyKey === "resend" ? <Spinner /> : null}
                      补发激活码邮件
                    </Button>
                    <form
                      className="grid gap-3 md:grid-cols-[1fr_auto]"
                      onSubmit={(event) => {
                        event.preventDefault();
                        void run("extend", async () => {
                          setSelected(await extendLicense(token, selected.id, extendDays));
                          setMessage(`已延长 ${extendDays} 天`);
                          await load();
                        });
                      }}
                    >
                      <Input type="number" min={1} max={3650} value={extendDays} onChange={(event) => setExtendDays(Number(event.target.value || 1))} />
                      <Button type="submit" variant="outline" disabled={busyKey === "extend" || selected.permanent}>
                        延期
                      </Button>
                    </form>
                    <Button
                      variant="destructive"
                      disabled={busyKey === "revoke" || selected.revoked}
                      onClick={() => {
                        if (!window.confirm(`确定作废 ${selected.code}？此操作不可恢复。`)) {
                          return;
                        }
                        void run("revoke", async () => {
                          setSelected(await revokeLicense(token, selected.id));
                          setMessage("激活码已作废");
                          await load();
                        });
                      }}
                    >
                      {busyKey === "revoke" ? <Spinner /> : null}
                      作废
                    </Button>
                  </div>
                </>
              ) : (
                <p className="text-sm text-muted-foreground">选择激活码后可解绑设备、补发邮件或作废。</p>
              )}
            </CardContent>
          </Card>
        </div>
      </section>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}

function Pager({ page, pages, onPrev, onNext }: { page: number; pages: number; onPrev: () => void; onNext: () => void }) {
  return (
    <div className="flex items-center justify-between">
      <p className="text-sm text-muted-foreground">
        第 {page} / {pages} 页
      </p>
      <div className="flex gap-2">
        <Button size="sm" variant="outline" disabled={page <= 1} onClick={onPrev}>
          上一页
        </Button>
        <Button size="sm" variant="outline" disabled={page >= pages} onClick={onNext}>
          下一页
        </Button>
      </div>
    </div>
  );
}
