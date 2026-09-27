# Design tokens

运行时源：[`src/WinDock.Core/DockTokens.cs`](../../../src/WinDock.Core/DockTokens.cs)。色值是 **ARGB `uint`**。改数值先改这个文件，再改文档。

便笺纸色：`NotePapers`。专注计时器色板：`FocusTimerColors`。热区像素：`DockHostGeometry.HotzonePixels`。

## 布局

| Token | 值 | 用途 |
|---|---|---|
| `IslandCornerRadius` | 8 | 岛、flyout、Dock 菜单 |
| `IslandPaddingX/Y` | 5 | 岛内边距 |
| `IslandMargin` | 10 | 岛到工作区边缘 |
| `MinIslandLength` | 160 | 岛最短边（沿边缘方向） |
| `TileCornerRadius` | 4 | 药丸、按钮、输入、菜单项、钉名 |
| `TilePaddingX/Y` | 10 / 6 | 药丸内边距 |
| `TileHeight` | 50 | 药丸与钉槽高度 |
| `TileGap` | 3 | 槽之间 |
| `ArtCornerRadius` | 4 | 封面/缩略默认 |
| `ArtSize` | 40 | 钉图标与媒体封面 |
| `GlyphSize` | 14 | 模块图标字形 |
| `ClockFaceSize` | 26 | 岛上模拟时钟 |
| `ClockPanelFaceSize` | 84 | 弹出层模拟时钟 |
| `SparklineHeight` | 14 | 网络火花线默认高 |
| `SparklineDockWidth` | 40 | 岛上网络火花线宽 |
| `ResizeGrip` | 8 | 岛两端拖宽热区 |
| `OverflowButtonWidth/Height` | 32 | 左右箭头按钮 |
| `OverflowGlyphSize` | 12 | 箭头字形 |
| `OverflowButtonGap` | 4 | 两箭头间距 |
| `FlyoutUserCopyMaxWidth` | 248 | 弹出层用户短输入换行宽度 |
| `ScrollStep` | 140 | 溢流一次滚动 |
| `AutoHideStrip` | 4 | 自动隐藏贴边细条 |
| `AutoHideIdleMs` | 800 | 离开岛后收成细条 |
| `HysteresisDelayMs` | 300 | 指针刚离开岛时暂不收回 |
| `WakeHoldMs` | 2000 | 唤醒快捷键后保持可见 |
| `FlyoutOffset` | 8 | 弹出层离岛距离（`WidgetFlyout`） |
| `DockHostGeometry.HotzonePixels` | 8 | 唤出热区 |
| `TrapezoidBuffer` | 8 | 岛接近热区两侧/外侧的 slack |

岛厚度：`DockLayoutCalculator.DefaultThickness` = `TileHeight + 2*IslandPaddingY + 2`（描边）。侧边与顶边同厚。内容超出工作区时夹紧，岛沿边缘居中。

钉槽宽 = 图标面宽 + 16。文件夹图标可按位图比例拉到 `ArtSize × 1.65`。图标按 48px 解码、40px 显示。

## 透明度（岛面）

用户滑块 0–100，写入 `UiSettings.IslandTransparency`。换材质时重置为该材质默认：

| 材质 | 默认滑块 | `EffectiveTransparency` |
|---|---|---|
| 云母 `Mica` | 50 | `t * 55 / 100`（更实） |
| 磨砂 `Frosted` | 0 | `t * 78 / 100` |
| 玻璃 `Glass` | 80 | `t`（最透） |

实际 alpha：`MinIslandAlpha(12) + (MaxIslandAlpha(220) - 12) * (100 - effective) / 100`。`backdropOk == false` 时用 `IslandFallback` 实色。

岛面 RGB 按材质选 `IslandFill` / `IslandFrosted` / `IslandGlass`，再套上面的 alpha。菜单和钉名用 `ResolveMenuFill`（同 RGB、alpha 255）。

## 圆角与阴影

| 用途 | 圆角 | 阴影 |
|---|---|---|
| 岛 / flyout / 菜单 | 8 | flyout：`Blur 24, Depth 4, Dir 270, Opacity 0.18` |
| 药丸 / 控件 / 钉名 | 4 | 钉名：`Blur 16, Depth 2, Dir 270, Opacity 0.22` |
| Bing 缩略 | **8**（模块特例，不是 `ArtCornerRadius`） | 无 |
| 播放圆钮 | 16（32×32） | 无 |
| 设置窗口 | 无内嵌卡片，铺 `Settings.Page` | 无 |

flyout 内边距 `16,12,16,20`（便笺底 16）。菜单内边距 6，项 `10,7`，最小宽 176。

## 字体

| 角色 | 字体 |
|---|---|
| UI | `Segoe UI Variable, Segoe UI` |
| 图标 | `Segoe MDL2 Assets` |
| 便笺 | `Candara, Microsoft YaHei UI, Segoe UI` |
| 文件夹徽标拉丁 | `Tahoma` |
| 文件夹徽标汉字 | 内嵌 `Noto Sans SC`（`WinDock.Shell/Fonts`） |

字重：主值 `SemiBold`，设置分区标题 `SemiBold`，正文 Regular。便笺预览 `Bold`。

## 字号

| 大小 | 用在 |
|---|---|
| 9 | 文件夹徽标 |
| 10 | 正在播放艺人/进度时间 |
| 11 | `SecondaryFontSize`、秒、圆环内数字、菜单勾 |
| 12 | 菜单、钉名、flyout 小标题、占位芯片 |
| 13 | 设置正文、目录卡片名、菜单项、部分状态 |
| 14 | `GlyphSize`、flyout 日期、部分按钮 |
| 15 | `PrimaryFontSize` 主值 |
| 16 | 设置分区大标题、专注岛上时间、便笺编辑 |
| 17 | 岛上时钟/秒表时间 |
| 18 | flyout 关闭 × |
| 20–22 | 目录预览主值、时间进度弹出层标题 |
| 26 | 无图标时的钉字形；电源大图标 |
| 32 | 设置/选择器按钮高 |
| 38 | 弹出层大时间 |

设置窗：960×680，最小 760×520。添加程序：420×560。文本提示：360×168。

## 不透明度（前景）

| Opacity | 用在 |
|---|---|
| 0.18 / 0.22 | flyout / 钉名阴影 |
| 0.28 | 溢流按钮禁用 |
| 0.45 | 缺失钉、关闭 ×、搜索图标 |
| 0.55 | 占位模块、空状态、部分标签 |
| 0.62 | 次要文字、溢流空闲、设置说明 |
| 0.75–0.85 | 设置帮助段落 |
| 1 | 悬停/启用 |

`WidgetCopy.Secondary` 默认 0.62。

## 浅 / 深：岛与药丸

### Dark

| Token | ARGB |
|---|---|
| `IslandFill` | `0xFF2A2A38` |
| `IslandFrosted` | `0xFF1A1A22` |
| `IslandGlass` | `0xFF0C1018` |
| `IslandFallback` | `0xF2141418` |
| `OverflowFill` | `0xE632323A` |
| `TileFill` | `0xF03A3A46` |
| `TileFrosted` | `0xE6363640` |
| `TileGlass` | `0xCC2E2E38` |
| `TileHover` | `0x33FFFFFF` |
| `Stroke` | `0x26FFFFFF` |
| `Text` | `0xFFFFFFFF` |

### Light

| Token | ARGB |
|---|---|
| `IslandFill` | `0xFFF3EFE6` |
| `IslandFrosted` | `0xFFE6EEF4` |
| `IslandGlass` | `0xFFFFFFFF` |
| `IslandFallback` | `0xE6F2F2F4` |
| `OverflowFill` | `0xF2F1F5FA` |
| `TileFill` | `0xF7FFFFFF` |
| `TileFrosted` | `0xE6F2F4F8` |
| `TileGlass` | `0xCCFFFFFF` |
| `TileHover` | `0xFFFFFFFF` |
| `Stroke` | `0x1A000000` |
| `Text` | `0xFF1A1A1A` |

解析：

- 岛 `ResolveIslandFill(isLight, backdropOk, backdrop, transparency)`
- 药丸 `ResolveTileFill(isLight, backdrop)`
- 菜单/钉名/flyout 底 `ResolveMenuFill(isLight, backdropOk, backdrop)`
- 模块输入井 `ResolveFieldFill(isLight, backdrop)`：不透明。浅色把菜单底压暗到 90%，深色向白提亮 16%。描边仍用 `Light/Dark.Stroke`。便笺编辑框走纸色，不用这个。
- 次要按钮/芯片 `ResolveQuietFill`：与输入井同色。旧的 `QuietFill`（`0x14000000`）在深色弹出层上几乎看不见，不要再拿来画按钮。

运行中钉点：浅 `0xFF1A1A1A`，深 `0xFFFFFFFF`（即正文色）。文件夹徽标字始终 `0xFF1A1A1A`（压在彩色图标上）。

## 语义色

| Token | ARGB | 用在 |
|---|---|---|
| `Cpu` | `0xFFC084FC` | 系统状态 CPU |
| `Memory` | `0xFF3B82F6` | 系统状态内存 |
| `Network` | `0xFF3B82F6` | 下载 |
| `NetworkUp` | `0xFFD946EF` | 上传 |
| `BatteryOk` | `0xFF34D399` | 电量正常 |
| `BatteryLow` | `0xFFFBBF24` | 电量偏低 |
| `BatteryCritical` | `0xFFF87171` | 电量危急 |
| `NotesLight` / `NotesDark` | `0xF2FBF0C8` / `0xE6C4A35A` | 旧便笺底（岛上现用 `NotePapers`） |
| `Focus` | `0xFFA78BFA` | 专注语义 |
| `Bing` | `0xFF38BDF8` | Bing 字形 |
| `Alarm` | `0xFFFF9F0A` | 已设定闹钟字形 |
| `QuietFill` | `0x14000000` | 安静填充（圆钮、未选中 chip） |
| `Danger` | `0xFFE11D48` | 菜单危险项 |

## 设置窗 `DockTokens.Settings`

不透明。XAML 浅色初值可留；运行时以这些为准。

| Token | Light | Dark |
|---|---|---|
| `Page` / `BgPrimary` | `0xFFF4F4F6` | `0xFF1C1C1E` |
| `Card` | `0xFFFFFFFF` | `0xFF2C2C2E` |
| `Stroke` / `BorderDefault` | `0x14000000` | `0x33FFFFFF` |
| `BorderStrong` | `0x33000000` | `0x66FFFFFF` |
| `ScrollThumb` | `0x59000000` | `0x66FFFFFF` |
| `ScrollThumbHover` | `0x8A000000` | `0x99FFFFFF` |
| `ScrollThumbPressed` | `0xB3000000` | `0xCCFFFFFF` |
| `Text` | `0xFF1A1A1A` | `0xFFF2F2F2` |
| `Muted` | `0xFF6F6F76` | `0xFFB0B0B5` |
| `FgDisabled` | `0xFFB0B0B5` | `0xFF8E8E93` |
| `NavPane` | `0xFFF4F5F7` | `0xFF232326` |
| `NavSelected` | `0xFFE6E6EA` | `0xFF48484A` |
| `Catalog` | `0xFFFFFFFF` | `0xFF1C1C1E` |
| `Preview` | `0xFFF1F2F4` | `0xFF3A3A3C` |
| `Search` / `BgSecondary` | `0xFFE8E8EC` | `0xFF3A3A3C` |
| `Accent` | `0xFF2563EB` | 同 |
| `AccentHover` | `0xFF1D4ED8` | 同 |
| `OnAccent` | `0xFFFFFFFF` | 同 |
| `FocusRing` | `0xFF2563EB` | 同 |
| `DangerSubtle` | `0x14E11D48` | `0x33E11D48` |

控件高 `Settings.ControlHeight` = 32。左栏 `NavWidth` = 168，模块分类列 `CatalogNavWidth` = 168，栏内边距 `NavPad` = 12。目录卡 `CatalogCardWidth` = 160、`CatalogCardGap` = 16、预览井 `CatalogPreviewHeight` = 96、`CatalogPreviewPad` = 12。Tab 最小宽 `TabMinWidth` = 96（旧胶囊样式仍在主题里）。表单 `FieldGap` = 8、`SectionGap` = 24、`PagePad` = 24、`ControlPad` = 12。滚动条槽 `ScrollThickness` = 12、滑块 `ScrollThumbSize` = 6。焦点环 `FocusRingThickness` = 2。按钮：`Settings.Button` 次要（描边 + `BgSecondary`）、`PrimaryButton` 确定/添加、`DestructiveButton` 删除、`GhostButton` 目录 +。开关 `Settings.Toggle`。设置导航 `Settings.NavItem`。滚动条隐式 `ScrollBar`（无上下箭头、透明槽、圆角滑块）。模板在 [`SettingsTheme.xaml`](../../../src/WinDock.App/Settings/SettingsTheme.xaml)。

## 模块色板（不放进 Light/Dark）

`NotePapers.Fill`：黄 `0xFFF6D15C`、橙 `0xFFF5A24A`、粉 `0xFFEE6B7A`、品红 `0xFFD45EC8`、蓝 `0xFF4BA6F5`、绿 `0xFF4FCF7C`。墨水 `NotePapers.Ink` = `0xFF1A1A1A`。

`FocusTimerColors.Fill`：橙 `0xFFFF9F0A`、红 `0xFFFF3B30`、品红 `0xFFFF2D55`、紫 `0xFFAF52DE`、靛 `0xFF5856D6`、蓝 `0xFF007AFF`、青 `0xFF00C7BE`、绿 `0xFF34C759`。

## 动效

统一 `CubicEase` + `EaseOut`。

| 场景 | 时长 | 属性 |
|---|---|---|
| flyout 打开 | 140ms | opacity 0→1，scale 0.96→1 |
| 钉名出现 | 90ms（重开 160ms） | opacity + 位移 |
| 钉名消失 | 140ms | opacity + 位移 |
| 槽重排 | 180ms | TranslateTransform |
| 便笺自动保存 | 400ms debounce | 非动画 |
| 自动隐藏空闲 | 离开岛 800ms | 状态，非动画 |
| 唤醒快捷键保持 | 热键唤出后 2000ms | 状态，非动画 |

## 已知漂移

- 电源 / 系统状态 / 秒表 / 专注 / 时间进度内部 `Accent = 0xFF0078D4`，与 `Settings.Accent`（`0xFF2563EB`）不同。
- 上述模块各有一份 `QuietFill = 0x14000000`；新代码用 `DockTokens.QuietFill`。
- 正在播放进度/封面占位 `0x99000000`、`0x33000000`、`0x33FFFFFF` 未收进 token。
- Bing 岛上缩略圆角 8，封面 token 是 4。
