namespace WinDock.Core;

/// <summary>User-facing zh/en copy. Widget format classes keep their own specialized strings.</summary>
public static class UiText
{
    public static string T(string zh, string en) => UiLocale.T(zh, en);

    public static string Settings => T("设置", "Settings");
    public static string Exit => T("退出", "Exit");
    public static string ExitWinDock => T("退出 WinDock", "Exit WinDock");
    public static string CloseWindow => T("关闭窗口", "Close window");
    public static string CloseAllWindows => T("关闭所有窗口", "Close all windows");
    public static string BackgroundRunningNotice => T("设置已保存。WinDock 仍在屏幕边缘运行，可在 Dock 或系统托盘右键退出。", "Settings saved. WinDock remains active at the screen edge; right-click the Dock or tray icon to exit.");
    public static string Cancel => T("取消", "Cancel");
    public static string Confirm => T("确定", "OK");
    public static string Add => T("添加", "Add");
    public static string Back => T("返回", "Back");
    public static string Customize => T("自定义", "Customize");
    public static string Layout => T("布局", "Layout");
    public static string Display => T("显示", "Display");
    public static string System => T("系统", "System");
    public static string Advanced => T("高级", "Advanced");
    public static string Appearance => T("外观", "Appearance");
    public static string Privacy => T("隐私", "Privacy");
    public static string About => T("关于", "About");
    public static string Profiles => T("情景", "Profiles");
    public static string Modules => T("模块", "Widgets");
    public static string Language => T("语言", "Language");
    public static string OnIsland => T("Dock", "the Dock");
    public static string None => T("无", "None");
    public static string Preview => T("预览", "Preview");

    public static string LanguageSystem => T("跟随系统", "Follow system");
    public static string LanguageChinese => "中文";
    public static string LanguageEnglish => "English";
    public static string LanguageHelp => T(
        "设置界面语言。设为跟随系统时，在中文 Windows 环境下显示中文，其他语言环境下显示英文。",
        "Set the interface language. Following system uses Chinese on Chinese Windows, and English on all other locales.");

    public static string Theme => T("主题", "Theme");
    public static string ThemeSystem => T("跟随系统", "Follow system");
    public static string ThemeLight => T("浅色", "Light");
    public static string ThemeDark => T("深色", "Dark");
    public static string Backdrop => T("材质", "Material");
    public static string BackdropMica => T("云母（偏实）", "Mica (opaque)");
    public static string BackdropFrosted => T("磨砂（雾面）", "Frosted");
    public static string BackdropGlass => T("玻璃（通透）", "Glass (translucent)");
    public static string BackdropMicaShort => T("云母", "Mica");
    public static string BackdropFrostedShort => T("磨砂", "Frosted");
    public static string BackdropGlassShort => T("玻璃", "Glass");
    public static string IslandTransparency => T("Dock 透明度", "Dock transparency");
    public static string AppearanceIntro => T("主题、材质和透明度设置将应用于整个 Dock。", "Theme, material, and transparency settings apply to the entire Dock.");
    public static string AppearanceFooter => T(
        "切换材质会自动应用推荐的预设透明度：云母（偏实）、磨砂（雾面）、玻璃（通透）。设置面板打开时 Dock 将保持常驻，便于即时预览效果。",
        "Switching materials applies recommended default transparency: Mica (opaque), Frosted (matte), Glass (translucent). The Dock remains visible while Settings is open for live preview.");

    public static string ModulesIntro => T("把时钟、便笺等加到边缘岛上。", "Add the clock, notes, and other widgets to the edge island.");
    public static string SearchWidgets => T("搜索模块", "Search widgets");
    public static string CatalogAll => T("全部模块", "All widgets");
    public static string CatalogClocks => T("时钟", "Clocks");
    public static string CatalogNotes => T("便笺", "Notes");
    public static string CatalogMedia => T("媒体", "Media");
    public static string CatalogSystem => T("系统", "System");
    public static string CatalogWallpaper => T("壁纸", "Wallpaper");
    public static string CatalogLifestyle => T("生活效率", "Lifestyle");
    public static string CatalogEmptyTitle => T("没有匹配的模块", "No matching widgets");
    public static string CatalogEmptyHint => T("换个关键词，或选左侧分类。", "Try another keyword, or pick a category.");
    public static string AddToIsland => T("添加到 Dock", "Add to the Dock");
    public static string AddToProfile(string name) => T($"添加到「{name}」", $"Add to “{name}”");

    public static string LayoutIntro => T("配置屏幕停靠边缘、目标显示器，以及空闲时的自动隐藏行为。", "Configure dock edge, target display, and auto-hide behavior when idle.");
    public static string Edge => T("边缘位置", "Edge");
    public static string EdgeTop => T("上", "Top");
    public static string EdgeLeft => T("左", "Left");
    public static string EdgeRight => T("右", "Right");
    public static string Monitor => T("显示器", "Display");
    public static string AutoHide => T("自动隐藏", "Auto-hide");
    public static string AutoHideHelp => T("无交互时自动收起至屏幕边缘，避免遮挡最大化窗口。", "Automatically tucks into the screen edge when idle to avoid obscuring maximized windows.");
    public static string Peek => T("鼠标靠近边缘热区时自动滑出", "Slide out when pointer enters the edge hotzone");
    public static string PeekHelp => T("鼠标指针移入屏幕边缘的感应区域时自动滑出 Dock。", "Move the mouse pointer into the edge trigger zone to reveal the Dock.");
    public static string RevealHotkey => T("全局唤醒快捷键", "Global reveal shortcut");
    public static string RevealHotkeyHelp => T("开启后可通过全局快捷键呼出处于隐藏状态的 Dock。若快捷键被占用，可在下方重新录制。", "Press a global shortcut to reveal the hidden Dock. Change the shortcut below if it conflicts with another app.");
    public static string RevealHotkeyName => T("唤醒快捷键", "Reveal shortcut");
    public static string CollapseApps => T("程序折叠", "Collapse apps");
    public static string CollapseAppsHelp => T("Dock 上仅显示一个聚合程序图标，点击可展开全部应用程序与快捷方式。", "Show a single consolidated apps icon on the Dock; click to expand all apps and shortcuts.");
    public static string CollapseFolders => T("文件夹折叠", "Collapse folders");
    public static string CollapseFoldersHelp => T("Dock 上仅显示一个聚合文件夹图标，点击可展开全部已钉选的文件夹。", "Show a single consolidated folder icon on the Dock; click to expand all pinned folders.");
    public static string LayoutFooter => T(
        "WinDock 默认为贴边居中的悬浮岛，在全屏应用或演示模式下会自动避让。可拖动两端调整宽度，双击边缘可恢复根据内容自适应。在多显示器间拖拽 Dock 会自动同步此处的显示器设置。内容超出屏幕范围时边缘将显示翻页箭头。",
        "WinDock floats centered along the chosen screen edge and automatically yields for fullscreen apps or presentations. Drag the ends to adjust width, or double-click an edge to fit content. Dragging the Dock across screens updates the display setting. Overflow arrows appear when content exceeds available space.");

    public static string PrivacyIntro => T("所有权限默认处于关闭状态。关闭权限后对应模块仍保留在 Dock 上，但不再读取相关数据。", "All permissions are disabled by default. Disabling a permission keeps widgets on the Dock, but stops data access.");
    public static string MediaPrivacy => T("允许读取正在播放的标题和封面", "Allow reading Now Playing title and artwork");
    public static string NetworkPrivacy => T("允许显示网络适配器名称和速率", "Allow showing adapter names and rates");
    public static string NetworkPrivacyHelp => T("数据仅在本地处理，绝不上传网络。", "Data is processed locally on this PC; nothing is uploaded.");
    public static string BingPrivacy => T("允许从 bing.com 下载壁纸图", "Allow downloading wallpapers from bing.com");
    public static string BingPrivacyHelp => T("将其设为桌面壁纸时会二次确认。", "Confirmation is requested before setting as desktop wallpaper.");
    public static string LocationPrivacy => T("允许使用大致位置自动填写天气城市", "Allow approximate location to fill the weather city");
    public static string LocationPrivacyHelp => T("仅在你点击「自动定位」后，通过 HTTPS 查询大致城市。可随时关闭；关闭后只使用已填写的城市。", "Used only after you tap Auto Location. Looks up an approximate city over HTTPS. You can turn this off anytime; the typed city is kept.");
    public static string PrivacyFooter => T("未启用崩溃数据上报，所有诊断日志仅保存在本地设备。", "Crash reporting is disabled. All diagnostic logs remain strictly on this PC.");
    public static string MediaHintOff => T("关闭后模块仍保留在 Dock 上，但不再读取歌曲名和专辑封面。", "The widget remains on the Dock; it simply stops reading track titles and artwork.");
    public static string MediaHintGrant(string profile) => T(
        $"开启后仅对当前情景「{profile}」授权。关闭后模块仍保留在 Dock 上，但不再读取歌曲名和专辑封面。",
        $"Turning this on grants permission to profile “{profile}” only. The widget remains on the Dock if disabled, but stops reading track titles and artwork.");
    public static string MediaHintGranted(string names) => T(
        $"已授权：{names}。关闭后模块仍保留在 Dock 上，但不再读取歌曲名和专辑封面。",
        $"Granted: {names}. The widget remains on the Dock if you turn this off; it simply stops reading track titles and artwork.");

    public static string AboutIntro => T(
        "WinDock 是一款轻量贴边桌面悬浮岛，用于快捷启动与情境信息展示。不替换 Windows 任务栏，不以管理员权限运行。",
        "WinDock is a lightweight desktop edge dock for quick launches and contextual widgets. It does not replace the Windows taskbar and never requires administrator privileges.");
    public static string AboutConfig(string settings, string logs) => T($"配置：{settings}{Environment.NewLine}日志：{logs}", $"Settings: {settings}{Environment.NewLine}Logs: {logs}");
    public static string RunAtStartup => T("开机启动", "Start with Windows");
    public static string RunAtStartupHelp => T("仅对当前 Windows 用户生效，不影响其他用户。", "Applies to the current Windows user only.");

    public static string ProfilesIntro => T("复制情景将完整保留所有钉选启动项与模块配置。可通过本页下拉菜单、系统托盘右键菜单或快捷键进行情景切换。", "Duplicating a profile creates a full copy of its pinned items and widget configurations. Switch profiles via this dropdown, the system tray menu, or hotkeys.");
    public static string CurrentProfile => T("当前情景", "Current profile");
    public static string UndoLast => T("撤销上一次切换", "Undo last switch");
    public static string DuplicateProfile => T("复制当前情景", "Duplicate profile");
    public static string Rename => T("重命名", "Rename");
    public static string Delete => T("删除", "Delete");
    public static string RecentSwitches => T("最近切换", "Recent switches");
    public static string RenameProfileTitle => T("重命名情景", "Rename profile");
    public static string DisplayNameLabel => T("显示名称", "Display name");
    public static string DeleteProfileTitle => T("删除情景", "Delete profile");
    public static string DeleteProfileConfirm(string name) => T($"确定要删除情景「{name}」吗？默认的「日常」情景将被保留。", $"Are you sure you want to delete profile “{name}”? The default “Daily” profile will be kept.");
    public static string CannotDeleteGeneral => T("「日常」是系统基础情景，无法删除。", "“Daily” is the default fallback profile and cannot be deleted.");
    public static string DeleteCurrentProfile => T("删除当前情景", "Delete current profile");
    public static string ProfileHotkeys(string next, string undo) => T(
        $"当前切换快捷键（暂不支持自定义）：下一个情景 {next}，撤销上一次 {undo}。亦可通过系统托盘菜单切换。",
        $"Current shortcuts (customization coming soon): next profile {next}, undo {undo}. The tray menu can also switch profiles.");
    public static string FocusProfileHint => T(
        "模块中的「专注计时器」为本地独立番茄钟，不会联动触发 Windows 专注助手，亦不会自动切换情景。",
        "The Focus Timer widget is a local standalone pomodoro clock. It does not activate Windows Focus or trigger profile switches.");
    public static string CopySuffix => T(" 的副本", " copy");
    public static string ProfileDaily => T("日常", "Daily");
    public static string ProfileDeepWork => T("深度工作", "Deep Work");

    public static string SwitchSucceeded => T("成功", "OK");
    public static string SwitchFailed => T("失败", "Failed");
    public static string ReasonManual => T("手动", "Manual");
    public static string ReasonFocus => T("系统专注", "Focus");
    public static string ReasonDisplay => T("显示器", "Display");
    public static string ReasonSchedule => T("时间段", "Schedule");
    public static string ReasonFallback => T("回退", "Fallback");
    public static string ReasonSafeMode => T("安全模式", "Safe mode");

    public static string AddApp => T("添加程序…", "Add app…");
    public static string AddShortcut => T("添加快捷方式…", "Add shortcut…");
    public static string AddFolder => T("添加文件夹…", "Add folder…");
    public static string AddFolderTitle => T("添加文件夹", "Add folder");
    public static string AddShortcutTitle => T("添加快捷方式", "Add shortcut");
    public static string AddAppTitle => T("从开始菜单选择程序", "Add app");
    public static string StartMenuApps => T("从开始菜单选择程序", "Apps from the Start Menu");
    public static string SearchApps => T("搜索应用程序…", "Search apps…");
    public static string FileFilter => T("快捷方式与程序|*.lnk;*.exe|所有文件|*.*", "Shortcuts and apps|*.lnk;*.exe|All files|*.*");
    public static string OpenWidget => T("打开模块", "Open widget");
    public static string Compact => T("紧凑", "Compact");
    public static string Expanded => T("展开", "Expanded");
    public static string HideNotes => T("隐藏便笺", "Hide notes");
    public static string RemoveFromDock => T("从 Dock 移除", "Remove from Dock");
    public static string OpenNew => T("在新窗口中打开", "Open in new window");
    public static string SwitchExisting => T("左键激活已有窗口", "Left-click switches to existing window");
    public static string ScrollLeft => T("向左", "Left");
    public static string ScrollRight => T("向右", "Right");
    public static string ScrollUp => T("向上", "Up");
    public static string ScrollDown => T("向下", "Down");
    public static string ShortcutFallback => T("快捷方式", "Shortcut");
    public static string PinFallback => T("钉选", "Pin");
    public static string LaterSuffix => T("（稍后）", " (later)");
    public static string EmptyDock => T("右键添加程序或直接拖入文件；添加时钟、便笺等模块请打开「设置 → 模块」", "Right-click to add apps, or drag and drop files from File Explorer. Add widgets via Settings → Widgets");
    public static string EmptyDockName => T("空 Dock，可右键或拖入程序；添加模块请打开设置", "Empty Dock. Right-click or drop apps; add widgets from Settings");
    public static string AppsStack(int count) => T($"程序 · {count}", $"Apps · {count}");
    public static string FoldersStack(int count) => T($"文件夹 · {count}", $"Folders · {count}");
    public static string FollowPrimary => T("始终跟着 Windows 主屏", "Always follow the Windows primary display");
    public static string PrimaryMonitor(string size) => T($"主屏 · {size}", $"Primary · {size}");
    public static string OtherMonitor(int index, string size) => T($"屏幕 {index} · {size}", $"Display {index} · {size}");

    public static string HotkeyTaken => T("该快捷键已被系统或其他程序占用，请更换其他组合。", "That shortcut is already in use by another application. Please choose another combination.");
    public static string PressShortcut => T("请按下快捷键组合…", "Press shortcut keys…");
    public static string SafeModeBlock => T("当前处于安全模式：已停用所有模块、设置面板及拖拽交互。退出程序后正常启动即可恢复。", "Safe mode active: widgets, Settings, and drag-and-drop are disabled. Exit and launch normally to restore full functionality.");
    public static string AlreadyRunning => T("WinDock 已经在运行。", "WinDock is already running.");
    public static string LoggedToFile => T("已记录到日志。", "Logged.");
    public static string SafeModeTray => T("WinDock — 安全模式", "WinDock — Safe mode");
    public static string UndoLastProfile => T("撤销上一次情景", "Undo last profile");
    public static string SaveFailed => T("设置保存失败，详细信息已记录至日志。", "Failed to save settings. Details have been logged.");
    public static string DockUpdateFailed => T("Dock 界面更新失败，详细信息已记录至日志。", "Failed to update the Dock. Details have been logged.");
    public static string AlreadyOnDock => T("已在 Dock 上。", "Already on the Dock.");
    public static string AlreadyOnDockSkipped(int skipped) => T($"已在 Dock 上（跳过 {skipped} 项）。", $"Already on the Dock (skipped {skipped}).");
    public static string ProfileSwitched => T("情景已切换", "Profile switched");
    public static string ProfileWantsLaunch(string target) => T($"情景要启动：{target}", $"This profile wants to launch: {target}");
    public static string ProfileLaunchFailed => T("情景启动应用程序失败，详细信息已记录至日志。", "Profile failed to launch application. Details have been logged.");
    public static string RestoredFromBackup => T("已从备份恢复设置。", "Settings were restored from the backup.");
    public static string ResetToFactory => T("设置无法读取，已恢复出厂默认。", "Settings could not be read. Factory defaults were restored.");
    public static string NewerSchema(int version) => T(
        $"设置来自更新的版本（schema {version}），本版本不会覆盖该文件，已使用出厂默认。",
        $"Settings are from a newer version (schema {version}). This build will not overwrite that file and is using factory defaults.");

    public static string ConsentNowPlaying => T("将读取正在播放的歌曲名、封面和播放器名称。可以在设置的隐私页随时关闭。", "WinDock will read the song title, artwork, and player name. You can turn this off later in Privacy.");
    public static string ConsentNetwork => T("将显示网络适配器名称和上下行速率。数据只留在本机，不会上传。", "WinDock will show adapter names and up/down rates. Data stays on this PC.");
    public static string ConsentBing => T("将访问 bing.com 下载壁纸图。设为桌面时会再确认一次。", "WinDock will visit bing.com to download wallpapers. Setting as desktop wallpaper asks again.");
    public static string ConsentWeather => T("将访问 Open-Meteo 获取天气预报（不经过 WinDock 服务器）。默认只使用你填写的城市。", "WinDock will request forecasts from Open-Meteo (not via a WinDock server). It uses the city you type unless you opt in to auto-location.");
    public static string ConsentLocation => T("将通过 HTTPS 查询大致所在城市，用于天气自动定位。可以在设置的隐私页随时关闭。", "WinDock will look up an approximate city over HTTPS for weather auto-location. You can turn this off later in Privacy.");
    public static string AddWidgetTitle(string name) => T($"添加{name}", $"Add {name}");
    public static string ApplyBingMessage => T("将从 bing.com 下载高清壁纸并设为当前桌面壁纸。", "WinDock will visit bing.com to download the image and set it as your desktop wallpaper.");
    public static string TodayBing => T("今日 Bing 壁纸", "Today’s Bing wallpaper");
    public static string BingApplied => T("已设为今日桌面壁纸。", "Set as today’s desktop wallpaper.");
    public static string BingApplyFailed => T("应用 Bing 壁纸失败。", "Failed to apply the Bing wallpaper.");
    public static string RestorePreviousWallpaper => T("恢复上一张壁纸", "Restore previous wallpaper");
    public static string WallpaperRestored => T("已恢复上一张桌面壁纸。", "Restored the previous desktop wallpaper.");
    public static string WallpaperRestoreFailed => T("无法恢复上一张壁纸。", "Could not restore the previous wallpaper.");

    public static string SecondTimeZone => T("第二时区", "Second time zone");
    public static string LocalTimeOnly => T("仅本地时间", "Local time only");
    public static string PaperColor => T("纸张颜色", "Paper color");
    public static string NotesSyntax => T("便笺语法", "Notes syntax");
    public static string NotesSyntaxHint => T(
        "第一行输入 ### 可设为标题。设置标题后 Dock 仅显示标题；未设置时默认预览前两行正文。\n支持 - [ ] / - [x] 待办清单语法（快捷键 Ctrl+Enter 快速切换）。",
        "Start the first line with ### to set a title. With a title, the Dock shows only the title; otherwise it previews the first two lines.\nSupports - [ ] / - [x] checklist syntax (use Ctrl+Enter to toggle).");
    public static string NotesTodoToggle => T("插入/切换待办清单 (Ctrl+Enter)", "Insert/toggle todo checklist (Ctrl+Enter)");
    public static string NotesHistory => T("历史便笺", "Note history");
    public static string ReplaceCurrentNote => T("替换当前便笺", "Replace current note");
    public static string DeleteForever => T("永久删除", "Delete permanently");
    public static string DeleteThisNote => T("删除这张便笺", "Delete this note");
    public static string ConfirmDelete => T("确认删除", "Delete");
    public static string DeleteNoteForeverHint => T("将永久删除此便笺，无法从历史记录中恢复", "Deletes permanently; this note cannot be restored from history");
    public static string NotesSaveFailed => T("便笺保存失败，无法写入磁盘。", "Failed to save note to disk.");
    public static string NotesAlertTitle => T("WinDock 便笺", "WinDock Notes");
    public static string NotesWriteSomething => T("写点什么…", "Write something…");
    public static string Memory => T("内存", "Memory");
    public static string MemoryShort => T("内存", "MEM");
    public static string MemoryUsed => T("已用内存", "Used");
    public static string Download => T("下载", "Download");
    public static string Upload => T("上传", "Upload");
    public static string ShowOnDock => T("在 Dock 上显示", "Show on the Dock");
    public static string ShowAllDevices => T("显示全部设备", "Show all devices");
    public static string AllHidden => T("已全部隐藏", "All hidden");
    public static string DockChart => T("在 Dock 上显示图表", "Chart on the island");
    public static string PopupChart => T("面板显示图表", "Chart in the panel");
    public static string Period => T("周期", "Period");
    public static string FocusColor => T("专注颜色", "Focus color");
    public static string FocusLabel => T("专注", "Focus");
    public static string ShortBreak => T("短休息", "Short break");
    public static string LongBreak => T("长休息", "Long break");
    public static string LongBreakEvery => T("长休息间隔", "Long break interval");
    public static string NotifyWhenDone => T("结束时通知", "Notify when done");
    public static string ResetTimer => T("重置计时", "Reset timer");
    public static string BackToSession1 => T("重置到第 1 轮", "Reset to session 1");
    public static string MinutesUnit => T("分", "min");
    public static string SessionsUnit => T("轮", "sessions");
    public static string NotPlaying => T("未在播放", "Not playing");
    public static string PlayPause => T("播放/暂停", "Play/Pause");
    public static string Play => T("播放", "Play");
    public static string Pause => T("暂停", "Pause");
    public static string PreviousTrack => T("上一首", "Previous");
    public static string NextTrack => T("下一首", "Next");
    public static string Playing => T("播放中", "Playing");
    public static string Paused => T("已暂停", "Paused");
    public static string NoPlaybackSession => T("没有可控制的播放会话", "No controllable playback session");
    public static string PreviousDay => T("前一天", "Previous day");
    public static string NextDay => T("后一天", "Next day");
    public static string SetAsDesktop => T("设为桌面", "Set as desktop");
    public static string SetAsDesktopDone => T("已设为桌面", "Set as desktop");
    public static string Loading => T("加载中…", "Loading…");
    public static string ClickRetry => T("点击重试", "Click to retry");
    public static string ThisPc => T("这台电脑", "This PC");
    public static string Device => T("设备", "Device");
    public static string Laptop => T("电脑", "PC");
    public static string Headphones => T("耳机", "Headphones");
    public static string ChargingCase => T("充电盒", "Case");
    public static string Keyboard => T("键盘", "Keyboard");
    public static string NoBingImage => T("暂无图片", "No image");
    public static string BingLoadFailed => T("无法加载", "Could not load");
    public static string DownloadFailed => T("下载失败", "Download failed");
    public static string ApplyFailed => T("未能应用", "Could not apply");
    public static string MediaFallbackHint => T("未接入系统媒体，可用按钮控制", "Not in System Media; controls may still work");
    public static string Disconnected => T("未连接", "Disconnected");
    public static string ConnectedDetailsOff => T("已连接（详情已关）", "Connected (details off)");
    public static string FocusStarted => T("专注开始", "Focus started");
    public static string PeriodEnded => T("本段结束", "Session finished");
    public static string Today => T("今天", "Today");
    public static string Tomorrow => T("明天", "Tomorrow");

    public static string WidgetClock => T("时钟", "Clock");
    public static string WidgetNotes => T("便笺", "Notes");
    public static string WidgetSysActivity => T("系统状态", "System");
    public static string WidgetNetwork => T("网络", "Network");
    public static string WidgetPower => T("电源", "Power");
    public static string WidgetNowPlaying => T("正在播放", "Now Playing");
    public static string WidgetBing => T("Bing 壁纸", "Bing Wallpaper");
    public static string WidgetStopwatch => T("秒表", "Stopwatch");
    public static string WidgetCountdown => T("倒计时", "Countdown");
    public static string WidgetAlarm => T("闹钟", "Alarm");
    public static string WidgetFocusTimer => T("专注计时器", "Focus Timer");
    public static string WidgetTimeProgress => T("时间进度", "Time Progress");
    public static string WidgetWeather => T("天气", "Weather");
    public static string WidgetSchedule => T("日程倒数", "Schedule");

    public static string BlurbClock => T("显示本地时间，点击可展开大时钟与世界时区。", "Displays local time. Click to open a full clock and world time zones.");
    public static string BlurbNotes => T("常驻便笺小组件，点击即可快速查看与编辑。", "Sticky note on the Dock. Click to view and edit instantly.");
    public static string BlurbSysActivity => T("实时监控 CPU 与内存占用状态。", "Real-time CPU and memory monitoring.");
    public static string BlurbNetwork => T("实时监控网络上传与下载速率。", "Real-time network upload and download throughput.");
    public static string BlurbPower => T("查看本机电池及已连接蓝牙外设的电量。", "View battery status for this PC and connected Bluetooth accessories.");
    public static string BlurbNowPlaying => T("显示当前播放曲目信息、专辑封面及控制按钮。", "Displays now playing track information, album artwork, and playback controls.");
    public static string BlurbBing => T("每日同步 Bing 精选壁纸，支持一键设为桌面。", "Daily featured Bing wallpapers with one-click desktop application.");
    public static string BlurbStopwatch => T("轻量级独立秒表，支持暂停与重置。", "Lightweight standalone stopwatch with pause and reset.");
    public static string BlurbCountdown => T("支持自定义预设的倒计时器，归零时发出系统提醒。", "Countdown timer with customizable presets and zero-point alerts.");
    public static string BlurbAlarm => T("设定目标时刻与闹钟，到达预设时间发出提示音与系统通知。", "Set scheduled alarms for a specific time, with audio cues and system notifications.");
    public static string BlurbFocusTimer => T("专注工作与休息交替的番茄钟，专注状态本地管理。", "Local Pomodoro timer for focus and break cycles without altering system focus state.");
    public static string BlurbTimeProgress => T("直观呈现今天、本月或今年的时间流逝进度。", "Visual progress bars tracking the passage of the day, month, or year.");
    public static string BlurbWeather => T("实时天气、气温与逐小时预报，支持自动定位与多城市。", "Real-time weather, temperature, and hourly forecast with auto-location.");
    public static string BlurbSchedule => T("今日工作生活待办日程与重要纪念日/倒数日。", "Today's agenda, to-do schedule, and milestone countdowns.");

    public static string WeatherClear => T("晴", "Clear");
    public static string WeatherPartlyCloudy => T("多云", "Partly Cloudy");
    public static string WeatherOvercast => T("阴", "Overcast");
    public static string WeatherFog => T("雾", "Fog");
    public static string WeatherDrizzle => T("毛毛雨", "Drizzle");
    public static string WeatherRain => T("雨", "Rain");
    public static string WeatherSnow => T("雪", "Snow");
    public static string WeatherThunderstorm => T("雷暴", "Thunderstorm");
    public static string WeatherWindy => T("大风", "Windy");
    public static string WeatherUnknown => T("未知", "Unknown");
    public static string WeatherFeelsLike => T("体感", "Feels like");
    public static string WeatherHumidity => T("湿度", "Humidity");
    public static string WeatherWind => T("风速", "Wind");
    public static string WeatherAqi => T("空气质量", "AQI");
    public static string WeatherHourly => T("逐小时预报", "Hourly Forecast");
    public static string WeatherDaily => T("多日预报", "Daily Forecast");
    public static string WeatherCityAuto => T("自动定位", "Auto Location");
    public static string WeatherChangeCity => T("更改城市", "Change City");
    public static string WeatherEnterCity => T("输入城市名称（如 北京 / Shanghai）", "Enter city name (e.g. Beijing)");
    public static string WeatherPrecipitation => T("降水概率", "Precipitation");
    public static string Weather7Days => T("7天预报", "7-Day Forecast");
    public static string WeatherNow => T("现在", "Now");
    public static string WeatherOffline => T("离线缓存", "Offline");
    public static string WeatherRefreshing => T("正在更新…", "Updating…");
    public static string WeatherNeedCity => T("选择城市", "Choose a city");
    public static string WeatherCityNotFound => T("找不到该城市", "City not found");
    public static string WeatherLocateFailed => T("定位失败", "Location failed");
    public static string WeatherNetworkError => T("天气暂时不可用", "Weather unavailable");
    public static string Save => T("保存", "Save");
    public static string Refresh => T("刷新", "Refresh");
    public static string AqiGood => T("优", "Good");
    public static string AqiModerate => T("良", "Moderate");
    public static string AqiUnhealthySensitive => T("轻度", "Sensitive");
    public static string AqiUnhealthy => T("中度", "Unhealthy");
    public static string AqiVeryUnhealthy => T("重度", "Very Unhealthy");
    public static string AqiHazardous => T("严重", "Hazardous");

    public static string ScheduleAllDay => T("全天", "All day");
    public static string ScheduleTodayIsDay => T("就是今天！", "Today!");
    public static string ScheduleDaysRemainingFormat => T("还有 {0} 天", "{0} days left");
    public static string ScheduleDaysPassedFormat => T("已过 {0} 天", "{0} days ago");
    public static string ScheduleNoEvents => T("今日无日程", "No events today");
    public static string ScheduleRelax => T("享受轻松时光", "Enjoy your day");
    public static string ScheduleTabEvents => T("今日日程", "Today's Schedule");
    public static string ScheduleTabCountdowns => T("重要倒数日", "Countdowns");
    public static string ScheduleAddEventPlaceholder => T("添加日程（如 14:30 方案评审）", "Add event (e.g. 14:30 Meeting)");
    public static string ScheduleAddCountdownPlaceholder => T("倒数日名称", "Countdown title");
    public static string ScheduleAdd => T("添加", "Add");
    public static string ScheduleEmptyEvents => T("今日暂无日程安排，在下方输入回车即可添加", "No events today. Type below and press Enter to add.");
    public static string ScheduleEmptyCountdowns => T("暂无重要倒数日，在下方输入即可添加", "No countdowns yet. Add one below.");
    public static string ScheduleFull => T("日程已满 100 条，请先删除过期项后再添加。", "Schedule is full (100 items). Remove an older item before adding another.");
    public static string ScheduleOverdue => T("过期", "Overdue");
    public static string ConfirmRemoveSchedule => T("从 Dock 移除日程模块？已保存的日程仍会留在设置里，可再次添加模块找回。", "Remove Schedule from the Dock? Saved items stay in settings and come back if you add the widget again.");

    public static string CategoryWork => T("工作", "Work");
    public static string CategoryPersonal => T("个人", "Personal");
    public static string CategoryTeam => T("团队", "Team");
    public static string CategoryImportant => T("重要", "Important");
    public static string CategoryUrgent => T("紧急", "Urgent");
    public static string CategoryStudy => T("学习", "Study");

    public static string ScheduleCalendars => T("分类…", "Calendars…");
    public static string ScheduleCustomCategories => T("自定义分类名称", "Custom Categories");
    public static string ScheduleColorDesc => T("系统提供 6 种预设颜色，可为每个颜色设置专属分类名称", "System provides 6 preset colors. Set a custom title for each.");
    public static string ScheduleResetDefaults => T("恢复默认", "Reset Defaults");
    public static string ScheduleDone => T("完成", "Done");
    public static string ScheduleBack => T("‹ 返回", "‹ Back");
    public static string ScheduleAllDaySection => T("全天", "All day");
    public static string ScheduleTodayHeader => T("今日", "Today");

    public static string CityShanghai => T("上海", "Shanghai");
    public static string CityTaipei => T("台北", "Taipei");
    public static string CityTokyo => T("东京", "Tokyo");
    public static string CitySeoul => T("首尔", "Seoul");
    public static string CitySingapore => T("新加坡", "Singapore");
    public static string CityLondon => T("伦敦", "London");
    public static string CityLosAngeles => T("洛杉矶", "Los Angeles");
    public static string CityNewYork => T("纽约", "New York");
}
