import { FormEvent, useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import { Spinner } from "@/components/ui/spinner";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/ui/empty-state";
import { useAuth } from "@/modules/auth/AuthContext";
import { formatDateTime } from "@/lib/format";
import { getApiErrorMessage } from "@/services/api/client";
import {
  createRelease,
  deleteRelease,
  fetchReleases,
  toggleReleaseActive,
} from "@/services/api/resources";
import { AppReleaseRecord, CreateReleasePayload } from "@/types/admin";
import { CheckCircle2, Download, ExternalLink, Power, Rocket, Trash2 } from "lucide-react";

export function ReleasesPage() {
  const { token } = useAuth();
  const [releases, setReleases] = useState<AppReleaseRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const [form, setForm] = useState<CreateReleasePayload>({
    version: "",
    min_supported_version: "1.0.0",
    download_url: "https://SideBox.microsoftware.app/downloads/SideBox-setup.exe",
    sha256: "",
    changelog: "",
    mandatory: false,
    is_active: true,
  });

  useEffect(() => {
    if (!token) {
      return;
    }
    void load();
  }, [token]);

  async function load() {
    if (!token) {
      return;
    }
    setLoading(true);
    try {
      const res = await fetchReleases(token);
      setReleases(res.items);
    } catch (err) {
      setError(getApiErrorMessage(err, "加载版本列表失败"));
    } finally {
      setLoading(false);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!token) {
      return;
    }
    if (!form.version.trim()) {
      setError("请填写版本号");
      return;
    }
    if (!form.download_url.trim()) {
      setError("请填写下载地址");
      return;
    }

    setSubmitting(true);
    setError("");
    setMessage("");

    try {
      await createRelease(token, form);
      setMessage(`版本 v${form.version} 发布成功！客户端检查更新即刻生效。`);
      setForm({
        version: "",
        min_supported_version: "1.0.0",
        download_url: "https://SideBox.microsoftware.app/downloads/SideBox-setup.exe",
        sha256: "",
        changelog: "",
        mandatory: false,
        is_active: true,
      });
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err, "发布版本失败"));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleToggle(id: number) {
    if (!token) {
      return;
    }
    setBusyId(id);
    setError("");
    setMessage("");
    try {
      await toggleReleaseActive(token, id);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err, "切换状态失败"));
    } finally {
      setBusyId(null);
    }
  }

  async function handleDelete(id: number, version: string) {
    if (!token) {
      return;
    }
    if (!window.confirm(`确定删除版本记录 v${version} 吗？`)) {
      return;
    }
    setBusyId(id);
    setError("");
    setMessage("");
    try {
      await deleteRelease(token, id);
      setMessage(`版本 v${version} 已删除`);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err, "删除版本失败"));
    } finally {
      setBusyId(null);
    }
  }

  const activeRelease = releases.find((r) => r.is_active);

  return (
    <div className="space-y-6">
      <PageHeader
        title="客户端版本发布"
        description="在线管理 SideBox 客户端更新版本。发版直接在此填写并提交，客户端检查更新即时生效，无需重启服务器。"
      />

      {error ? (
        <div className="rounded-lg border border-destructive/20 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      ) : null}

      {message ? (
        <div className="rounded-lg border border-success/20 bg-success/10 px-4 py-3 text-sm text-success">
          {message}
        </div>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Release Form */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Rocket className="size-4 text-primary" />
              发布新版本
            </CardTitle>
            <CardDescription>
              填写版本号、下载链接与更新说明。发布后将作为最新版本推送给所有客户端。
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="version">版本号 *</Label>
                  <Input
                    id="version"
                    placeholder="例如 1.1.0"
                    value={form.version}
                    onChange={(e) => setForm({ ...form, version: e.target.value })}
                    required
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="min_supported_version">最低兼容版本</Label>
                  <Input
                    id="min_supported_version"
                    placeholder="例如 1.0.0"
                    value={form.min_supported_version}
                    onChange={(e) => setForm({ ...form, min_supported_version: e.target.value })}
                  />
                </div>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="download_url">安装包下载地址 *</Label>
                <Input
                  id="download_url"
                  placeholder="https://SideBox.microsoftware.app/downloads/SideBox-setup.exe"
                  value={form.download_url}
                  onChange={(e) => setForm({ ...form, download_url: e.target.value })}
                  required
                />
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="sha256">安装包 SHA-256 哈希值 (可选)</Label>
                <Input
                  id="sha256"
                  placeholder="留空则不强制校验哈希"
                  value={form.sha256}
                  onChange={(e) => setForm({ ...form, sha256: e.target.value })}
                />
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="changelog">更新日志 (Changelog)</Label>
                <textarea
                  id="changelog"
                  rows={4}
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  placeholder="- 新增多屏幕悬浮岛独立支持&#10;- 优化时钟小组件流畅度&#10;- 修复已知问题"
                  value={form.changelog}
                  onChange={(e) => setForm({ ...form, changelog: e.target.value })}
                />
              </div>

              <div className="flex flex-wrap items-center gap-6 pt-1">
                <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="size-4 rounded border-gray-300 text-primary focus:ring-primary"
                    checked={form.is_active}
                    onChange={(e) => setForm({ ...form, is_active: e.target.checked })}
                  />
                  <span>立即生效（作为当前有效版本）</span>
                </label>

                <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="size-4 rounded border-gray-300 text-primary focus:ring-primary"
                    checked={form.mandatory}
                    onChange={(e) => setForm({ ...form, mandatory: e.target.checked })}
                  />
                  <span className="text-warning">标记为强制更新</span>
                </label>
              </div>

              <div className="pt-2">
                <Button type="submit" disabled={submitting}>
                  {submitting ? <Spinner className="mr-2 size-4" /> : <Rocket className="mr-2 size-4" />}
                  立即发布
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        {/* Current Active Release Overview */}
        <Card className="flex flex-col">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <CheckCircle2 className="size-4 text-success" />
              当前生效版本
            </CardTitle>
            <CardDescription>客户端发起检查更新时接收到的版本信息</CardDescription>
          </CardHeader>
          <CardContent className="flex-1 space-y-4">
            {activeRelease ? (
              <div className="space-y-4 rounded-lg border border-success/20 bg-success/5 p-4">
                <div className="flex items-center justify-between">
                  <span className="text-2xl font-bold tracking-tight">v{activeRelease.version}</span>
                  <Badge variant="success">生效中</Badge>
                </div>

                <div className="space-y-1 text-xs text-muted-foreground">
                  <div>最低兼容：v{activeRelease.min_supported_version}</div>
                  <div>强制更新：{activeRelease.mandatory ? "是" : "否"}</div>
                  <div>发布时间：{formatDateTime(activeRelease.created_at)}</div>
                </div>

                <div className="space-y-1">
                  <span className="text-xs font-medium text-muted-foreground">下载链接：</span>
                  <a
                    href={activeRelease.download_url}
                    target="_blank"
                    rel="noreferrer"
                    className="flex items-center gap-1 text-xs text-primary underline truncate"
                  >
                    <Download className="size-3 shrink-0" />
                    <span className="truncate">{activeRelease.download_url}</span>
                  </a>
                </div>

                {activeRelease.changelog ? (
                  <div className="space-y-1">
                    <span className="text-xs font-medium text-muted-foreground">更新说明：</span>
                    <pre className="whitespace-pre-wrap rounded bg-muted/50 p-2 text-xs font-sans text-foreground">
                      {activeRelease.changelog}
                    </pre>
                  </div>
                ) : null}
              </div>
            ) : (
              <div className="rounded-lg border border-dashed p-4 text-center text-sm text-muted-foreground">
                <p>当前数据库中暂无激活的版本。</p>
                <p className="mt-1 text-xs text-muted-foreground/80">
                  此时接口自动使用服务器配置中的默认版本兜底。
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Historical Releases */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">历史版本记录</CardTitle>
          <CardDescription>管理所有已发布的版本。可以随时停用、重新启用或删除。</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex h-32 items-center justify-center">
              <Spinner className="size-6 text-muted-foreground" />
            </div>
          ) : releases.length === 0 ? (
            <EmptyState title="暂无发布记录" description="发布第一个版本后，历史记录将展示在此处。" />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b text-xs text-muted-foreground">
                    <th className="pb-3 font-medium">版本</th>
                    <th className="pb-3 font-medium">状态</th>
                    <th className="pb-3 font-medium">下载地址</th>
                    <th className="pb-3 font-medium">更新说明</th>
                    <th className="pb-3 font-medium">发布时间</th>
                    <th className="pb-3 font-medium text-right">操作</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {releases.map((item) => (
                    <tr key={item.id} className="hover:bg-muted/40 transition-colors">
                      <td className="py-3 font-medium">
                        <div className="flex items-center gap-1.5">
                          <span>v{item.version}</span>
                          {item.mandatory ? (
                            <span className="rounded bg-destructive/10 px-1 py-0.5 text-[10px] text-destructive">
                              强更
                            </span>
                          ) : null}
                        </div>
                      </td>
                      <td className="py-3">
                        {item.is_active ? (
                          <Badge variant="success">已生效</Badge>
                        ) : (
                          <Badge variant="muted">已下线</Badge>
                        )}
                      </td>
                      <td className="py-3 max-w-[240px] truncate text-xs">
                        <a
                          href={item.download_url}
                          target="_blank"
                          rel="noreferrer"
                          className="text-primary hover:underline flex items-center gap-1"
                        >
                          <span className="truncate">{item.download_url}</span>
                          <ExternalLink className="size-3 shrink-0" />
                        </a>
                      </td>
                      <td className="py-3 max-w-[200px] truncate text-xs text-muted-foreground">
                        {item.changelog || "—"}
                      </td>
                      <td className="py-3 text-xs text-muted-foreground">
                        {formatDateTime(item.created_at)}
                      </td>
                      <td className="py-3 text-right space-x-2 whitespace-nowrap">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-8 text-xs"
                          disabled={busyId === item.id}
                          onClick={() => handleToggle(item.id)}
                        >
                          <Power className="mr-1 size-3.5" />
                          {item.is_active ? "下线" : "生效"}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-8 text-xs text-destructive hover:bg-destructive/10"
                          disabled={busyId === item.id}
                          onClick={() => handleDelete(item.id, item.version)}
                        >
                          <Trash2 className="size-3.5" />
                        </Button>
                      </td>
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
