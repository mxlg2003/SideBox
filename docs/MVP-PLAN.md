# WinDock MVP 计划

对应 2 名熟悉 Windows 桌面的开发者、约 6–9 人月。本文把报告阶段 1+2 切成可验收里程碑。

## 人员切分建议

- **A：宿主** — Shell、Dock 窗口、DPI、显示器、托盘、自动隐藏。
- **B：产品逻辑** — Core ProfileEngine、设置、widget 视图、Providers。

M0 两人一起完成骨架。之后按垂直切片并行，接口以 `WinDock.Core` 为准。

## M0 骨架（约 1 周）

交付：

- 本文档集已入库
- `src/WinDock.sln` 可 `dotnet build` / `dotnet test`
- 一个无边框 Topmost 窗口出现在主屏顶边
- 托盘可退出
- `--safe` 可解析

验收：

- Debug 启动后 3 秒内看到 Dock 条
- 托盘 Exit 后进程退出，AppBar 未残留（M0 尚未注册 AppBar）

## M1 宿主地基（约 4–6 周）

交付：

- 上 / 左 / 右边缘吸附；默认避开主屏底边任务栏
- 多屏拖动；断开屏上的窗口迁主屏
- Per-Monitor v2：125% / 150% / 200% 图标与文本清晰
- Explorer 拖入 app/file/folder；内部排序
- JSON 原子持久化；崩溃后位置恢复
- 自动隐藏 + 8px 热区
- `SHQueryUserNotificationState` 全屏 / 演示避让
- `TaskbarCreated` 恢复托盘

验收矩阵：

- 主屏 + 副屏插拔
- 锁屏 / 解锁
- 休眠恢复（能测则测）
- DPI 切换不模糊、不跑到错误坐标

## M2 手动 Profile（约 2–3 周）

交付：

- 至少 `general` 与 `deep-work` 两条；设置可增删改（日常不可删）
- 热键、托盘、设置页手动切换；Focus / 拓扑 / 时间自动切换仍关闭
- 预览、撤销、最近 100 条历史
- 切换只改 Dock 集合和 widget 显隐，不移动用户窗口

验收：

- 热键切换可逆
- 杀掉进程再启动，active profile 与 items 与退出前一致
- Core 单测覆盖触发器优先级与手动覆盖

## M3 系统 widget（约 4–5 周）

交付可开关 widget：

- clock、notes、sysactivity、network、power、nowplaying、bing、stopwatch、countdown、alarm、focustimer、timeprogress
- CPU / 内存共用 `IPerfSampler`（`PerformanceCounter`），仅在系统状态模块上岛时启动；网络 / 电源 / 媒体同样按订阅 Start/Stop
- Now Playing：Edge 网页媒体 + 至少一种常见播放器；有会话才显示封面与控制，无会话不发全局媒体键
- Bing 壁纸：HPImageArchive（仅 bing.com URL）+ 设为桌面；模块要先同意；失败时卡片可点重试
- 系统专注是情景触发器，不是独立 widget；旧 `focus` / `cpu` / `memory` 卡片已合并或退役。`IsSupported == false` 时设置页说明原因
- 隐私开关生效：`mediaRead=false` 时不展示标题 / 封面；`networkDetails=false` 时显示「已连接（详情已关）」且停适配器枚举

性能预算（空闲，仅 Dock + 时钟）：

- 工作集 &lt; 80MB
- 系统状态未上岛时不采样 CPU；上岛时 `PerformanceCounter` 约 2s
- 运行 30 分钟工作集不单调上涨

## M4 拓扑 + 打磨（约 3–4 周）

交付：

- 显示器规则、时间规则
- AppBar 可选模式仍是目标；当前实现为 stub，设置不开放，失败保持 Overlay
- 隐私页、关于页、开机启动
- 卸载不留服务 / 计划任务；保留 `%AppData%\WinDock` 与日志；卸载删 Run 键

验收：

- “笔记本仅内屏”与“外接 2 屏”两条规则可来回切换
- 扩展坞拔掉后 Dock 出现在主屏
- AppBar 尚未作为产品开关交付

## M5 发布候选

交付：

- unpackaged x64 EXE + Inno Setup 骨架（`pack/WinDock.iss`）；SignTool 有证书再开
- 兼容矩阵：22H2 / 23H2 / 24H2
- 笔记本 + 扩展坞插拔
- `--safe` 禁用 AppBar 与全部 widget

发布检查：

- 不以管理员启动
- 无 WorkerW / 全局钩子
- 卸载后无自启残留（用户数据保留）

Shell 手工矩阵：DPI 125/150/200、插拔显示器、全屏游戏、自动隐藏热区。

## Stretch（不挡 M5）

- Open-Meteo 天气 + 手动城市
- 本地日历（非 Graph）；`AppointmentStore` 仅在可用时启用
- 文件夹徽标

## 明确延后

- WebView2 widget SDK
- 脚本插件 / Power Automate
- 实验性桌面层
- Microsoft Store / MSIX
- Windows 10
- Graph To Do、支付 widget

## 风险对应

| 风险 | 里程碑内的应对 |
|---|---|
| Explorer 更新破坏层级 | 默认 Overlay；无 WorkerW |
| AppBar 被挤出 | 可选 + 自动回退 |
| 多屏孤儿窗口 | DisplayService 防抖 + 迁主屏 |
| 媒体播放器不一致 | 会话选择 + 无封面降级 |
| 计数器权限 / CPU | 按模块订阅 `PerformanceCounter`、失败显示 “—” |
| Focus 不可用 | 门闩禁用，不读注册表 |
