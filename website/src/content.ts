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
    id: "free",
    q: "SideBox 是免费软件吗？",
    a: "是的，SideBox 现已完全免费。所有 17 款实用模块（包括时钟、便笺、天气、股票、白噪音、硬件监控、日程倒数等）以及自定义情景均无限制开放，没有任何收费项与功能锁定。",
  },
  {
    id: "account",
    q: "需要注册账号或输入激活码吗？",
    a: "不需要。没有账号体系，不需要激活码，也不联网校验设备。安装后打开即可直接使用全部功能。",
  },
  {
    id: "device",
    q: "可以安装在多台电脑上吗？",
    a: "可以。您可以在您的所有 Windows 11 电脑上自由安装和使用，没有任何设备数量或台数限制。",
  },
  {
    id: "taskbar",
    q: "会替换任务栏吗？",
    a: "不会。SideBox 贴在屏幕上沿、左侧或右侧，默认不占主屏底部。它不接管窗口，也不替换资源管理器。",
  },
  {
    id: "system",
    q: "需要什么系统？",
    a: "Windows 11 22H2（版本 22621）或更新的 64 位系统。",
  },
  {
    id: "data",
    q: "数据存在哪里？",
    a: "钉选、便笺与配置全部留在这台电脑上本地保存。没有云端同步，隐私完全属于你。",
  },
];

export const faqEn = [
  {
    id: "free",
    q: "Is SideBox completely free?",
    a: "Yes, SideBox is now 100% free. All 17 widgets (including Clock, Notes, Weather, Stocks, Ambient Noise, Hardware Activity, Schedule, etc.) and custom profiles are fully unlocked with zero limitations.",
  },
  {
    id: "account",
    q: "Do I need an account or license key?",
    a: "No. There are no accounts, no license keys, and no online device verifications. Download and enjoy all features immediately upon launch.",
  },
  {
    id: "device",
    q: "Can I install it on multiple PCs?",
    a: "Yes. You can freely install and use SideBox across all your Windows 11 machines with no device limits.",
  },
  {
    id: "taskbar",
    q: "Does it replace the Windows taskbar?",
    a: "No. SideBox docks at the top, left, or right edges of your screen, leaving the bottom taskbar untouched. It never takes over windows or modifies File Explorer.",
  },
  {
    id: "system",
    q: "What are the system requirements?",
    a: "Windows 11 22H2 (Build 22621) or newer 64-bit systems.",
  },
  {
    id: "data",
    q: "Where is my data stored?",
    a: "All pinned shortcuts, sticky notes, and configurations remain strictly on your local PC with zero cloud tracking.",
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
