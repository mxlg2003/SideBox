---
name: windock-ui
description: >-
  Documents the current WinDock WPF visual system and design tokens. Use when
  adding or changing the Dock island, pins, widgets, flyouts, settings, pickers,
  XAML, colors, typography, spacing, theme, or backdrop. Read this skill before
  any UI work. 做 Dock / 模块 / 弹出层 / 设置 / 主题 / 材质 / 颜色 / 间距时必须先读。
---

# WinDock UI

记录**当前已经实现的界面**，不是设计提案。改 UI 前先读本 skill；新颜色 / 圆角 / 字号 / 间距先进入 token，再写控件。

## 必读

1. 本文件（规则）
2. 需要查数值时读 [tokens.md](tokens.md)
3. 需要认界面时读 [surfaces.md](surfaces.md)
4. 改内置模块时读 [widgets.md](widgets.md)
5. 改完会进运行中进程的 UI 后，按 [restart-windock](../restart-windock/SKILL.md) 重启

产品边界仍以 `docs/PRODUCT.md` 为准。本 skill 不管功能范围，只管长什么样、用哪套常量。

## 硬规则

1. **先 token 再像素。** 新颜色、圆角、字号、间距、阴影、动效时长写入 [`src/WinDock.Core/DockTokens.cs`](../../../src/WinDock.Core/DockTokens.cs)。便笺纸色走 `NotePapers`，专注计时器色板走 `FocusTimerColors`。禁止再往 XAML / 控件里塞裸 hex 或魔术数字。
2. **模块嵌在岛上。** 岛上是 `WidgetChrome.Wrap` 药丸（高 50、无标题行、图标+主值）。点开用 `WidgetChrome.Flyout` + `WidgetFlyout.Attach`，贴岛 8px 展开。不另开独立 widget 窗口，不用 WebView2。
3. **两套表面。** Dock 岛跟随 `ThemePreference` + `DockBackdrop` + `IslandTransparency`（半透明分层 HWND）。设置窗 / 选择器是不透明卡片，用 `DockTokens.Settings`，不套云母/磨砂/玻璃。
4. **主题从 token 解析。** 岛面 `DockTokens.ResolveIslandFill`，药丸 `ResolveTileFill`，菜单/弹出层 `ResolveMenuFill`。文字浅 `Light.Text`、深 `Dark.Text`。设置窗 `DockTokens.Settings.*(isLight)`。
5. **字体固定。** UI `Segoe UI Variable, Segoe UI`；图标 `Segoe MDL2 Assets`；便笺 `Candara, Microsoft YaHei UI, Segoe UI`。文件夹徽标才用 Tahoma + 内嵌 Noto Sans SC。
6. **托盘例外。** `NotifyIcon` + WinForms `ContextMenuStrip` 走系统菜单，不套 WPF token。
7. **不碰产品边界。** 不上主屏底部任务栏、不做 AppBar、不替换 Explorer / 任务栏、不把 Dock 画成 macOS 逐像素复刻。
8. **用户文案。** 岛上不铺自定义名称/标签，只留时间或短状态。打开模块后自动换行看全文，不要截断、不要把整段塞进 Tooltip。便笺继续换行+滚动。设置列表和钉名仍可截断+悬停全文。详见 [user-text-overflow](../../rules/user-text-overflow.mdc)。
9. **滚动条一致性。** 模块内任何滚动视图/下拉列表（如天气城市建议、便笺历史、设置列表等）必须统一使用 WinDock 现代滚动条样式（12px 宽透明槽、6px 宽圆角胶囊滑块、无上下箭头按键、主题色滑块 `DockTokens.Settings.ScrollThumb*`），禁止出现 Windows 原生灰底方块滚动条或传统上下箭头按钮。通过 `WidgetChrome.PaintScrollViewer` 或在 `WidgetChrome.ApplyTree` 中统一附着样式，外观与设置-模块（`Settings.ScrollBarVertical`）完全一致。

## 组合层（不要绕开）

| 要做 | 用这个 |
|---|---|
| 岛上药丸 | `WidgetChrome.Wrap` |
| 贴岛弹出层 | `WidgetChrome.Flyout` + `WidgetFlyout.Attach` |
| 模块输入井 | `WidgetChrome.Field`（`ResolveFieldFill`，禁止 `Background=Transparent` 的 TextBox） |
| 模块下拉 | `WidgetChrome.PaintCombo`（岛面 token，外形同设置 ComboBox；`ApplyTree` 会扫到） |
| 模块滚动条 / 列表滚动 | `WidgetChrome.PaintScrollViewer`（12px 槽、6px 圆角胶囊滑块、无上下箭头，同设置-模块 `Settings.ScrollBarVertical`；`ApplyTree` 会自动扫到） |
| 次要按钮 / 芯片 | `WidgetChrome.MarkQuiet`（`ResolveQuietFill`，禁止用 `QuietFill` 画深色按钮） |
| 主/次文字 | `WidgetCopy.Primary` / `Secondary` |
| 图标字形 | `WidgetChrome.Glyph` 或 Segoe MDL2 |
| Dock 右键菜单 | `DockMenu.Create` / `Item` / `DangerItem` |
| ARGB → 刷 | `TokenBrushes.From`（Shell 与 Widgets 各有一份） |
| 主题扫树 | `WidgetChrome.ApplyTree` |

`App.xaml` 没有 ResourceDictionary。MVP 不引入 WinUI 主题或全局 XAML 样式表。XAML 里的浅色初值只给窗口尚未 `ApplyTheme` 时用，**以 code-behind 的 token 为准**。

## 新 UI 检查清单

- [ ] 读过 tokens / 对应 surface 或 widget 页
- [ ] 没有新增裸 `#RRGGBB` / `0xAARRGGBB`（先加常量）
- [ ] 模块内凡出现滚动条（列表、下拉联想、多行文本）均使用 `WidgetChrome.PaintScrollViewer`，禁止 Windows 原生灰底带箭头滚动条
- [ ] 药丸高度、圆角、间距来自 `DockTokens`
- [ ] 浅色 / 深色都能看；岛面三种材质不把字洗掉
- [ ] 模块需要输入或详情时用 flyout，不新开 Window
- [ ] 岛上不铺长的用户名称；打开模块后换行看全文，不截断
- [ ] `Tag = "tint"` / `"ink"` 的文字不会被 `ApplyTheme` 刷成正文色
- [ ] 改完运行时 UI 后已重启 WinDock

## 已知漂移（本轮不要“顺手统一”）

模块内部仍有 `0xFF0078D4`（Win11 蓝）和重复的 `QuietFill = 0x14000000`。设置强调色是 `DockTokens.Settings.Accent`（`#2563EB`）。不要为了对齐而改十个模块的外观。新代码用 `DockTokens.QuietFill`；设置/目录用 `Settings.Accent`；模块里已有的 Win11 蓝保持原样，直到单独的视觉任务。
