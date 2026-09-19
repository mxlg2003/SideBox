# Built-in widgets

岛上全部是 `WidgetChrome.Wrap` 药丸。能力以 `BuiltInWidgets` 为准。目录分组见 `WidgetCatalog`。

图例：开 = 可点开 flyout；自 = 可自定义；密 = 有紧凑/展开。

## 时钟 `clock`

开 · 自 · 密。[`ClockWidget`](../../../src/WinDock.Widgets/WidgetViews.cs)

- 展开：26px 模拟表盘 + 时间 17 SemiBold + 日期 + 秒 11。
- 紧凑：隐藏表盘，时间/日期居中，无秒。
- 弹出：84px 表盘、时间 38、日期 14、时区/秒 12。可开第二时区。
- 自定义：第二时区。无纸色（`ApplyPaper` 空）。

## 便笺 `notes`

开 · 自。无密度。[`NotesWidget`](../../../src/WinDock.Widgets/NotesWidget.cs)

- 岛上：纸色药丸（`NotePapers`，不是 `ResolveTileFill`），`Tag = wd-chrome-notes`。预览宽 132、最多两行、Candara Bold 11、墨水 `#1A1A1A`。
- 弹出：最小 300×220，同纸色，编辑字号 16，自动保存 400ms。历史列表最高 148。
- 自定义：六色纸。墨水与预览 `Tag = ink`，主题扫描不得改色。

## 系统状态 `sysactivity`

开 · 自。[`SysActivityWidget`](../../../src/WinDock.Widgets/SysActivityWidget.cs)

- 岛上三种布局同宽（按圆环标签 + 双环 34 + 间距 8 收，数字 `100%` 若更宽则取更大值）。布局：`numbers` 双百分比+色点；`rings` 双环 34；`bars` 条形。CPU `DockTokens.Cpu`，内存 `Memory`。
- 弹出：双环 88 + 已用内存。
- 内部按钮强调仍是 `0xFF0078D4`（漂移）。

## 网络 `network`

开 · 自 · 密。[`NetworkWidget`](../../../src/WinDock.Widgets/NetworkWidget.cs)

- 展开：↓上传入 `Network` / ↑ `NetworkUp` 速率；可选 72×28 火花线。
- 紧凑：只留速率，无岛上图。
- 弹出：双速率 + 可选 120 高折线（60s）。可显下载/上传/全部。
- 未连接显示「未连接」。

## 电源 `power`

开 · 自。[`PowerWidget`](../../../src/WinDock.Widgets/PowerWidget.cs)

- 岛上横排设备。布局：`status`、`percentageRing`（环 34）、`iconOnly`（环 40）。电量色 `BatteryOk/Low/Critical`。
- 药丸 padding 特例 `8,4`（仍高 50）。
- 弹出：设备列表；自定义显示项。无设备时空电池图标 +「没有设备」。

## 正在播放 `nowplaying`

密。无独立弹出。[`NowPlayingWidget`](../../../src/WinDock.Widgets/WidgetViews.cs)

- 展开：40px 封面 + 标题 12（最大宽 110）+ 艺人 10 + 进度 3px + 上/播/下。
- 紧凑：32px 播放圆钮（`QuietFill`），隐藏详情。
- 无会话/无权限时降级文案。隐私关则不读媒体。封面 `ArtCornerRadius` 4。

## Bing 壁纸 `bing`

密。无弹出。[`BingWallpaperWidget`](../../../src/WinDock.Widgets/WidgetViews.cs)

- 38×38 缩略，**圆角 8**（特例）。字形色 `DockTokens.Bing`。
- 展开：标题+日期宽 72 + 前一天 / 设为桌面 / 后一天。
- 紧凑：只留缩略和控制。点空白且无图时刷新。

## 秒表 `stopwatch`

开。每份独立。[`StopwatchWidget`](../../../src/WinDock.Widgets/StopwatchWidget.cs)

- 岛上：时间 17 + 状态行，最小宽 72。
- 弹出：时间 38 + 开始/暂停（Win11 蓝填充钮）+ 复位。运行中 200ms 刷新。

## 倒计时 `countdown`

开。每份独立。[`CountdownWidget`](../../../src/WinDock.Widgets/CountdownWidget.cs)

- 岛上：计时器字形 + 时间 17 + 名称（暂停为「已暂停」）。
- 弹出：命名预设、名称、时/分/秒（右侧重叠箭头、滚轮、方向键），开始/暂停（Win11 蓝填充钮）；暂停时复位。开始且有名则 upsert 预设。到零声音+通知。运行中 200ms 刷新。

## 专注计时器 `focustimer`

开 · 自。每份独立。[`FocusTimerWidget`](../../../src/WinDock.Widgets/FocusTimerWidget.cs)

- 岛上：26px 环（色板）+ 时间 16 + 阶段文案。
- 弹出：时间 38、开始/暂停、复位、预设 15/25/45/60、八色、专注/休息分钟、结束通知。
- 不是 Windows 系统专注，不会切情景。

## 时间进度 `timeprogress`

开 · 自 · 密。[`TimeProgressWidget`](../../../src/WinDock.Widgets/TimeProgressWidget.cs)

- 周期：年 / 月 / 日。布局：`bars` / `percentage` / `ring`（岛上环 18）。
- 展开宽 124；紧凑收掉固定宽。弹出环 88、标题约 22。
- 条/环填充浅色近黑 `0xFF2A2A32`、深色白（未进 Light/Dark 表）。

## 占位

未知 `WidgetId` → `PlaceholderWidget`：药丸 + 名称，opacity 0.55，不可点。

## 目录预览

设置「模块」页预览是静态缩略（高 96），不是岛上真控件。时钟/进度会每秒跳。预览强调色可用 `Settings.Accent`；Bing 占位蓝 `0xFF93C5FD`。
