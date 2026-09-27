# WinDock 数据模型

本文是配置与领域对象的唯一说明。C# 类型放在 `WinDock.Core`，JSON 由 `WinDock.Infrastructure` 读写。改字段必须同时改 `schemaVersion` 迁移。

## 路径

| 用途 | 路径 |
|---|---|
| 设置 | `%AppData%\WinDock\settings.json` |
| 上一份成功设置 | `%AppData%\WinDock\settings.bak` |
| 便笺 | `%AppData%\WinDock\notes\{id}.txt`；首次打开时若仍有旧版 `notes.txt` 则迁入第一份 |
| 日志 | `%LocalAppData%\WinDock\logs\` |
| 图标缓存 | `%LocalAppData%\WinDock\icon-cache\` |
| Bing 壁纸缓存 | `%LocalAppData%\WinDock\bing-wallpaper\` |
| 媒体封面 | `%LocalAppData%\WinDock\media-thumbs\` |

## settings.json

当前 `schemaVersion`：**1**。

```json
{
  "schemaVersion": 1,
  "ui": {
    "theme": "system",
    "language": "system",
    "backdrop": "mica",
    "islandTransparency": 50,
    "dockMode": "overlay"
  },
  "privacy": {
    "mediaRead": false,
    "networkDetails": false,
    "processNames": false,
    "crashReports": false,
    "preciseLocation": false,
    "bingDownload": false
  },
  "hotkeys": {
    "nextProfile": "Win+Shift+D",
    "previousProfile": null,
    "undoProfile": "Win+Shift+Z",
    "revealDock": "Win+Shift+A"
  },
  "manualOverride": {
    "until": "nextFocusOrTopology"
  },
  "activeProfileId": "general",
  "lastProfileId": null,
  "switchHistory": [],
  "profiles": []
}
```

### `ui.theme`

`system` | `light` | `dark`

### `ui.language`

`system` | `chinese` | `english`。默认 `system`。跟随系统时，Windows UI 语言以 `zh` 开头用中文，否则英文。缺省字段按 `system` 读入，不升 `schemaVersion`。界面文案走 `UiText` / `UiLocale.T`；设置、托盘、Dock 菜单和模块在切换后即时刷新，不重建正在走的计时器。

### `ui.backdrop`

`mica` 更实、偏壁纸底色；`frosted` 雾面；`glass` 最透。选择材质时会把 `ui.islandTransparency` 设为云母 `50`、磨砂 `0`、玻璃 `80`，之后仍可手动改滑块。云母会压低通透、玻璃用满。

### `ui.islandTransparency`

`0`–`100`，默认 `50`。越大岛面底板越通透，只影响 Dock 底，不改变模块药丸。

### `ui.dockMode`

`overlay` | `appbar`

`appbar` 失败时运行时回退 overlay，但磁盘上仍保留用户选择，以便下次启动重试。`--safe` 忽略该字段，强制 overlay。

### `privacy`

全部默认 `false`。含义：

- `mediaRead`：允许 Now Playing 显示标题 / 封面 / 播放器名。关闭时仍可尝试播放控制（若会话存在），但不展示元数据。
- `networkDetails`：允许显示适配器名、速率。关闭则 Network widget 显示「已连接（详情已关）」，并停止枚举适配器。
- `processNames`：预留。MVP 的 CPU widget 只显示总量，不列出进程。此开关为将来进程列表准备，默认关。
- `crashReports`：预留，默认关。MVP 不上传。
- `preciseLocation`：天气 stretch 用。默认关，只用手动城市。
- `bingDownload`：允许访问 bing.com 下载壁纸并设为桌面。模块首次使用会征求同意。

`IPermissionGate` 在订阅对应 provider 之前检查这些开关。widget 不得自行读取设置文件绕过门闩。`networkDetails=false` 时网络采样只查是否联通，不枚举适配器。`mediaRead=false` 时不抓标题 / 封面。

### `manualOverride.until`

当前产品只暴露 `untilManual` 与 `session`。`nextFocusOrTopology` 仍可出现在 JSON 里，但自动触发关闭时引擎把它当成 `untilManual`。

### `lastProfileId` / `switchHistory`

- `lastProfileId`：上一次成功切换的来源情景，供撤销（热键 / 托盘 / 设置）。
- `switchHistory`：最近 100 条 `ProfileSwitchRecord`（时间、from、to、原因、结果）。

## Profile

```json
{
  "id": "deep-work",
  "name": "深度工作",
  "triggers": [
    { "type": "focus", "active": true },
    { "type": "displayTopology", "monitorCount": 1 }
  ],
  "dock": {
    "edge": "top",
    "monitor": "primary",
    "autoHide": true,
    "peekOnPointer": true,
    "revealHotkey": false,
    "islandLength": null,
    "items": [
      { "type": "app", "path": "C:\\Windows\\System32\\notepad.exe" },
      { "type": "widgetChip", "widgetId": "clock" }
    ]
  },
  "widgets": [
    { "id": "notes", "monitor": "primary", "x": 40, "y": 80, "visible": true }
  ],
  "actions": [],
  "permissions": ["media.read"]
}
```

### 触发器

同一 profile 内多个触发器为 **OR**（任一匹配即该 profile 成为候选）。跨 profile 用全局优先级消解。

| `type` | 字段 | 匹配 |
|---|---|---|
| `manual` | 无 | 仅由热键 / 托盘 / 设置显式选中 |
| `focus` | `active`: bool | `ContextSnapshot.FocusActive` 相等；Focus 不受支持时该触发器永不匹配 |
| `displayTopology` | `monitorCount`?、`primaryDevicePath`?、`requireDevicePaths`? | 见下 |
| `schedule` | `days`、`start`、`end` | 本地时区的日 / 周窗口；`end` 可跨午夜 |

`displayTopology`：

- `monitorCount`：当前显示器数量相等。
- `primaryDevicePath`：主屏身份相等。
- `requireDevicePaths`：这些身份必须全部在线。
- 字段均可选；全空则视为不匹配（避免误伤）。

### Dock 项目

| `type` | 必填 | 说明 |
|---|---|---|
| `app` | `path` | 左键切到已开窗口，没有则启动；右键「打开」新开 |
| `file` | `path` | 用 Shell 打开 |
| `folder` | `path` | 打开文件夹；徽标 P1 |
| `widgetChip` | `widgetId` | 内置模块卡片，直接显示在 Dock 上 |

旧配置里的 `url`、`separator` 加载时丢弃；产品暂不提供网址钉和分隔符。

可选：`id`、`title`、`iconPath`、`shortcutPath`（原 `.lnk`，启动时优先 ShellExecute 该快捷方式以保留参数；圆点仍看 `path` 解析出的 exe）、`density`（`expanded` 默认 / `compact`）、`paperColor`（便笺纸色：`yellow` 默认 / `orange` / `pink` / `magenta` / `blue` / `green`）、`timeZoneId`（时钟第二时区，Windows 或 IANA id；缺省仅本地）、`focusTimer`（专注计时器：颜色、专注/短休息/长休息分钟、每几段长休息、结束通知）、`countdown`（倒计时：上次时/分/秒、名称、最多 12 条命名预设）、`alarm`（闹钟：时/分、标签、是否已设定、`ringsAt` UTC 响铃时刻）、`power`（电源：`layout` 为 `status` / `percentageRing` / `iconOnly`，`hiddenDeviceIds` 为岛上隐藏的设备）、`timeProgress`（时间进度：`period` 为 `year` / `month` / `day`，`layout` 为 `bars` / `percentage` / `ring`）、`sysActivity`（系统状态：`layout` 为 `numbers` / `rings` / `bars`）、`network`（网络：`show` 为 `both` / `download` / `upload`，`chartInDock` / `chartInPopup`）。`density` 只对有两种内容模式的模块生效（时钟紧凑为短日期、展开带表盘与星期；正在播放紧凑为播放键、展开为封面与进度；时间进度紧凑为百分比在上、展开为周期与百分比左右排列；网络紧凑为速率、展开带折线；Bing 壁纸紧凑为圆角方形缩略图加前后/应用按钮，展开带较窄标题与日期），不是改卡片高度。`paperColor` 只对便笺生效。`timeZoneId` 只对时钟生效；未知 id 忽略。`focusTimer` 只对专注计时器生效。`countdown` 只对倒计时生效。`alarm` 只对闹钟生效。`power` 只对电源模块生效。`timeProgress` 只对时间进度生效。`sysActivity` 只对系统状态生效。`network` 只对网络模块生效。`id` 缺省时由 store 生成稳定 GUID；同一 `widgetId` 可出现多份，用 `id` 区分。右键：有点击动作才出现「打开模块」；有自定义才出现「自定义」；有双模式才出现「紧凑 / 展开」；均可从 Dock 移除。设置目录点预览或 + 始终追加一份，不切换成移除。文件夹徽标显示标题而不是条目数。

`dock.islandLength`：可选，岛沿长边的逻辑像素长度。缺省（`null`）按内容自适应；用户拖动两端后写入。双击边缘恢复自适应。长度小于内容时，岛内滚动，右侧（左右条则底部）出现箭头平移模块。

### Widget 布局

`id` 必须是内置 ID 之一：`clock` `notes` `sysactivity` `network` `power` `nowplaying` `bing` `stopwatch` `countdown` `alarm` `focustimer` `timeprogress`。旧配置里的 `battery` 在加载时改为 `power`（若该 profile 还没有电源）或删除。旧的 `cpu` / `memory` 卡片合并为一份 `sysactivity`。

`visible` 表示该类型至少有一份在当前 profile 的 Dock 上（与 `dock.items` 里对应的 `widgetChip` 同步）。同一 `widgetId` 可以有多份卡片。`monitor` / `x` / `y` 保留兼容，模块本身不再使用独立窗口坐标。

### 权限清单（profile 级）

目前仅：

- `media.read`

Profile 声明的权限是**上限**，还要与全局 `privacy.*` 做 AND。全局关闭则即使用户在 profile 里打开也不能读元数据。

### 动作

```json
{
  "on": "activate",
  "kind": "notify",
  "message": "Deep Work on"
}
```

| `kind` | MVP |
|---|---|
| `notify` | 是 |
| `launch` | 是，但首次确认；`target` 为已存在的 app/file/url |
| `quitApp` | 否 |
| `moveWindows` | 否 |
| `script` | 否 |

`on`：`activate` | `deactivate`

`launch` 失败写入历史 `result=failed`，不回滚已经完成的 Dock 切换。

## 运行时对象（不落盘）

### `ContextSnapshot`

ProfileEngine 的唯一输入。由 App 在 UI 线程根据 ProviderHub + DisplayService + 时钟组装。

- `UtcNow` / 本地时间
- `FocusSupported` / `FocusActive`
- `Monitors`：id、devicePath、isPrimary、bounds、workArea、dpi
- `ManualOverrideProfileId` / `ManualOverrideUntil`
- `SafeMode`
- `ProfilesEnabled` / `AutoProfileTriggersEnabled`

### `ProfileDiff`

Engine 输出，供 Shell / Widgets 应用：

- `ProfileId`
- `TriggerReason`
- `Dock`（edge / monitor / autoHide / peekOnPointer / revealHotkey / items）
- `Widgets`（要显示的布局）
- `Theme`
- `ActionsToRun`
- `Permissions`

应用 diff 时：先改 Dock 与 widget，再跑动作。动作抛错只记日志。

### `ProfileSwitchRecord`

历史条目：时间、from、to、reason、result、errorMessage。保留最近 100 条，供撤销与设置页。

## 默认 profile

全新安装写入两条：

1. `general`（显示名「日常」）：主屏顶边 Overlay 悬浮岛；新安装默认钉时钟与便笺，并 `autoHide=true`。电源 / 网络 / 正在播放 / Bing / 系统状态在模块目录里。无自动触发器（作为兜底）。**已有用户配置不会被改回出厂布局。** 显示名可改，id 仍是 `general`，不能删除。
2. `deep-work`（显示名「深度工作」）：JSON 里仍带 Focus 触发器，但产品自动触发关闭时不会自己切过去。显示 `focustimer` 与 `notes`；出厂 `autoHide=true`。可以删除。

用户可在设置「高级 → 情景」里复制 / 重命名 / 删除（仅 `ProfilesEnabled` 打开时）。复制后的情景只能手动选中。

`general` 必须始终存在。删除请求应被拒绝。Engine 在没有任何候选时回退 `general`。

`timeZoneId` 是 v1 附加字段，未知值忽略，不升 schema。`dock.peekOnPointer`（缺省 `true`）、`dock.revealHotkey`（缺省 `false`）、`hotkeys.revealDock`（缺省 `Win+Shift+A`）同样是 v1 附加字段，不升 schema。

## 迁移

`ISettingsStore.Load`：

1. 读 `settings.json`
2. 损坏则读 `settings.bak`
3. 仍失败则写入出厂默认并备份损坏文件为 `settings.corrupt.<timestamp>`
4. `schemaVersion < 当前` 则按序执行迁移
5. `schemaVersion > 当前` 则**不覆盖**该文件，内存使用出厂默认并提示
6. 未知 widget、非法 dock 项、重复 `general` 会被修复/丢弃，而不是抛错让进程起不来
7. 缺 `id` 的 dock 项会生成 GUID 并写回磁盘

v1 无历史迁移。下一个破坏性变更从 v2 开始写 `MigrateV1ToV2`。
