# UI surfaces

按层列当前界面。实现细节以源文件为准。

## 边缘岛

文件：[`DockWindow.cs`](../../../src/WinDock.Shell/DockWindow.cs)、[`DockLayout.cs`](../../../src/WinDock.Core/Layout/DockLayout.cs)、[`DwmBackdrop.cs`](../../../src/WinDock.Shell/DwmBackdrop.cs)

无边框、透明 HWND、不进任务栏、Per-Monitor v2。默认 Overlay 置顶，贴所选边缘（上 / 左 / 右）居中。岛面圆角 8、描边 1、padding 5。通透靠分层窗口 + 岛面 ARGB，不铺窗口级亚克力。

内容：水平（顶/底）或竖直（左/右）`StackPanel`。超出长度时右侧（或对应末端）出现左右箭头，一次滚 140。两端 8px 可拖长度；双击边缘恢复按内容自适应。把岛拖到另一块屏会写回 `dock.monitor`。

状态：置顶 → 空闲自动隐藏（贴边 4px 细条）→ 热区 Peek（可关）或全局唤醒快捷键（可关，和弦可在布局页录制，默认 `Win+Shift+A`）→ 滑入。关掉 Peek 后停在细条上也不会滑出；点细条或唤醒键才会出来。全屏/演示避让时 Hide。细条仍要能点到，热区对齐岛宽并含从工作区边到岛的走廊。设置窗打开时 `KeepVisible`，岛保持可见。唤醒键把岛置顶约 2s，方便把指针移上岛，之后仍走 800ms 空闲隐藏。

空岛提示：「右键添加程序，或从资源管理器拖入。模块请打开设置 → 模块」。启动项在岛上添加，不把加程序指去模块页。

## 钉

应用 / 文件 / 文件夹。透明槽，高 50，图标 40（文件夹可略宽）。无图标时用 MDL2 字形 26。文件夹在图标上叠 9px 名称徽标（Tahoma + Noto Sans SC）。底部 4px 运行点，有窗口时显示。

悬停：圆角 4 的钉名弹出层，最大宽 220，过长换行；菜单实色底 + 描边 + 轻阴影。缺失路径 opacity 0.45。

左键：已有窗口则切换（多窗口轮换），否则启动。右键可「打开（新开一份）」、移除。

布局页「程序折叠」「文件夹折叠」默认关。打开后，岛上只留一个聚合槽（少于 2 个不收），图标用系统通用程序 / 文件夹图标，和普通钉一样 40px。悬停「程序 · N」/「文件夹 · N」。任一成员在跑则亮运行点。拖聚合槽会整组移动；从资源管理器拖入仍落到可见缝对应的真实列表位置。组内顺序要先关掉对应折叠。

## 模块药丸

[`WidgetChrome.Wrap`](../../../src/WinDock.Widgets/WidgetViews.cs)：圆角 4、高锁 50、padding 10×6、手型光标。底色 `ResolveTileFill`。无标题行。同一模块可钉多份。

右键：有动作才显示「打开模块」；有紧凑/展开才显示这两项；均可从 Dock 移除。密度见 [widgets.md](widgets.md)。

## 弹出层

[`WidgetFlyout`](../../../src/WinDock.Widgets/WidgetFlyout.cs) + `WidgetChrome.Flyout`。`AllowsTransparency`，距岛 8px（下/上/左/右随 `DockEdge`）。卡片圆角 8、实色 `ResolveMenuFill`、内边距 16/12/16/20、最小宽约 280（便笺 300）。外层 DropShadow。打开时部分模块 140ms 淡入+缩放。自定义里的 `ComboBox` 走 `WidgetChrome.PaintCombo`：闭合井 `ResolveFieldFill`，展开层圆角 8、`ResolveMenuFill`，外形同设置 ComboBox，不走系统默认下拉。弹出层内的滚动列表（如城市联想下拉、多项列表、便笺等）统一调用 `WidgetChrome.PaintScrollViewer`，使用 12px 槽、6px 宽圆角胶囊滑块、无箭头按钮，滑块颜色跟随当前主题 `DockTokens.Settings.ScrollThumb*`，样式与设置-模块（`Settings.ScrollBarVertical`）完全一致。

弹出层开着时视为指针仍在 Dock（避免自动隐藏）。同一时间只开一层：打开新的模块弹出层或折叠钉展开层会立刻关掉已经打开的那一层。不要用独立 `Window` 做模块详情。

程序/文件夹折叠的展开层是 [`PinStackFlyout`](../../../src/WinDock.Widgets/PinStackFlyout.cs)：同样 `WidgetFlyout.Attach` + `WidgetChrome.Flyout`，贴岛 8px。顶/底横排、左/右竖排，间距 `TileGap`，内边距 `TilePaddingX/Y`。成员钉外观与岛上一致（40px 图标、文件夹徽标、运行点）；点成员仍切换/启动，右键仍是打开一份/移除。再点聚合槽、点层外、或组成员不足 2 个时收起。

## Dock 菜单

[`DockMenu.cs`](../../../src/WinDock.Shell/DockMenu.cs)。圆角 8、padding 6、字号 13、项圆角 4 padding 10×7、勾选 MDL2 `E73E`。危险项 `DockTokens.Danger`。岛空白处：添加程序/快捷方式/文件夹、设置、退出。槽上：打开、密度、自定义、移除。

## 溢流

内容超出才显示。32×32、圆角 4、空闲 opacity 0.62、悬停 1、禁用 0.28。字形 `E76B` / `E76C`。

## 设置窗

[`SettingsWindow.xaml`](../../../src/WinDock.App/Settings/SettingsWindow.xaml) 1000×680，单例、关闭即隐藏。窗口铺 `Settings.Page`。左栏 `NavWidth` 168、内边距 `NavPad` 12（「设置」+ 分组导航），右栏页标题 16 SemiBold + 一句说明 + 内容。控件模板来自 [`SettingsTheme.xaml`](../../../src/WinDock.App/Settings/SettingsTheme.xaml)。滚动条是隐式 `ScrollBar`：12px 槽、6px 圆角滑块、无上下箭头，浅/深色用 `Settings.ScrollThumb*`。`SettingsChrome.Publish` 按主题写入 token。模块目录只在「模块」页刷新。

没有全局情景顶栏。`ProfilesEnabled` 默认开，左栏「情景」在最底分组 **高级**。撤销/复制/重命名/删除/历史只在该页。自动触发仍关。

表单页边距 `PagePad` 24、区块间距 `SectionGap` 24、标签到控件 `FieldGap` 8、内容最大宽 560。布局的边缘+显示器、外观的主题+材质两列并排。

| 左栏 | 分组 | 内容 |
|---|---|---|
| 模块（默认） | Dock | `WidgetPicker`；页说明「把时钟、便笺等加到边缘岛上。」 |
| 布局 | Dock | 边缘、显示器、自动隐藏、热区 Peek、可录制的全局唤醒快捷键、程序折叠、文件夹折叠 |
| 外观 | 显示 | 主题、材质、语言、岛面透明度滑块、岛面预览条 |
| 隐私 | 系统 | 媒体 / 网络 / Bing 开关 |
| 关于 | 系统 | 版本、配置/日志路径、开机启动开关 |
| 情景 | 高级 | 仅 `ProfilesEnabled`；当前情景下拉、撤销、复制/重命名/删除、热键、最近切换 |

说明文字用 `Settings.Muted`。按钮：次要 32 高圆角 4，确定/添加 primary，删除 destructive。键盘焦点用 `Settings.FocusRing`，不用系统虚线。导航项：MDL2 图标 + 名称，选中 `NavSelected` 圆角 4。

## 模块目录

[`WidgetPicker`](../../../src/WinDock.App/Settings/WidgetPicker.xaml)。作为设置右栏内容，不再自带大标题。左栏 `CatalogNavWidth` 168、内边距 `NavPad` 12：搜索、分类（全部 / 时钟 / 便笺 / 媒体 / 系统 / 壁纸）。选中项圆角 4 + `NavSelected`，高度与设置导航项同为 32。右栏 padding `PagePad` 24，分类标题 16 SemiBold。预览卡固定宽 `CatalogCardWidth` 160，`WrapPanel` 排布（默认窗口约 3 列），间距 `CatalogCardGap` 16；预览井高 96、内边距 12、圆角 8，`ClipToBounds`，文字过长省略。电源示例最多两个设备。预览只展示，不添加；`+` 把模块放到岛上。从 Dock 右键移除。卡片名称下不跟说明；`BuiltInWidgets.Blurb` 只用于预览悬停和搜索。无匹配时显示图标 + 标题 + 一句说明。时钟预览定时器只在目录可见时跑。界面中英双语，文案来自 `UiText`。

## 添加程序

[`InstalledAppPicker.xaml`](../../../src/WinDock.App/InstalledAppPicker.xaml)。搜索开始菜单、28px 图标行、取消/添加。合并 `SettingsTheme`，跟随当前主题。

## 文本提示

[`TextPrompt.cs`](../../../src/WinDock.App/TextPrompt.cs)。360×176，按钮和输入框用 `SettingsTheme`。用于重命名情景。

## 托盘

[`App.xaml.cs`](../../../src/WinDock.App/App.xaml.cs)。`SystemIcons.Application`。左键/双击打开设置。菜单：设置、退出；`ProfilesEnabled` 时才有撤销情景和情景勾选列表。气球提示 4s。不要改成 WPF 自定义托盘菜单。
