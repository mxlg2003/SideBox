using System.Globalization;

namespace WinDock.Core;

/// <summary>User-facing zh/en copy. Widget format classes keep their own specialized strings.</summary>
public static class UiText
{
    public static string T(string zh, string en) => UiLocale.T(zh, en);

    public static string Settings => T("设置", "Settings");
    public static string Exit => T("退出", "Exit");
    public static string ExitWinDock => T($"退出 {ProductInfo.Name}", $"Exit {ProductInfo.Name}");
    public static string CloseWindow => T("关闭窗口", "Close window");
    public static string CloseAllWindows => T("关闭所有窗口", "Close all windows");
    public static string ConfirmCloseAllWindows(int count) => T(
        $"关闭这 {count} 个窗口？有的程序会直接退出，不一定先询问保存。",
        $"Close these {count} windows? Some apps quit immediately and may not ask you to save.");
    public static string BackgroundRunningNotice => T($"设置已保存。{ProductInfo.Name} 仍在屏幕边缘运行，可在 Dock 或系统托盘右键退出。", $"Settings saved. {ProductInfo.Name} remains active at the screen edge; right-click the Dock or tray icon to exit.");
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

    public static string NavGroupBasic => T("基础设置", "Basic Settings");
    public static string NavGroupCore => T("核心功能", "Core Features");
    public static string NavGroupSystemAndSupport => T("系统与支持", "System & Support");
    public static string NavGeneral => T("常规设置", "General");
    public static string NavLayout => T("停靠与布局", "Dock & Layout");
    public static string NavAppearance => T("外观与材质", "Appearance");
    public static string NavModules => T("模块广场", "Widgets");
    public static string NavProfiles => T("情景模式", "Profiles");
    public static string NavHotkeys => T("快捷键中心", "Shortcuts");
    public static string NavPrivacy => T("数据与权限", "Data & Privacy");
    public static string NavAbout => T("关于与社区", "About & Community");
    public static string General => T("常规", "General");
    public static string GeneralIntro => T("配置开机启动、界面语言、托盘行为以及配置备份与还原。", "Configure startup, interface language, tray behavior, and settings backup.");
    public static string BackupSection => T("配置管理与备份", "Settings Backup & Management");
    public static string BackupHelp => T("导出或导入所有情景、模块排版与快捷键配置，便于多设备同步与还原。", "Export or import all profiles, widgets, and shortcuts for multi-device sync and restore.");
    public static string ExportSettings => T("导出配置", "Export Settings");
    public static string ImportSettings => T("导入配置", "Import Settings");
    public static string ExportSuccess => T("配置已成功导出。", "Settings exported successfully.");
    public static string ImportSuccess => T("配置已成功导入。", "Settings imported successfully.");
    public static string ImportFailed => T("导入配置文件失败，文件格式可能不正确。", "Failed to import settings. The file format may be invalid.");
    public static string ResetFactory => T("恢复出厂默认值", "Reset to Defaults");
    public static string ResetFactoryConfirm => T("确定要恢复出厂设置吗？已钉选的项目和自定义外观将被重置。", "Are you sure you want to reset to factory defaults? Pinned items and custom appearance will be reset.");
    public static string Hotkeys => T("快捷键中心", "Shortcuts");
    public static string HotkeysIntro => T("集中配置全局呼出、情景轮换与撤销快捷键。", "Configure global reveal, profile cycling, and undo shortcuts.");
    public static string NextProfileShortcut => T("轮换下一个情景", "Next Profile");
    public static string PrevProfileShortcut => T("轮换上一个情景", "Previous Profile");
    public static string UndoProfileShortcut => T("撤销情景切换", "Undo Profile Switch");
    public static string ActiveWidgetsTitle => T("当前 Dock 上的模块", "Mounted Widgets");
    public static string ActiveWidgetsHelp => T("在实际 Dock 上可直接右键配置详细参数或按住拖拽调整顺序。", "Right-click or drag on the actual Dock to customize or reorder widgets.");
    public static string OpenFolder => T("在资源管理器中打开", "Open in File Explorer");
    public static string OpenLogsFolder => T("打开日志目录", "Open Logs Folder");
    public static string OpenConfigFolder => T("打开配置目录", "Open Config Folder");
    public static string CommunitySection => T("开源社区与支持", "Open Source & Support");
    public static string GitHubRepo => T("GitHub 开源仓库", "GitHub Repository");
    public static string OfficialWebsite => T("官方网站", "Official Website");
    public static string LicenseFree => T("永久免费开源 · 纯净无广告", "Forever Free & Open Source · No Ads");
    public static string ConfigPathTitle => T("配置文件路径", "Settings File");
    public static string LogsPathTitle => T("运行日志路径", "Logs Directory");
    public static string SystemAndStartup => T("系统与启动", "System & Startup");

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
    public static string CatalogBusiness => T("商业", "Business");
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
    public static string PeekHelp => T("鼠标指针移入屏幕边缘大约 8 像素的热区时自动滑出 Dock。热区很窄。", "Move the pointer into the edge trigger zone, about 8 pixels, to reveal the Dock. The zone is narrow.");
    public static string RevealHotkey => T("全局唤醒快捷键", "Global reveal shortcut");
    public static string RevealHotkeyHelp => T("开启后可通过全局快捷键呼出处于隐藏状态的 Dock。若快捷键被占用，可在下方重新录制。", "Press a global shortcut to reveal the hidden Dock. Change the shortcut below if it conflicts with another app.");
    public static string RevealHotkeyName => T("唤醒快捷键", "Reveal shortcut");
    public static string CollapseApps => T("程序折叠", "Collapse apps");
    public static string CollapseAppsHelp => T("Dock 上仅显示一个聚合程序图标，点击可展开全部应用程序与快捷方式。", "Show a single consolidated apps icon on the Dock; click to expand all apps and shortcuts.");
    public static string CollapseFolders => T("文件夹折叠", "Collapse folders");
    public static string CollapseFoldersHelp => T("Dock 上仅显示一个聚合文件夹图标，点击可展开全部已钉选的文件夹。", "Show a single consolidated folder icon on the Dock; click to expand all pinned folders.");
    public static string LayoutFooter => T(
        $"{ProductInfo.Name} 默认为贴边居中的悬浮岛，在全屏应用或演示模式下会自动避让。打开自动隐藏后，岛收成大约 4 像素的细条，鼠标要碰到大约 8 像素的热区才会滑出。可拖动两端调整宽度，双击边缘可恢复根据内容自适应。在多显示器间拖拽 Dock 会自动同步此处的显示器设置。内容超出屏幕范围时边缘将显示翻页箭头。",
        $"{ProductInfo.Name} floats centered along the chosen screen edge and automatically yields for fullscreen apps or presentations. With auto-hide on, the island tucks to about 4 pixels and only slides out when the pointer hits a hot zone about 8 pixels wide. Drag the ends to adjust width, or double-click an edge to fit content. Dragging the Dock across screens updates the display setting. Overflow arrows appear when content exceeds available space.");

    public static string PrivacyIntro => T("所有权限默认处于关闭状态。关闭权限后对应模块仍保留在 Dock 上，但不再读取相关数据。", "All permissions are disabled by default. Disabling a permission keeps widgets on the Dock, but stops data access.");
    public static string MediaPrivacy => T("允许读取正在播放的标题和封面", "Allow reading Now Playing title and artwork");
    public static string NetworkPrivacy => T("允许显示网络适配器名称和速率", "Allow showing adapter names and rates");
    public static string NetworkPrivacyHelp => T("数据仅在本地处理，绝不上传网络。", "Data is processed locally on this PC; nothing is uploaded.");
    public static string ProcessNamesPrivacy => T("允许列出进程名称与占用", "Allow listing process names and usage");
    public static string ProcessNamesPrivacyHelp => T("仅在本机采样 CPU 与内存占用较高的进程，不会上传。", "Samples top CPU and memory processes on this PC only. Nothing is uploaded.");
    public static string BingPrivacy => T("允许从 bing.com 下载壁纸图", "Allow downloading wallpapers from bing.com");
    public static string BingPrivacyHelp => T("将其设为桌面壁纸时会二次确认。", "Confirmation is requested before setting as desktop wallpaper.");
    public static string LocationPrivacy => T("允许使用大致位置自动填写天气城市", "Allow approximate location to fill the weather city");
    public static string LocationPrivacyHelp => T("仅在你点击「自动定位」后，根据出口 IP 通过 HTTPS 向 geojs.io 估计大致城市，不是 GPS。可随时关闭；关闭后只使用已填写的城市。", "Used only after you tap Auto Location. Estimates a city from your public IP over HTTPS via geojs.io. This is not GPS. You can turn this off anytime; the typed city is kept.");
    public static string PrivacyFooter => T("未启用崩溃数据上报，所有诊断日志仅保存在本地设备。", "Crash reporting is disabled. All diagnostic logs remain strictly on this PC.");
    public static string MediaHintOff => T("关闭后模块仍保留在 Dock 上，但不再读取歌曲名和专辑封面。", "The widget remains on the Dock; it simply stops reading track titles and artwork.");
    public static string MediaHintGrant(string profile) => T(
        $"开启后仅对当前情景「{profile}」授权。关闭后模块仍保留在 Dock 上，但不再读取歌曲名和专辑封面。",
        $"Turning this on grants permission to profile “{profile}” only. The widget remains on the Dock if disabled, but stops reading track titles and artwork.");
    public static string MediaHintGranted(string names) => T(
        $"已授权：{names}。关闭后模块仍保留在 Dock 上，但不再读取歌曲名和专辑封面。",
        $"Granted: {names}. The widget remains on the Dock if you turn this off; it simply stops reading track titles and artwork.");

    public static string AboutIntro => T(
        $"{ProductInfo.Name} 是一款贴在屏幕边缘的悬浮岛，用来钉程序，并查看时钟、便笺等模块。不替换 Windows 任务栏，不以管理员权限运行。",
        $"{ProductInfo.Name} is an edge island for pinned apps and widgets such as the clock and notes. It does not replace the Windows taskbar and never requires administrator privileges.");
    public static string AboutConfig(string settings, string logs) => T($"配置：{settings}{Environment.NewLine}日志：{logs}", $"Settings: {settings}{Environment.NewLine}Logs: {logs}");
    public static string RunAtStartup => T("开机启动", "Start with Windows");
    public static string RunAtStartupHelp => T("仅对当前 Windows 用户生效，不影响其他用户。", "Applies to the current Windows user only.");
    public static string CheckForUpdates => T("检查更新", "Check for updates");
    public static string CheckingForUpdates => T("正在检查更新...", "Checking for updates...");
    public static string UpToDate => T("当前已是最新版本", "You are up to date.");
    public static string UpdateAvailable(string version) => T($"发现新版本 v{version}", $"Version v{version} is available");
    public static string UpdateNow => T("立即更新", "Update now");
    public static string DownloadingUpdate(int pct) => T($"正在下载更新 ({pct}%)...", $"Downloading update ({pct}%)...");
    public static string UpdateDownloadFailed => T("下载更新失败，请重试或在浏览器中下载。", "Failed to download update. Please retry or download via browser.");
    public static string UpdateCheckFailed => T("检查更新失败，请检查网络。", "Failed to check for updates. Please check your connection.");
    public static string UpdateChangelog => T("更新日志", "Changelog");
    public static string UpdateHashMismatch => T("安装包校验失败，文件可能已损坏。", "Package verification failed. File may be corrupted.");
    public static string ViewOnGitHub => T("查看 Release", "View on GitHub");
    public static string DownloadInBrowser => T("在浏览器中下载", "Download in browser");
    public static string UpdateRateLimited => T("GitHub 访问过于频繁，请稍后再试或前往网页下载。", "GitHub rate limit reached. Please try again later or view on GitHub.");
    public static string UpdateDownloadFailedWithFallback => T("下载失败，请检查网络，或点击右侧按钮在浏览器中下载。", "Download failed. Please check your connection or download via browser.");

    public static string ProfilesIntro => T("复制情景将完整保留所有钉选启动项与模块配置。可通过本页下拉菜单或系统托盘右键菜单进行情景切换。", "Duplicating a profile creates a full copy of its pinned items and widget configurations. Switch profiles via this dropdown or the system tray menu.");
    public static string CurrentProfile => T("当前情景", "Current profile");
    public static string DuplicateProfile => T("复制当前情景", "Duplicate profile");
    public static string Rename => T("重命名", "Rename");
    public static string Delete => T("删除", "Delete");
    public static string RecentSwitches => T("最近切换", "Recent switches");
    public static string ProfileModulesTitle => T("包含的模块", "Widgets");
    public static string ProfileCurrentMark => T("当前", "Current");
    public static string ProfileNoModules => T("这个情景还没有模块。", "This profile has no widgets.");
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
        "模块中的「专注计时器」为本地独立番茄钟，不会联动触发 Windows 专注助手，亦不会自动切换情景。出厂的「深度工作」写了系统专注触发，但自动切换目前关闭，打开 Windows 专注不会换成这个情景。",
        "The Focus Timer widget is a local standalone pomodoro clock. It does not activate Windows Focus or trigger profile switches. The factory Deep Work profile includes a Focus trigger, but automatic switching is off, so Windows Focus will not select it.");
    public static string CopySuffix => T(" 的副本", " copy");
    public static string ProfileDaily => T("日常", "Daily");
    public static string ProfileDeepWork => T("深度工作", "Deep Work");
    public static string ProfilesSection => T("情景", "Profiles");
    public static string ProfileNow(string name) => T($"当前情景：{name}", $"Profile: {name}");

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
    public static string ChosenApps(int count) => T($"已选择 · {count}", $"Selected · {count}");
    public static string ChosenEmpty => T("从左侧加入程序", "Add apps from the left");
    public static string MoveToChosen => T("加入已选择", "Add to selected");
    public static string MoveToAvailable => T("移出", "Remove");
    public static string AddSelected(int count) => count <= 0
        ? Add
        : T($"添加 {count} 项", $"Add {count}");
    public static string FileFilter => T("快捷方式与程序|*.lnk;*.exe|所有文件|*.*", "Shortcuts and apps|*.lnk;*.exe|All files|*.*");
    public static string OpenWidget => T("打开模块", "Open widget");
    public static string Compact => T("紧凑", "Compact");
    public static string Expanded => T("展开", "Expanded");
    public static string HideNotes => T("从 Dock 拿下", "Take off the Dock");
    public static string ConfirmDeleteBlankNote => T("这张便笺是空的，从 Dock 拿下会直接删除。确定删除？", "This note is empty. Taking it off the Dock deletes it. Delete it?");
    public static string ConfirmDeleteHiddenNote => T("永久删除这张历史便笺？此操作无法撤销。", "Delete this note from history permanently? This cannot be undone.");
    public static string ConfirmRemoveFromDock(string name) => T($"从 Dock 移除「{name}」？", $"Remove “{name}” from the Dock?");
    public static string RemoveFromDock => T("从 Dock 移除", "Remove from Dock");
    public static string OpenNew => T("在新窗口中打开", "Open in new window");
    public static string SwitchExisting => T("左键激活已有窗口", "Left-click switches to existing window");
    public static string StayOpen => T("锁定保持展开", "Pin open");
    public static string RunAsAdmin => T("以管理员身份运行", "Run as administrator");
    public static string LocateInExplorer => T("在资源管理器中定位", "Locate in File Explorer");
    public static string CopyPath => T("复制路径", "Copy path");
    public static string Drawer => T("快捷抽屉", "Quick Drawer");
    public static string SearchSettings => T("搜索设置…", "Search settings…");
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
    public static string AlreadyRunning => T($"{ProductInfo.Name} 已经在运行。", $"{ProductInfo.Name} is already running.");
    public static string LoggedToFile => T("已记录到日志。", "Logged.");
    public static string SafeModeTray => T($"{ProductInfo.Name} — 安全模式", $"{ProductInfo.Name} — Safe mode");
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

    public static string ConsentNowPlaying => T("将读取正在播放的歌曲名、封面和播放器名称。可以在设置的隐私页随时关闭。", $"{ProductInfo.Name} will read the song title, artwork, and player name. You can turn this off later in Privacy.");
    public static string ConsentNetwork => T("将显示网络适配器名称和上下行速率。数据只留在本机，不会上传。", $"{ProductInfo.Name} will show adapter names and up/down rates. Data stays on this PC.");
    public static string ConsentProcesses => T("将列出占用较高的进程名称及其 CPU、内存占用。数据只留在本机。可在设置的隐私页随时关闭。", $"{ProductInfo.Name} will list high-usage process names with CPU and memory. Data stays on this PC. You can turn this off later in Privacy.");
    public static string ConsentBing => T("将访问 bing.com 下载壁纸图。设为桌面时会再确认一次。", $"{ProductInfo.Name} will visit bing.com to download wallpapers. Setting as desktop wallpaper asks again.");
    public static string ConsentWeather => T($"将访问 Open-Meteo 获取天气预报（不经过 {ProductInfo.Name} 服务器），大约每 30 分钟刷新。默认只使用你填写的城市。", $"{ProductInfo.Name} will request forecasts from Open-Meteo (not via a {ProductInfo.Name} server), about every 30 minutes. It uses the city you type unless you opt in to auto-location.");
    public static string ConsentLocation => T("将根据出口 IP，通过 HTTPS 向 geojs.io 估计大致城市，用于天气自动定位。这不是 GPS。可以在设置的隐私页随时关闭。", $"{ProductInfo.Name} will estimate an approximate city from your public IP over HTTPS via geojs.io. This is not GPS. You can turn this off later in Privacy.");
    public static string AddWidgetTitle(string name) => T($"添加{name}", $"Add {name}");
    public static string ApplyBingMessage => T("将从 bing.com 下载高清壁纸并设为当前桌面壁纸。", $"{ProductInfo.Name} will visit bing.com to download the image and set it as your desktop wallpaper.");
    public static string TodayBing => T("今日 Bing 壁纸", "Today’s Bing wallpaper");
    public static string BingApplied => T("已设为桌面壁纸。", "Set as desktop wallpaper.");
    public static string BingApplyFailed => T("应用 Bing 壁纸失败。", "Failed to apply the Bing wallpaper.");
    public static string RestorePreviousWallpaper => T("恢复上一张壁纸", "Restore previous wallpaper");
    public static string WallpaperRestored => T("已恢复上一张桌面壁纸。", "Restored the previous desktop wallpaper.");
    public static string WallpaperRestoreFailed => T("无法恢复上一张壁纸。", "Could not restore the previous wallpaper.");
    public static string ApplyToDesktop => T("设为桌面壁纸", "Set as desktop wallpaper");
    public static string CurrentDesktop => T("当前桌面壁纸", "Current desktop wallpaper");
    public static string ViewOriginalImage => T("在浏览器中查看原图", "View original in browser");
    public static string ViewOriginalShort => T("查看原图", "Original");
    public static string ApplyingWallpaper => T("正在应用…", "Applying…");
    public static string BingAutoDaily => T("每天自动切换桌面壁纸", "Auto-switch desktop wallpaper daily");
    public static string BingAutoDailyHint => T("每天推出新壁纸时，自动将其设为桌面壁纸", "Automatically set new daily Bing wallpaper as desktop wallpaper");

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
    public static string NotesAlertTitle => T($"{ProductInfo.Name} 便笺", $"{ProductInfo.Name} Notes");
    public static string NotesWriteSomething => T("写点什么…", "Write something…");
    public static string Memory => T("内存", "Memory");
    public static string MemoryShort => T("内存", "MEM");
    public static string MemoryUsed => T("已用内存", "Used");
    public static string PerCore => T("每核占用", "Per-core usage");
    public static string CpuFrequency => T("当前主频", "Clock speed");
    public static string BatteryWatts => T("充放功率", "Charge rate");
    public static string BatteryRemaining => T("剩余时间", "Time remaining");
    public static string BatteryHealth => T("电池健康", "Battery health");
    public static string WifiSignal => T("Wi-Fi 信号", "Wi-Fi signal");
    public static string DiskFree => T("剩余空间", "Free space");
    public static string DiskUsedPercent => T("已用百分比", "Used percent");
    public static string GpuVram => T("专用显存", "Dedicated VRAM");
    public static string GpuEngines => T("引擎拆分", "Engine breakdown");
    public static string GpuUnsupported => T("此电脑不支持 GPU 计数器", "GPU counters are not available on this PC");
    public static string GpuUnavailable => T("不可用", "Unavailable");
    public static string GpuAdapter => T("适配器", "Adapter");
    public static string GpuEngine3D => T("3D", "3D");
    public static string GpuEngineCopy => T("拷贝", "Copy");
    public static string GpuEngineVideo => T("视频", "Video");
    public static string GpuEngineCompute => T("计算", "Compute");
    public static string ProcessSort => T("排序", "Sort");
    public static string ProcessNamesOff => T("已关闭", "Turned off");
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
    public static string WidgetWallpaper => T("壁纸", "Wallpaper");
    public static string WidgetBing => T("Bing 壁纸", "Bing Wallpaper");
    public static string WidgetStopwatch => T("秒表", "Stopwatch");
    public static string WidgetCountdown => T("倒计时", "Countdown");
    public static string WidgetAlarm => T("闹钟", "Alarm");
    public static string WidgetFocusTimer => T("专注计时器", "Focus Timer");
    public static string WidgetTimeProgress => T("时间进度", "Time Progress");
    public static string WidgetWeather => T("天气", "Weather");
    public static string WidgetSchedule => T("日程倒数", "Schedule");
    public static string WidgetDisk => T("磁盘", "Disk");
    public static string WidgetGpu => T("GPU", "GPU");
    public static string WidgetProcesses => T("进程", "Processes");
    public static string WidgetStocks => T("股票", "Stocks");
    public static string WidgetWhiteNoise => T("白噪音", "White Noise");

    public static string BlurbClock => T("显示本地时间，点击可展开大时钟与世界时区。", "Displays local time. Click to open a full clock and world time zones.");
    public static string BlurbNotes => T("常驻便笺小组件，点击即可快速查看与编辑。", "Sticky note on the Dock. Click to view and edit instantly.");
    public static string BlurbSysActivity => T("实时监控 CPU 与内存占用状态。", "Real-time CPU and memory monitoring.");
    public static string BlurbNetwork => T("实时监控网络上传与下载速率。", "Real-time network upload and download throughput.");
    public static string BlurbPower => T("查看本机电池及已连接蓝牙外设的电量。", "View battery status for this PC and connected Bluetooth accessories.");
    public static string BlurbNowPlaying => T("显示当前播放曲目信息、专辑封面及控制按钮。", "Displays now playing track information, album artwork, and playback controls.");
    public static string BlurbWallpaper => T("多源高清壁纸中心：内置 Unsplash 精选图库、关键词搜索与本地相册目录轮巡，支持一键与自动设为桌面。", "Multi-source wallpaper hub: built-in Unsplash curated gallery, keyword search, and local directory rotation with auto-desktop sync.");
    public static string BlurbBing => T("每日同步 Bing 精选壁纸，支持一键设为桌面。", "Daily featured Bing wallpapers with one-click desktop application.");

    public static string WallpaperHubTitle => T("壁纸中心", "Wallpaper Hub");
    public static string WallpaperSourceCurated => T("Unsplash 精选", "Unsplash Picks");
    public static string WallpaperSourceLocal => T("本地相册", "Local Folders");
    public static string WallpaperSourceUnsplash => T("Unsplash 搜索", "Unsplash Search");
    public static string WallpaperSourceBing => T("Bing 每日", "Bing Daily");
    public static string WallpaperNext => T("换下一张", "Next Wallpaper");
    public static string WallpaperPrev => T("上一张", "Previous");
    public static string WallpaperApplyDesktop => T("设为桌面壁纸", "Set as Desktop");
    public static string WallpaperRestoreOriginal => T("恢复初始壁纸", "Restore Original");
    public static string WallpaperAutoApply => T("自动设为桌面壁纸", "Auto Set Desktop");
    public static string WallpaperAutoApplyHint => T("换新壁纸时自动同步为 Windows 桌面", "Automatically update Windows desktop when wallpaper changes");
    public static string WallpaperRotationLabel => T("更换频次", "Rotation");
    public static string WallpaperRotationDaily => T("每天一张", "Once a day");
    public static string WallpaperRotationStartup => T("每次启动", "Every startup");
    public static string WallpaperRotation15Min => T("每 15 分钟", "Every 15 min");
    public static string WallpaperRotation30Min => T("每 30 分钟", "Every 30 min");
    public static string WallpaperRotation1Hour => T("每 1 小时", "Every 1 hour");
    public static string WallpaperRotation4Hours => T("每 4 小时", "Every 4 hours");
    public static string WallpaperRotationManual => T("手动更换", "Manual only");
    public static string WallpaperChooseFolder => T("选择图片文件夹", "Choose Folder");
    public static string WallpaperRecursive => T("包含子文件夹", "Include subfolders");
    public static string WallpaperUnsplashKey => T("Unsplash Access Key", "Unsplash Access Key");
    public static string WallpaperUnsplashQuery => T("搜索关键词", "Keywords");
    public static string WallpaperUnsplashQueryHint => T("例如：mountain, nature, minimal", "e.g. mountain, nature, minimal");
    public static string WallpaperViewSource => T("查看原图与摄影师", "View photo & author");
    public static string WallpaperEmptyFolder => T("文件夹内未找到图片", "No images found in folder");
    public static string WallpaperNoKey => T("未设置 Unsplash Key，已回退至精选图库", "No Access Key; falling back to curated gallery");
    public static string WallpaperSelectFolder => T("请选择图片文件夹…", "Select a folder with images…");
    public static string WallpaperApplied => T("已设为系统桌面壁纸", "Desktop wallpaper updated");
    public static string WallpaperApplyFailed => T("设为桌面壁纸失败", "Failed to update desktop wallpaper");
    public static string BlurbStopwatch => T("轻量级独立秒表，支持暂停与重置。", "Lightweight standalone stopwatch with pause and reset.");
    public static string BlurbCountdown => T("支持自定义预设的倒计时器，归零时发出系统提醒。", "Countdown timer with customizable presets and zero-point alerts.");
    public static string BlurbAlarm => T("设定目标时刻。到点播放提示音并显示托盘通知。需要本程序保持运行；关掉后不会响。当前情景里没有这块闹钟时也会响。", $"Set a time. At that time it plays a sound and shows a tray notification. {ProductInfo.Name} must stay running, and it still rings when this alarm is on another profile. It does not ring after you quit.");
    public static string BlurbFocusTimer => T("专注工作与休息交替的番茄钟，专注状态本地管理。", "Local Pomodoro timer for focus and break cycles without altering system focus state.");
    public static string BlurbTimeProgress => T("直观呈现今天、本月或今年的时间流逝进度。", "Visual progress bars tracking the passage of the day, month, or year.");
    public static string BlurbWeather => T("Open-Meteo 预报与气温，大约每 30 分钟刷新。可添加多份并分别设置城市；自动定位按 IP 估计城市。", "Open-Meteo forecast and temperature, refreshed about every 30 minutes. Add more than one and set a different city on each. Auto-location estimates the city from your IP.");
    public static string BlurbSchedule => T("本机手写的今日事项与倒数日，不同步 Outlook 或 Windows 日历。", "A local list of today's items and countdowns. It does not sync with Outlook or the Windows calendar.");
    public static string BlurbDisk => T("查看固定磁盘剩余空间与已用容量。", "See free space and used capacity on fixed disks.");
    public static string BlurbGpu => T("查看 GPU 占用，可选显存与引擎拆分。", "See GPU utilization, with optional VRAM and engine breakdown.");
    public static string BlurbProcesses => T("列出占用最高的进程。首次添加时会征求同意。", "Lists the processes using the most CPU or memory. Adding it asks for consent.");
    public static string BlurbStocks => T("盯一只股票的现价、分时和涨跌。数据来自第三方，非官方，可能延迟，不是投资建议。每添加一份模块对应一只股票。", "Follow one symbol's price, intraday chart, and change. Quotes come from a third party, are unofficial, may be delayed, and are not investment advice. Each copy tracks a single stock.");
    public static string BlurbWhiteNoise => T("沉浸式专注音效与多轨自然混音，可与专注计时器联动。", "Immersive focus soundscapes and multi-track ambient mixer, linkable with the Focus Timer.");

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
    public static string WeatherPrevHours => T("向前查看（可拖拽或滚轮）", "Previous hours");
    public static string WeatherNextHours => T("向后查看（可拖拽或滚轮）", "Next hours");
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
    public static string WeatherNoCity => T("未选城市", "No city");
    public static string WeatherUnavailableShort => T("暂时不可用", "Unavailable");
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

    public static string ScheduleLayoutNext => T("下一项", "Next event");
    public static string ScheduleLayoutDateNext => T("日期 + 下一项", "Date + next event");
    public static string ScheduleLayoutAgenda => T("日期 + 日程", "Date + agenda");
    public static string ScheduleLayoutDate => T("日期", "Date");
    public static string ScheduleAllDay => T("全天", "All day");
    public static string ScheduleTodayIsDay => T("就是今天！", "Today!");
    public static string ScheduleDaysRemainingFormat => T("还有 {0} 天", "{0} days left");
    public static string ScheduleDaysPassedFormat => T("已过 {0} 天", "{0} days ago");
    public static string ScheduleNoEvents => T("今日无日程", "No events today");
    public static string ScheduleRelax => T("享受轻松时光", "Enjoy your day");
    public static string ScheduleTabEvents => T("今日日程", "Today's Schedule");
    public static string ScheduleTabCountdowns => T("重要倒数日", "Countdowns");
    public static string ScheduleAddEventPlaceholder => T("日程名称", "Event title");
    public static string ScheduleTime => T("时间", "Time");
    public static string ScheduleDate => T("日期", "Date");
    public static string ScheduleEditTime => T("修改时间", "Edit time");
    public static string ScheduleEditDate => T("修改日期", "Edit date");
    public static string ScheduleAddCountdownPlaceholder => T("倒数日名称", "Countdown title");
    public static string ScheduleAdd => T("添加", "Add");
    public static string ScheduleEmptyEvents => T("今日暂无日程。在下方选择时间并输入名称", "No events today. Pick a time and type a title below.");
    public static string ScheduleEmptyCountdowns => T("暂无重要倒数日，在下方输入即可添加", "No countdowns yet. Add one below.");
    public static string ScheduleFull => T("日程已满 100 条，请先删除过期项后再添加。", "Schedule is full (100 items). Remove an older item before adding another.");
    public static string ScheduleOverdue => T("过期", "Overdue");
    public static string ScheduleLater => T("以后", "Later");
    public static string ConfirmRemoveSchedule => T("从 Dock 移除日程模块？已保存的日程仍会留在设置里，可再次添加模块找回。", "Remove Schedule from the Dock? Saved items stay in settings and come back if you add the widget again.");

    public static string CategoryWork => T("工作", "Work");
    public static string CategoryPersonal => T("个人", "Personal");
    public static string CategoryTeam => T("团队", "Team");
    public static string CategoryImportant => T("重要", "Important");
    public static string CategoryUrgent => T("紧急", "Urgent");
    public static string CategoryStudy => T("学习", "Study");

    public static string ScheduleCalendars => T("分类…", "Categories…");
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

    public static string StockIslandSection => T("岛上", "On the Dock");
    public static string StockQuoteSection => T("行情", "Quote");
    public static string StockShowName => T("名称", "Name");
    public static string StockShowBadge => T("市场标记", "Market badge");
    public static string StockShowCode => T("代码", "Symbol");
    public static string StockShowBoard => T("板块", "Board");
    public static string StockShowPrice => T("现价", "Price");
    public static string StockShowSpark => T("分时", "Intraday");
    public static string StockShowChange => T("涨跌幅", "Change");
    public static string StockShowCurrency => T("货币", "Currency");
    public static string StockShowHint => T("操作提示", "Chart hint");
    public static string StockShowAsOf => T("截至时间", "As of");
    public static string StockShowPrevious => T("前收", "Previous close");
    public static string StockShowSample => T("采样说明", "Sample note");
    public static string StockChoose => T("选择股票", "Choose a symbol");
    public static string StockSearch => T("搜索代码或名称", "Search symbol or name");
    public static string StockNoMatch => T("没有匹配的股票", "No matching symbols");
    public static string StockBoardStar => T("科创", "STAR");
    public static string StockBoardChiNext => T("创业", "ChiNext");
    public static string StockBoardBse => T("北证", "BSE");
    public static string StockBoardSme => T("中小", "SME");
    public static string StockHoverHint => T("悬停查看价格 · 拖动对比", "Hover for price · Drag to compare");

    public static string StockPreviousClose(string price) => T($"前收 {price}", $"Previous close: {price}");

    public static string StockSample(StockRange range) => range switch
    {
        StockRange.Day => T("非官方，可能延迟 · 每 5 分钟", "Unofficial, may be delayed · Every 5 minutes."),
        StockRange.FiveDay => T("非官方，可能延迟 · 每 30 分钟", "Unofficial, may be delayed · Every 30 minutes."),
        StockRange.FiveYear => T("非官方，可能延迟 · 每周", "Unofficial, may be delayed · Weekly."),
        _ => T("非官方，可能延迟 · 每日", "Unofficial, may be delayed · Daily.")
    };

    public static string StockStaleMark => T("上次", "last");
    public static string StockStaleQuote => T("上次成功的报价，可能已过时", "Last successful quote; it may be out of date");

    public static string StockAsOf(DateTime time)
    {
        var prefix = T("截至 ", "As of ");
        var formatted = UiLocale.IsChinese || UiLocale.IsChineseCulture(CultureInfo.CurrentCulture)
            ? time.ToString("yyyy年M月d日 HH:mm", CultureInfo.CurrentCulture)
            : time.ToString("MMM d, yyyy HH:mm", CultureInfo.CurrentCulture);
        return prefix + formatted;
    }

    public static string SoundRain => T("细雨", "Rain");
    public static string SoundThunder => T("远雷", "Thunder");
    public static string SoundWaves => T("海浪", "Waves");
    public static string SoundCampfire => T("篝火", "Campfire");
    public static string SoundWind => T("清风", "Wind");
    public static string SoundWhiteNoise => T("白噪音", "White Noise");
    public static string SoundPinkNoise => T("粉红噪音", "Pink Noise");
    public static string SoundBrownNoise => T("褐噪音", "Brown Noise");
    public static string SoundCafe => T("咖啡馆", "Coffee Shop");
    public static string SoundKeyboard => T("机械键盘", "Keyboard");
    public static string SoundClock => T("钟摆", "Clock");

    public static string PresetRainyStudy => T("雨夜书房", "Rainy Study");
    public static string PresetDeepForest => T("森林漫步", "Forest Walk");
    public static string PresetWarmFire => T("炉火暖冬", "Cozy Fireplace");
    public static string PresetOceanBreeze => T("潮汐海风", "Ocean Breeze");
    public static string PresetPureFocus => T("纯白噪音", "Pure White");
    public static string PresetDeepBrown => T("沉浸褐噪", "Deep Brown");
    public static string PresetCoffeeShop => T("街角咖啡", "Corner Cafe");
    public static string WhiteNoiseCustom => T("自定义", "Custom");

    public static string WhiteNoiseMasterVolume => T("主音量", "Master Volume");
    public static string WhiteNoisePresetsSection => T("场景预设", "Soundscapes");
    public static string WhiteNoiseMixerSection => T("音效混音台", "Sound Mixer");
    public static string WhiteNoiseSleepTimerSection => T("定时关闭", "Sleep Timer");
    public static string WhiteNoiseTimerOff => T("不开启", "Off");
    public static string WhiteNoiseTimerMin(int minutes) => T($"{minutes} 分钟", $"{minutes} min");
    public static string WhiteNoiseLinkFocus => T("随专注计时器自动起停", "Link with Focus Timer");
    public static string WhiteNoiseFade => T("平滑淡入淡出", "Smooth fade in/out");
    public static string WhiteNoisePlaying => T("播放中", "Playing");
    public static string WhiteNoisePaused => T("已暂停", "Paused");
    public static string WhiteNoiseStopped => T("未播放", "Ready");
}

