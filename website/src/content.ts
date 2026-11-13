export const widgetsZh = [
  "时钟",
  "便笺",
  "秒表",
  "倒计时",
  "闹钟",
  "专注计时器",
  "时间进度",
  "系统状态",
  "网络",
  "电源",
  "磁盘",
  "GPU",
  "天气",
  "日程倒数",
  "Bing 壁纸",
  "股票",
  "白噪音",
];

export const widgetsEn = [
  "Clock",
  "Sticky Notes",
  "Stopwatch",
  "Countdown",
  "Alarm",
  "Focus Timer",
  "Time Progress",
  "System Activity",
  "Network",
  "Power & Battery",
  "Disk Usage",
  "GPU Activity",
  "Weather",
  "Schedule",
  "Bing Wallpaper",
  "Stocks",
  "White Noise",
];

export const faqZh = [
  {
    id: "system",
    q: "支持哪些 Windows 系统版本？",
    a: "支持 Windows 11 与 Windows 10（64 位，Build 19041 及以上版本）。自包含轻量运行库，下载后直接运行即可，无需额外安装繁琐环境。",
  },
  {
    id: "taskbar",
    q: "会替换任务栏或修改系统文件吗？",
    a: "不会。SideBox 独立悬浮于屏幕上沿、左侧或右侧，默认不占底部任务栏。它不接管窗口，不注入 Explorer 资源管理器，不修改任何系统文件，与原生桌面完美并存。",
  },
  {
    id: "performance",
    q: "日常运行的系统资源占用如何？",
    a: "基于原生 C# 与 Win32 顶层架构打造，绝非昂贵的 Electron 网页套壳。常规待机内存占用仅约 40MB，无交互时自动休眠，低 CPU 消耗，不抢占工作和游戏资源。",
  },
  {
    id: "free",
    q: "软件是否完全免费？需要注册账号吗？",
    a: "是的，SideBox 完全免费。没有账号体系，不需要激活码，也不收集个人信息。所有内置小组件与功能全量可用，没有任何功能锁定或内购广告。",
  },
  {
    id: "data",
    q: "便笺和钉选数据保存在哪里？",
    a: "所有配置、便笺记录与快捷方式均存储在本地电脑的 %AppData% 目录中。绝无云端上传与追踪，隐私完全属于您自己。",
  },
];

export const faqEn = [
  {
    id: "system",
    q: "Which Windows versions are supported?",
    a: "Supports Windows 11 and Windows 10 (64-bit, Build 19041 or newer). Self-contained and lightweight, ready to run directly without installing complicated runtimes.",
  },
  {
    id: "taskbar",
    q: "Does it replace the taskbar or modify system files?",
    a: "No. SideBox floats neatly at the top, left, or right edges of your screen, leaving the taskbar untouched. It never takes over windows, never injects Explorer, and modifies zero system files.",
  },
  {
    id: "performance",
    q: "How are the system resources and memory usage?",
    a: "Built with native C# and Win32 topmost architecture, never a bloated Electron web wrapper. Idle memory consumption is only ~40MB with minimal CPU overhead, preserving all resources for gaming and workflow.",
  },
  {
    id: "free",
    q: "Is it completely free? Do I need an account?",
    a: "Yes, SideBox is completely free. There are no accounts, no activation keys, and no telemetry tracking. All built-in widgets and profiles are fully unlocked with zero limitations or ads.",
  },
  {
    id: "data",
    q: "Where is my data stored?",
    a: "All pinned shortcuts, sticky notes, and configurations are stored locally in your PC's %AppData% directory. No cloud sync, no tracking — your data belongs solely to you.",
  },
];

export const widgets = widgetsZh;
export const faq = faqZh;

export function getFaq(lang: "zh" | "en") {
  return lang === "en" ? faqEn : faqZh;
}

export function getWidgets(lang: "zh" | "en") {
  return lang === "en" ? widgetsEn : widgetsZh;
}
