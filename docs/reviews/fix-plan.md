# 可修复问题与方案

> 来源：开发者 / 使用者 / 产品经理三视角审查（当前工作区，含未提交改动）。  
> 本文只收 **合理、有代码证据、且能在现有产品边界内修掉** 的问题。其余见 [other-issues.md](other-issues.md)。  
> 编号沿用审查原文：`D-` 开发者、`U-` 使用者、`P-` 产品经理。同一根因合并为一条。

## 筛选原则

收入本文须同时满足：

1. 有代码路径，不是纯市场判断。
2. 修完用户能感知到变化，或能挡住数据丢失 / 隐私越权 / 崩溃。
3. 不改产品定位、不推翻「明确不做」清单、不需要证书或法务拍板。
4. 工作量可切成可验收的小步（小时级到一两天），不是整仓重构。

| 批次 | 目标 | 建议顺序 |
|---|---|---|
| A 止血 | 回归、隐私、数据丢失、首次启动误报 | 先做，做完即可再跑 |
| B 日常路径 | 点击切窗、托盘、模块移除、Bing 确认 | 紧接 A |
| C 正确性与性能 | 天气数据、日程上限、主题、轮询、文档 | 发布前尽量做完 |
| D 体验补丁 | 反馈、文案、发现性、小交互 | 有余力再做 |

---

## 批次 A：止血（发布前必须）

### A1. 自动隐藏永不触发（本轮回归）

- 来源：U-01（P0）
- 位置：`src/WinDock.App/DockSession.cs` `PulseZorder`（约 1218–1235 行）
- 现象：默认 `AutoHide = true`，岛却一直置顶。设置里开/关看起来一样。宿主脉冲也一直停在 100ms。
- 根因：新增 300ms 磁滞 `inHysteresis` 被算进 `keepVisible`，而 `keepVisible` 会把 `_leftHostAt` 清掉。下一个 tick 再赋值，永远攒不满 `AutoHideIdleMs`（800ms）。
- 方案：磁滞只阻止「刚离开就收回」，**不要**清零离开时刻。推荐改成：

  1. 指针在岛 / 热区 / 菜单 / 弹出层 / 唤醒期内：`_leftHostAt = null`。
  2. 指针离开：只写 `_leftHostAt ??= now`，磁滞期间不清。
  3. `idle = AutoHide && 离开时长 >= 800ms`。
  4. 若仍要 300ms 防抖，用它延长「离开后最早可隐藏」的下限（`max(800, 300)` 无意义），或直接删 `HysteresisDelayMs`——已有 800ms 空闲延迟。

- 验收：开自动隐藏，指针离开岛 > 800ms 后应收成细条；指针在岛上或弹出层打开时不收。
- 测试：给 `PulseZorder` 抽纯函数，用假时钟测「离开后 N ms 才 idle」。

### A2. 首次启动弹「设置无法读取」

- 来源：U-02、P-02（P0/P1）
- 位置：`src/WinDock.Infrastructure/JsonSettingsStore.cs` `Load()`（约 141–183 行）；`src/WinDock.App/DockSession.cs` 196–198 行
- 现象：全新安装没有 `settings.json`，仍设 `LastLoadMessage = UiText.ResetToFactory` 并弹 MessageBox。
- 方案：
  1. `Load()` 区分三种结果：首次运行（主文件和 bak 都不存在）/ 从 bak 恢复 / 损坏已隔离并出厂。
  2. 仅后两种写 `LastLoadMessage` 并弹窗。首次运行写工厂设置，不提示。
  3. 可选：首次运行用托盘气泡说明「Dock 在屏幕顶边」，不要用错误对话框。
- 验收：清空 `%AppData%\WinDock` 后启动，无错误框，岛正常出现。
- 测试：`SettingsStoreTests` 增加「空目录 Load 时 `LastLoadMessage` 为空」。

### A3. 出厂回退会删光便笺文件

- 来源：D-02（P0）
- 位置：`DockSession.cs` `SweepNotes`（约 1563、883 行）；`JsonSettingsStore.Sweep`（383–407 行）；更高 schema 只读时 `CreateFactorySettings()`（221–228 行）
- 现象：每次 `Apply` 都按「当前设置里的 live keys」扫 notes 目录，不在集合里的 `*.txt` 直接 `File.Delete`。出厂便笺会拿到新 GUID，旧文件全部命中删除。更高 schema 只读时也会走这条路。
- 方案：
  1. **Immutable / 出厂占位设置时禁止 Sweep。** `IsImmutable == true` 或本次 Load 是「损坏隔离 / 更高 schema」时跳过。
  2. 首次运行可以 Sweep（目录本就空）。
  3. 常规 Sweep 改为移入 `notes\trash\` 或延迟删除，不要当场永久删。
- 验收：把旧 `notes\{guid}.txt` 留下，再人为损坏 settings 启动，便笺文件仍在。
- 测试：构造「出厂 settings + 磁盘上已有 notes 文件」，断言 Sweep 不删。

### A4. 天气未上岛、未同意就后台联网 + 明文 IP 定位

- 来源：D-01、U-07、P-01、P-04（部分）
- 位置：`ProviderHub.cs:27`；`WeatherProvider.cs` 构造函数与 `FetchFromApiAsync`；`TryConsentForWidget` 未覆盖天气；`PreciseLocation` 无人读取
- 现象：启动 5 秒后、之后每 30 分钟请求 `http://ip-api.com/json/` 和 Open-Meteo。`--safe`、没加天气模块也一样。模块页预览能直接显示定位到的城市。
- 方案（与产品原则对齐：手动城市默认、权限可撤销）：
  1. **按订阅启停**：有天气卡片在岛上才 `Start()`；全部移除或 `--safe` 时 `Stop()` 并停定时器。对照 CPU/网络的 `SyncProviders`。
  2. **默认不定位**：`City` 为空时不请求 ip-api，卡片显示「选择城市」。自动定位必须用户点「自动定位」，并走同意 + `Privacy.PreciseLocation`。
  3. **禁止明文 HTTP**：定位若保留，只用 HTTPS 源；失败则报错，不静默继续。
  4. 添加天气时 `TryConsentForWidget` 说明会访问 Open-Meteo；隐私页增加位置开关，关掉后只保留已填城市。
  5. 设置「模块」预览不得触发联网；用占位文案。
- 验收：全新安装、不加天气：抓包无 ip-api / open-meteo。加天气且未填城市：不定位。勾选自动定位并同意后才请求。`--safe` 零外连。
- 测试：注入 `HttpMessageHandler`，断言未订阅时零请求。

### A5. 天气失败时编造数据（北京坐标 / 假 AQI）

- 来源：D-10、U-08、U-18、P-05
- 位置：`WeatherProvider.cs` 81–83、103–106、139 行附近
- 现象：地理编码或 IP 失败时 lat/lon 停在北京，城市名仍是用户输入；空气质量失败写死 `aqi = 45`。
- 方案：
  1. 定位/地理编码失败：返回错误快照（「找不到该城市」/「定位失败」），**不要**带温度。
  2. 预报失败：标离线；有磁盘缓存才显示缓存并标明离线。
  3. AQI 失败：显示「—」，不要默认 45。
  4. 单实例缓存按城市/坐标分键；不要所有卡片共用一个 `Current`。
- 验收：输入不存在的城市名，岛上是错误态而不是「某城 + 北京天气」。断网时显示离线而不是优 45。

---

## 批次 B：日常最高频路径

### B1. 点钉选会把最大化 / 贴靠窗口还原

- 来源：U-03（P1）
- 位置：`src/WinDock.Providers/Win32WindowSwitch.cs` `FocusWindow`（约 394–399 行）；`WindowNative.IsIconic` 已声明未用
- 方案：仅当 `IsIconic(target)` 为真时 `ShowWindow(SW_RESTORE)`；否则只 `SetForegroundWindow` / `SetWindowPos(HWND_TOP)`，不改显示状态。
- 验收：最大化的浏览器、Snap 左右分屏的窗口，从 Dock 切回后保持原状态。最小化窗口仍能还原。
- 注意：改完请实机确认 Win11 Snap 布局。

### B2. Explorer 重启后托盘图标消失

- 来源：D-03、U-21（P1）
- 位置：`NativeTrayIcon.cs` WndProc 只处理托盘回调；`ShellHost.cs` 用 `HWND_MESSAGE`（`ParentWindow = -3`）收 `TaskbarCreated`，消息专用窗口收不到广播
- 方案：
  1. 在 `NativeTrayIcon` 里 `RegisterWindowMessage("TaskbarCreated")`，收到后 `NIM_DELETE`（忽略失败）再 `NIM_ADD`，把 `_added` 与结果对齐。
  2. `NativeTrayIcon` 的 HwndSource 保持顶层隐藏窗（现在这样即可收广播），不要挂到 message-only。
  3. App 层现有的 `_session.TaskbarCreated → InitTray` 可保留作双保险，但 **ShellHost 若要继续转发，必须改成可见性隐藏的顶层窗**。
- 验收：杀 `explorer.exe` 再启动，托盘图标回来，气球通知仍能弹出。
- 顺手：托盘改用应用图标，不要 `IDI_APPLICATION`（若仓库还没有 `.ico`，先从现有资源提一个最小图标）。

### B3. 点 Dock 抢走前台，多窗口轮换停在第一个（待实机，代码路径清楚）

- 来源：U-10、D-07 相关
- 位置：`DockWindow` 无 `WS_EX_NOACTIVATE`；`PinSwitch` / `Win32WindowSwitch` 用 `GetForegroundWindow()` 算下一个
- 方案：
  1. 岛窗口加 `WS_EX_NOACTIVATE`（点击不激活 Dock）。
  2. 需要键盘输入的弹出层（便笺、倒计时设定）单独激活；关掉后不要把焦点留在已关闭的岛上。
  3. 轮换在 `MouseDown` 时记下按下前的前台 HWND，不要在 `MouseUp` 时再读。
  4. `Closing` 时若非显式退出则 `e.Cancel = true`，防止 Alt+F4 关掉岛后进程还在、岛回不来。
- 验收：三个 Chrome 窗口连点图标能依次切换；在记事本打字时点时钟，焦点不丢（或弹出层关掉后焦回去）。Alt+F4 在岛上不退出进程。
- 置信度：中，改完必须实机。

### B4. 移除模块：计时器变成幽灵；日程等数据直接没

- 来源：D-08、U-05、U-14、P-07
- 位置：`PruneUnusedWidgets`（1943–2034 行）只从字典拿掉视图；闹钟/倒计时 `Unloaded` 在运行中不停 timer；`OnWidgetRemove` 仅便笺走 Hide
- 方案：
  1. 模块实现 `IDisposable`（或 `StopAndDetach()`）。Prune 时先停 DispatcherTimer、退订 Provider、再移出字典。
  2. `IKeepAliveWhileRunning` **只**在「仍配置在某个情景里、只是当前岛没显示」时保活；用户点了「从 Dock 移除」必须停。
  3. 带用户数据的模块（日程、倒计时预设、闹钟、天气城市）移除前确认，或像便笺一样进 Hidden 可恢复。至少日程必须确认。
  4. 天气/日程构造时订的全局 `Changed` 必须在 Unloaded/Dispose 成对退订。
- 验收：移除正在响的闹钟后不再响；移除日程后数据可恢复或明确确认后才删；反复添加/移除天气，定时器数量不涨。

### B5. Bing「设为桌面」绕过确认，也无法恢复

- 来源：U-09、P-06
- 位置：岛按钮直接 `_bing.TryApplyCurrentAsync()`；`DockSession.ApplyBingWallpaperAsync`（带确认）无调用方；不记录旧壁纸
- 方案：
  1. 岛上「设为桌面」改走 `ApplyBingWallpaperAsync`（或同等确认）。
  2. 应用前读当前壁纸路径与 `WallpaperStyle` / `TileWallpaper`，写入 `%LocalAppData%\WinDock\wallpaper-backup.json`；弹出层提供「恢复上一张」。
  3. 缓存目录只保留最近 8 张 + 当前已应用，避免 UHD 图无限堆。
- 验收：点应用会确认；取消不改桌面；应用后能恢复到操作前的壁纸。

---

## 批次 C：正确性、性能、文档

### C1. 日程：满 100 条静默丢新项；过期看不见也删不掉

- 来源：D-06、U-06、P-07、D-30
- 位置：`Schedule.cs` `Normalize` 保留**前** 100 条；`Add` 追加在末尾；列表只显示 `Date == today`；`TimeSpan.TryParse` 把「9」当成 9 天
- 方案：
  1. 上限改为丢**最旧**的过期项，或拒绝新增并提示「已满 100 条」。禁止清空输入却不写入。
  2. 自动归档/删除 `Date < today` 的项（或提供「过期」分组可删）。
  3. 时间解析只用 `H:mm` / `HH:mm`；纯数字当小时（0–23）或直接拒绝。
  4. 弹出层标题按 `user-text-overflow` 规则换行，不要省略号截断。
- 测试：第 101 条要么留下并挤掉最旧，要么报错且输入框不清空；`"9 开会"` 不得变成 00:00。

### C2. `ApplyTree` 视觉树 + 逻辑树双递归

- 来源：D-04（P1）
- 位置：`WidgetViews.cs` `ApplyTree` 214–251 行，`SetForeground` 同类
- 方案：只走视觉树；`Popup` 单独 `Child`；用 `HashSet<DependencyObject>` 去重。画刷用冻结静态实例。主题/语言没变则跳过。
- 验收：改一次设置，Profiler 里 `ApplyTree` 不再随嵌套指数上涨；模块多时提交设置不卡 UI。

### C3. Now Playing 每秒多次全量进程快照

- 来源：D-05、U-34 部分
- 位置：`NetworkAndMediaProviders.cs` Tick 无条件 `MediaTransportFallback.TrySnapshot`；`KnownLocalPlayers` 对 9 个进程名各 `GetProcessesByName`
- 方案：
  1. GSMTC 已有 Playing 会话时跳过本地回退。
  2. 每个 tick 最多一次 `Process.GetProcesses()`，内存里按名匹配。
  3. 岛自动隐藏或无会话时降到 5–10 秒。
- 验收：只放时钟时（无 Now Playing）零次媒体进程枚举；有 Now Playing 且 Spotify 在播时，每秒最多 1 次快照。

### C4. 全屏避让不看 Dock 所在屏

- 来源：D-14、U-20、P-19
- 位置：`UserNotificationState.IsForegroundCoveringMonitor` 用前台窗口自己的监视器；QUNS 的 D3D/演示是全局的
- 方案：启发式必须比较 **前台窗口所在屏 vs Dock 所在屏**（传入 `selfHwnd` 的监视器）。仅当同一块屏被盖住才 `Yield`。QUNS 的 D3D/Presentation 仍可全局隐藏（系统本意），但启发式不得跨屏误伤。
- 验收：副屏全屏视频时，主屏 Dock 仍在；Dock 所在屏 F11 全屏时才避让。

### C5. 拖岛换屏没有鼠标捕获

- 来源：D-13、U-20
- 位置：`DockWindow.cs` 岛拖动未 `CaptureMouse`
- 方案：按下捕获鼠标，移动时预览，松开时按光标所在屏写 `dock.monitor`。
- 验收：从主屏拖到副屏松开，设置里的显示器跟着变，岛出现在目标屏。

### C6. 「跟随系统」主题不跟踪系统变化

- 来源：D-09、U-24
- 方案：订阅 `SystemEvents.UserPreferenceChanged`（及 `WM_SETTINGCHANGE`）。主题为 Follow 时重跑 `ApplyTheme`。时区变化时 `TimeZoneInfo.ClearCachedData()` 并刷新时钟/闹钟。
- 验收：Windows 切深色后岛在数秒内跟上，不必改 WinDock 设置。

### C7. 天气/日程「自定义」点了没反应；华氏度无入口

- 来源：U-17、P-16
- 位置：`BuiltInWidgets.CanCustomize` 含 Weather、Schedule；两者未实现 `IWidgetCustomizable`
- 方案：二选一——实现自定义（城市、单位、刷新）或从 `CanCustomize` 去掉。`UseFahrenheit` 有字段就给开关，否则删字段。调用已有的 `ConditionLocalized`，接口 `language` 跟随界面语言。
- 验收：右键没有空菜单项；英文界面天气现象是英文。

### C8. 启动钉选未设工作目录；快捷方式图标当图片解码

- 来源：D-16、D-17
- 方案：`ProcessStartInfo.WorkingDirectory` 设为目标所在目录（`.lnk` 用快捷方式工作目录）。`IconPath` 仅当 `.ico/.png` 才当位图；否则按「路径 + 索引」抽系统图标。
- 验收：拖入依赖相对路径的 exe 能启动；开始菜单里图标设在 exe 上的项不再变通用字形。

### C9. 文档与实现脱节（最小对账）

- 来源：P-13
- 必改：`docs/PRODUCT.md` 模块表补天气/日程/闹钟；隐私段补天气；删掉「AppBar 只有 Stub」或改成「实现已接入但设置不开放」。`ARCHITECTURE.md` 同步安全模式（停天气）、全屏启发式、宿主脉冲间隔、App 层托盘 P/Invoke 例外。`DATA-MODEL.md` 补 `weather` / `schedule` 字段与 `weather-cache.json` 路径。
- 验收：按文档做验收不会把已交付模块判为非法。

### C10. 钉选运行检测可接受，但锁屏/无钉选时应停

- 来源：D-26、P-18 部分
- 方案：岛上零个 app/file 钉选时不要 `GetProcesses()`。锁屏（`SessionSwitch`）暂停 3 秒采样与 200ms 计时刷新。
- 验收：只有时钟+便笺时，进程快照定时器不跑。

---

## 批次 D：体验补丁（合理、小、可修）

| ID | 来源 | 改法 |
|---|---|---|
| D1 启动失败无反馈 | U-11 | `TryLaunch` 失败弹气泡：「无法启动，文件可能已移动」+ 右键仍可移除 |
| D2 缺失目标仍显示缓存图标 | U-11 | 目标不存在时一律用缺失态（透明+错误字形），忽略缓存 |
| D3 连点双开 | U-12 | 启动后 2 秒内吞重复点击，钉上显示「启动中」 |
| D4 二次启动只报已运行 | U-21、P-10 | 已运行实例用已有单实例管道/事件唤出设置或把岛 Peek 出来（没有管道就先用命名 EventWaitHandle） |
| D5 模块目录大卡点不了 | U-23 | 整卡点击 = 添加；`AllowsMultiple == false` 已在岛上则提示，不要再加第三个时钟 |
| D6 添加程序入口难找 | U-04 部分 | 模块右键和设置里加「添加程序…」；空岛引导保留 |
| D7 专注计时文案 | U-16 | 休息结束：「点开始进入下一轮专注」；运行中点预设先确认或禁用 |
| D8 热键冲突误报 | U-32 | `ProfilesEnabled == false` 时 D/Z 热键不参与占用检测；注册失败打开设置时提示一次 |
| D9 菜单标题吞下划线 | U-27 | `RecognizesAccessKey` 关掉或把 `_` 换成 `__` |
| D10 文件夹也显示「左键激活已有窗口」 | U-27 | 提示按钉类型：文件夹/文件写「打开」 |
| D11 设置页快捷键框吞 Tab | U-25 最小集 | 录制框放行 Tab/Esc；Backspace 提示「已清除唤醒键」 |
| D12 开机启动藏在关于 | U-33、P-15 部分 | 开关挪到布局或新建「通用」页，关于页留只读状态 |
| D13 隐藏便笺难找 | U-22 | 隐藏时气泡说明去处；设置模块页或岛菜单加「已隐藏的便笺」 |
| D14 网络同意 vs 天气 | P-23 | 网络改成页内说明；模态同意留给外发（天气）和读其他应用内容（正在播放） |
| D15 日志不轮转 | D-22 部分 | 只留 14 天；Bing 缓存见 B5 |
| D16 `--safe` 真正停采样 | P-13.5 | `ProviderHub` 在 SafeMode 不构造天气定时器，已有的 CPU/网络路径对齐 |
| D17 未知 widget 丢弃 | D-23 | 升 `schemaVersion`；未知卡片原样保留不回写丢掉 |
| D18 日程岛上铺用户长标题 | U-30 | 岛上只留下一项短状态，全文放弹出层换行 |

---

## 建议实施顺序（一条工作流）

```
A1 自动隐藏 → A2 首启误报 → A3 Sweep 便笺
    → A4+A5 天气隐私与错误态
    → B1 切窗还原 → B2 托盘重建 → B4 模块生命周期
    → B5 Bing 确认 → B3 NOACTIVATE（实机）
    → C1 日程 → C2 ApplyTree → C3 媒体进程
    → C4 全屏按屏 → C6 主题 → C9 文档
    → 其余 C/D 穿插
```

每批结束后：跑 `dotnet test`；按 `.cursor/skills/restart-windock/SKILL.md` 重启；实机过一遍自动隐藏、首次启动（临时改名 settings 目录）、天气断网、点最大化窗口。

---

## 不在本文、但修 A/B 时不要踩的坑

- 不要为了「磁滞」再加一套和 800ms 空闲重复的计时器。
- 不要在出厂设置里写死便笺 GUID 然后继续无条件 Sweep。
- 不要把天气改成「启动就定位但改用 HTTPS」——默认仍应是手动城市。
- 不要在修轮换时给岛 `ShowActivated = true`，那会加重抢焦点。
