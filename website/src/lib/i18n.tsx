import React, { createContext, useContext, useEffect, useState, useMemo } from "react";
import { useLocation, useNavigate } from "react-router-dom";

export type Lang = "zh" | "en";

interface I18nContextType {
  lang: Lang;
  setLang: (lang: Lang) => void;
  toggleLang: () => void;
  t: typeof zh;
  l: (path: string) => string;
}

const zh = {
  header: {
    product: "产品",
    modules: "小组件",
    features: "特性",
    faq: "问答",
    download: "下载",
    downloadFree: "免费下载",
    langName: "English",
    themeLight: "浅色模式",
    themeDark: "深色模式",
    themeSystem: "跟随系统",
  },
  hero: {
    badge: "Windows 原生轻量 SideBox",
    titleLine1: "把常用的东西，",
    titleLine2: "放在屏幕边缘。",
    subtitle:
      "SideBox 贴在 Windows 屏幕边缘。钉上常用程序与文件夹，把时钟、便笺与硬件状态嵌在 SideBox 上。它不替换任务栏，也不接管窗口。",
    downloadFree: "免费下载 Windows 版",
    exploreWidgets: "浏览内置小组件",
    guarantee: "适用于 Windows 11 / 10 (64 位) · 纯本地存储 · 开箱即用",
  },
  values: {
    badge: "为什么选择 SideBox",
    title: "更懂 Windows，却不打扰你的屏幕。",
    subtitle: "它不是任务栏的竞争对手，而是你屏幕边缘最亲密的高效伙伴。",
    card1Label: "屏幕边缘",
    card1Title: "钉在边上，常用随手即得。",
    card1Desc:
      "从资源管理器把每日打开的代码、文档、文件夹直接拖到 SideBox 上。时钟、便笺和番茄钟嵌在 SideBox 里，不占多余屏幕空间。点击正在运行的程序直接切到现有窗口，绝不重复多开。",
    card2Label: "系统原生",
    card2Title: "保留你熟悉的原生任务栏。",
    card2Desc:
      "SideBox 默认悬浮在屏幕上沿或侧边，不占主屏底部，不篡改系统外壳，不修改 Explorer。你既能享受 Windows 原生的任务栏体验，又拥有随时触达的 SideBox。",
    taskbarBadge: "保留完好 · 互不干扰",
  },
  simulator: {
    edge: "停靠边缘:",
    topCenter: "上沿居中",
    leftEdge: "左侧贴边",
    rightEdge: "右侧贴边",
    profile: "情景预设:",
    general: "日常",
    deepWork: "深度工作",
    material: "SideBox 材质:",
    mica: "云母",
    frosted: "磨砂",
    glass: "亚克力",
    autoHide: "自动隐藏 (800ms)",
    themeMode: "模拟器桌面:",
    themeLight: "浅色桌面",
    themeDark: "深色桌面",
    search: "搜索",
    tip: "💡 真实还原 Windows 桌面运行环境。SideBox 悬浮在屏幕边缘，不接管窗口，不替换任务栏。",
    browserTip: "Edge 浏览器 · 点击切到现有窗口",
    chatTip: "通讯应用 · 点击切到现有窗口",
    folderTip: "项目文件夹 · 徽标 P",
    clockTip: "本地时钟",
    noteTip: "便笺 · 点击快速编辑",
    whiteNoiseTip: "白噪音 · 雨声与咖啡馆",
    ideTip: "Visual Studio / VS Code",
    terminalTip: "Windows Terminal",
    timerTip: "番茄专注计时器 · 点击开始/暂停",
    sysTip: "系统状态 · CPU 18% | 物理内存 42%",
    noteTitle: "快速便笺 (本地自动保存)",
    notePlaceholder: "写点什么...",
    timerStart: "开始",
    timerPause: "暂停",
  },
  widgetsSection: {
    badge: "内置模块展墙",
    title: "丰富小组件，全部包含。",
    subtitle:
      "图标加主值，没有臃肿标题行。时钟、便笺、性能双环、实时网络、每日壁纸、白噪音……点开即在 SideBox 旁平滑展开。",
    backAll: "返回全部流动展墙",
    footerNote1: "全部模块嵌在 SideBox 上",
    footerNote2: "同一模块可添加多份",
    footerNote3: "点击在 SideBox 旁平滑展开，不弹多余独立窗口",
  },
  workflow: {
    badge: "情景与工作流",
    titleLine1: "工作状态在变，",
    titleLine2: "边上的 SideBox 也随之切换。",
    subtitle:
      "日常协作和深度专注，不需要同一套图标。按一下快捷键或者在托盘点击，SideBox 秒切为对应的高效工作台。",
    triggerHint: "触发方式: 快捷键切换",
    activeBadge: "● 已激活",
    undoNote: "误按切换？随时按下撤销返回上个情景",
  },
  bento: {
    badge: "Windows 原生级设计",
    title: "细节，处处到位。",
    subtitle: "不替换任务栏，不接管窗口。做 Windows 屏幕边缘最顺手的 SideBox。",
    card1Title: "把常用的东西钉在边上。",
    card1Desc:
      "从资源管理器直接拖入程序、文件或文件夹。点击已在运行的程序直接切到现有窗口，绝不重复多开。",
    card1Action: "切到现有窗口",
    card2Title: "一套快捷键，随叫随到。",
    card2Desc:
      "无需打开设置，用全局快捷键秒切工作流或唤出隐藏的 SideBox。误触切换还可按快捷键一键撤销。",
    hotkeySwitch: "切换情景:",
    hotkeySummon: "呼出 SideBox:",
    hotkeyUndo: "一键撤销:",
    card3Title: "上、左、右停靠，空闲自动隐藏。",
    card3Desc:
      "自由停靠在屏幕上沿、左侧或右侧，默认不占底部任务栏。空闲 800ms 自动缩进为 4px 细条，鼠标贴边即滑出。",
    autoHideTag: "自动隐藏 800ms",
    card4Title: "干净纯粹，无侵入式设计。",
    card4Desc:
      "采用标准 Win32 顶层悬浮架构，拒绝高危底层键盘钩子与系统注入，安静稳定不卡顿。",
    card4Point1: "不注入 Explorer 资源管理器",
    card4Point2: "不篡改系统任务栏与开始菜单",
    card4Point3: "不要求管理员权限常驻",
    card5Title: "便笺与钉选，留在你电脑上。",
    card5Desc:
      "不需要注册 SideBox 账号。你的所有便笺内容、钉选清单与情景配置均保存在本机，隐私完全属于你。",
    card5Note: "数据永驻本地 · 纯净零上传",
    card6Title: "轻快敏捷，低能耗静默驻留。",
    card6Desc:
      "拒绝庞大臃肿的网页打包套壳。采用原生 C# 构建，日常内存占用仅约 40MB，不抢占工作与游戏时的宝贵系统资源。",
    card6Badge1: "~40MB 内存占用",
    card6Badge2: "低能耗待机",
    card6Badge3: "极速冷启动",
  },
  testimonials: {
    badge: "用户评价与反馈",
    title: "大家都在怎么说。",
    subtitle: "来自真实效率用户与开发者的实际使用体验。",
  },
  faqSection: {
    badge: "常见疑问解答",
    titleLine1: "常见疑问",
    titleLine2: "与快速解答。",
    subtitle: "还有其他疑问或遇到使用问题？随时联系支持或查看常见解答。",
    tryFreeBtn: "立即免费下载 SideBox ➔",
  },
  pricingSection: {
    badge: "即刻开启高效桌面",
    title: "让屏幕边缘，成为得力助手。",
    subtitle: "纯净无广告，极低系统开销，解压或安装即可享受更从容的 Windows 桌面。",
    addonInfo: "",
    chooseBtn: "下载 Windows 版",
    downloadBtn: "查看常见问答",
    tag1: "⚡ ~40MB 原生低耗",
    tag2: "🔒 纯本地数据存储",
    tag3: "🛡️ 不打扰原生任务栏",
  },
  footer: {
    desc: "Windows 屏幕边缘的轻量 SideBox。钉应用、查便笺、看状态，不打扰原生任务栏。",
    minSys: "最低系统：Windows 11 或 Windows 10 (19041+) 64 位",
    navTitle: "导航",
    licenseTitle: "资源与下载",
    pricingPlan: "功能特性",
    addonDev: "下载安装包",
    downloadPkg: "安装与使用指南",
    guaranteeTitle: "使用保障",
    guaranteeHead: "✨ 纯净高效 · 绿色无捆绑",
    guaranteeDesc: "原生架构开发，无广告弹窗，不修改系统文件，数据永驻本地。",
    rights: "© 2026 SideBox · 保留所有权利",
    tagLocal: "100% 本地隐私",
    tagNoSub: "纯净无广告",
    tagCoexist: "不接管任务栏",
  },
  downloadPage: {
    badge: "绿色轻量 · 开箱即用",
    title: "下载 SideBox。",
    subtitle: "专为 Windows 屏幕边缘设计的轻量悬浮岛。纯本地存储，安装即用。",
    cardTitle: "SideBox for Windows",
    cardSubtitle: "x64 架构 · 自包含安装包 · 纯净无捆绑",
    downloadBtn: "下载 Windows 安装包 (.exe)",
    pendingPkg: "📦 安装包已打包完成，即将挂载至公开 CDN。最低系统要求：Windows 11 / 10 (19041+)。",
    sysReq: "系统：Win 11 / Win 10 (Build 19041+)",
    noRuntime: "无需额外运行时",
    stepsTitle: "极简 3 步开始使用",
    step1Title: "安装并启动",
    step1Desc: "双击运行安装包。启动后屏幕边缘将自动悬浮初始 SideBox，包含时钟与便笺。",
    step2Title: "定制你的 SideBox",
    step2Desc: "从资源管理器拖入常用程序、文件夹，在设置中挑选心仪的实用小组件。",
    step3Title: "立即开始使用",
    step3Desc: "无需复杂配置，开箱即用。快捷键与实用小组件随时就绪。",
    noCodeYet: "遇到使用问题？",
    viewPricingLink: "查看常见问答 ➔",
  },
  cookie: {
    title: "我们重视您的隐私",
    desc: "SideBox 仅使用必要的本地存储与 Cookie 来记忆您的深浅色主题、多语言偏好并保障网站流畅运行。我们绝不跨站追踪或向第三方出售您的个人数据。",
    essentialTitle: "必要偏好存储",
    essentialDesc: "记忆深浅色主题、语言选择与桌面模拟器状态（始终启用）。",
    acceptAll: "全部接受",
    essentialOnly: "仅必要",
    manage: "偏好详情",
    hideDetails: "收起详情",
    cookieSettings: "Cookie 设置",
  },
};

const en: typeof zh = {
  header: {
    product: "Product",
    modules: "Widgets",
    features: "Features",
    faq: "FAQ",
    download: "Download",
    downloadFree: "Download Free",
    langName: "中文",
    themeLight: "Light",
    themeDark: "Dark",
    themeSystem: "System",
  },
  hero: {
    badge: "Windows Native SideBox",
    titleLine1: "Keep what you use most,",
    titleLine2: "right at your screen edge.",
    subtitle:
      "SideBox rests neatly at your Windows screen edge. Pin everyday apps, files, and folders, with live clocks, sticky notes, and system stats in one strip. It never replaces your taskbar or hijacks your windows.",
    downloadFree: "Download Free for Windows",
    exploreWidgets: "Explore Built-in Widgets",
    guarantee: "Compatible with Windows 11 / 10 (64-bit) · 100% Local · Instant Setup",
  },
  values: {
    badge: "Why SideBox",
    title: "Built for Windows, without cluttering your screen.",
    subtitle: "Not a taskbar competitor — your most accessible companion at the screen edge.",
    card1Label: "Screen Edge",
    card1Title: "Pinned to the edge, always within reach.",
    card1Desc:
      "Drag and drop everyday apps, documents, and folders straight from File Explorer. Clocks, sticky notes, and Pomodoro timers live in the same strip without taking extra screen space. Clicking an open app switches directly to its existing window.",
    card2Label: "Native Coexistence",
    card2Title: "Keep your familiar Windows taskbar.",
    card2Desc:
      "SideBox floats on the top or sides by default, leaving your bottom taskbar intact. No shell tampering, no Explorer injection. Enjoy native Windows while having a dedicated dock right at hand.",
    taskbarBadge: "Intact & Independent",
  },
  simulator: {
    edge: "Dock Edge:",
    topCenter: "Top Center",
    leftEdge: "Left Edge",
    rightEdge: "Right Edge",
    profile: "Profile:",
    general: "General",
    deepWork: "Deep Work",
    material: "Material:",
    mica: "Mica",
    frosted: "Frosted",
    glass: "Acrylic Glass",
    autoHide: "Auto-hide (800ms)",
    themeMode: "Stage Desktop:",
    themeLight: "Light Desktop",
    themeDark: "Dark Desktop",
    search: "Search",
    tip: "💡 Realistic Windows desktop simulation. SideBox floats at the screen edge without hijacking windows or replacing the taskbar.",
    browserTip: "Browser · Switch to existing window",
    chatTip: "Chat App · Switch to existing window",
    folderTip: "Project Folder · Tag P",
    clockTip: "Local Clock",
    noteTip: "Sticky Note · Click to quick edit",
    whiteNoiseTip: "White Noise · Rain & Café",
    ideTip: "Visual Studio / VS Code",
    terminalTip: "Windows Terminal",
    timerTip: "Pomodoro Focus Timer · Click to start/pause",
    sysTip: "System Activity · CPU 18% | RAM 42%",
    noteTitle: "Quick Note (Saved locally)",
    notePlaceholder: "Write something...",
    timerStart: "Start",
    timerPause: "Pause",
  },
  widgetsSection: {
    badge: "Built-in Modules",
    title: "Rich widgets. All included.",
    subtitle:
      "Icons with primary values — no bulky headers. Clocks, sticky notes, hardware dual-rings, live network sparkline, Bing 4K wallpaper, ambient noise... expand smoothly beside SideBox with a single click.",
    backAll: "Return to All Floating Wall",
    footerNote1: "All modules live in the same SideBox",
    footerNote2: "Add multiple copies of any widget",
    footerNote3: "Expands smoothly beside SideBox without standalone popup clutter",
  },
  workflow: {
    badge: "Profiles & Workflows",
    titleLine1: "Your workday changes,",
    titleLine2: "SideBox changes with you.",
    subtitle:
      "Collaboration and deep focus require different tools. Switch profiles instantly with a hotkey or tray click to adapt your workspace in seconds.",
    triggerHint: "Trigger: Global Shortcut",
    activeBadge: "● Active",
    undoNote: "Accidental switch? Press Win + Shift + Z anytime to undo.",
  },
  bento: {
    badge: "Windows Native Architecture",
    title: "The little things, taken care of.",
    subtitle: "No taskbar replacement, no window hijacking. The smoothest companion on your screen edge.",
    card1Title: "Keep useful tools right at hand.",
    card1Desc:
      "Drag & drop apps, files, or folders directly from File Explorer. Clicking a running app switches to its existing window — no redundant duplicates.",
    card1Action: "Switch to existing window",
    card2Title: "A shortcut away, whenever you need it.",
    card2Desc:
      "No need to open settings. Use global hotkeys to switch profiles or summon a hidden SideBox. Undo mistaken triggers with a single keypress.",
    hotkeySwitch: "Switch profile:",
    hotkeySummon: "Summon SideBox:",
    hotkeyUndo: "Quick Undo:",
    card3Title: "Dock anywhere, auto-hide when idle.",
    card3Desc:
      "Freely dock to top, left, or right edges without crowding the taskbar. Auto-hides to a subtle 4px hairline after 800ms of inactivity; slides out when touched.",
    autoHideTag: "Auto-hide 800ms",
    card4Title: "Clean, lightweight, non-intrusive.",
    card4Desc:
      "Built on standard Win32 topmost overlay architecture. Never uses low-level keyboard hooks, never injects Explorer, and never requires administrator rights.",
    card4Point1: "Never injects File Explorer",
    card4Point2: "Never tampers with taskbar or Start menu",
    card4Point3: "Never requires administrator privileges",
    card5Title: "Notes and pins stay on your machine.",
    card5Desc:
      "No SideBox account required. All notes, pinned shortcuts, and profile configurations remain 100% on your local disk — zero cloud leaks.",
    card5Note: "100% local storage · Zero cloud telemetry",
    card6Title: "Ultra-lightweight & efficient.",
    card6Desc:
      "No heavy Electron wrappers. Crafted with native C# and high-performance desktop rendering, idling at just ~40MB RAM without eating your CPU or gaming resources.",
    card6Badge1: "~40MB RAM",
    card6Badge2: "Low-power idle",
    card6Badge3: "Instant launch",
  },
  testimonials: {
    badge: "User Reviews & Feedback",
    title: "What people are saying.",
    subtitle: "Authentic thoughts from developers, designers, and multitasking professionals.",
  },
  faqSection: {
    badge: "Frequently Asked Questions",
    titleLine1: "Common questions",
    titleLine2: "answered simply.",
    subtitle: "Have more questions or need assistance? Reach out to support or browse the answers below.",
    tryFreeBtn: "Download SideBox Free ➔",
  },
  pricingSection: {
    badge: "Elevate your desktop",
    title: "Keep what matters at your screen edge.",
    subtitle: "Zero bloat, minimal footprint. Download and experience a cleaner, more focused Windows desktop.",
    addonInfo: "",
    chooseBtn: "Download for Windows (.exe)",
    downloadBtn: "Browse FAQ",
    tag1: "⚡ ~40MB Native Footprint",
    tag2: "🔒 100% Local Storage",
    tag3: "🛡️ Leaves Taskbar Untouched",
  },
  footer: {
    desc: "A lightweight module strip at your Windows screen edge. Pin apps, check notes, and glance at status without taskbar disruption.",
    minSys: "Minimum OS: Windows 11 or Windows 10 (19041+) 64-bit",
    navTitle: "Navigation",
    licenseTitle: "Resources",
    pricingPlan: "Features",
    addonDev: "Download Setup",
    downloadPkg: "Usage Guide",
    guaranteeTitle: "Assurance",
    guaranteeHead: "✨ Clean & Lightweight",
    guaranteeDesc: "Native architecture with zero ads. Never modifies system files, and all data stays local.",
    rights: "© 2026 SideBox · All rights reserved",
    tagLocal: "100% Local Privacy",
    tagNoSub: "Zero Ads",
    tagCoexist: "Taskbar Independent",
  },
  downloadPage: {
    badge: "Lightweight · Instant Setup",
    title: "Download SideBox.",
    subtitle: "A lightweight floating island crafted for the Windows screen edge. 100% local, ready to use immediately.",
    cardTitle: "SideBox for Windows",
    cardSubtitle: "x64 architecture · Self-contained installer · Clean & adware-free",
    downloadBtn: "Download Windows Setup (.exe)",
    pendingPkg: "📦 Installer packaged. Available soon on CDN. Minimum requirement: Windows 11 / 10 (19041+).",
    sysReq: "OS: Win 11 / Win 10 (Build 19041+)",
    noRuntime: "No extra runtimes required",
    stepsTitle: "3 Easy Steps to Start",
    step1Title: "Install & Launch",
    step1Desc: "Double-click the installer. Upon launch, SideBox floats at the screen edge with clock & notes.",
    step2Title: "Customize your SideBox",
    step2Desc: "Drag in everyday apps and folders from Explorer, and choose your favorite widgets in Settings.",
    step3Title: "Ready to Use",
    step3Desc: "No complicated setup required. Hotkeys and built-in widgets are ready to go immediately.",
    noCodeYet: "Have questions?",
    viewPricingLink: "Browse FAQ ➔",
  },
  cookie: {
    title: "We value your privacy",
    desc: "SideBox only uses essential local storage and cookies to remember your theme, language preferences, and ensure a smooth experience. We do not track you across third-party websites or sell personal data.",
    essentialTitle: "Essential Storage",
    essentialDesc: "Remembers light/dark theme, language, and desktop simulator state (always active).",
    acceptAll: "Accept All",
    essentialOnly: "Essential Only",
    manage: "Preferences",
    hideDetails: "Hide Details",
    cookieSettings: "Cookie Settings",
  },
};

const I18nContext = createContext<I18nContextType | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const navigate = useNavigate();

  // Determine initial language from URL pathname or localStorage
  const [lang, setLangState] = useState<Lang>(() => {
    if (typeof window !== "undefined") {
      if (window.location.pathname.startsWith("/en")) return "en";
      const saved = localStorage.getItem("SideBox_lang");
      if (saved === "en" || saved === "zh") return saved;
      if (navigator.language.startsWith("en")) return "en";
    }
    return "zh";
  });

  // Sync lang with URL pathname whenever location changes
  useEffect(() => {
    if (location.pathname.startsWith("/en")) {
      if (lang !== "en") setLangState("en");
    } else {
      if (lang === "en" && !localStorage.getItem("SideBox_lang_forced_en")) {
        // If not on /en and not explicitly forced, keep zh
        setLangState("zh");
      }
    }
  }, [location.pathname]);

  const setLang = (newLang: Lang) => {
    setLangState(newLang);
    localStorage.setItem("SideBox_lang", newLang);
    if (newLang === "en") {
      localStorage.setItem("SideBox_lang_forced_en", "1");
    } else {
      localStorage.removeItem("SideBox_lang_forced_en");
    }

    // Switch URL between /en and /
    const current = location.pathname;
    let nextPath = current;
    if (newLang === "en") {
      if (!current.startsWith("/en")) {
        nextPath = current === "/" ? "/en" : `/en${current}`;
      }
    } else {
      if (current.startsWith("/en")) {
        nextPath = current.replace(/^\/en/, "") || "/";
      }
    }
    if (nextPath !== current) {
      navigate(nextPath + location.search + location.hash);
    }
  };

  const toggleLang = () => {
    setLang(lang === "zh" ? "en" : "zh");
  };

  // Helper to construct localized links
  const l = (path: string): string => {
    if (lang === "zh") return path;
    if (path === "/") return "/en";
    if (path.startsWith("/#")) return `/en#${path.slice(2)}`;
    if (path.startsWith("/")) return `/en${path}`;
    return path;
  };

  const t = useMemo(() => (lang === "en" ? en : zh), [lang]);

  return (
    <I18nContext.Provider value={{ lang, setLang, toggleLang, t, l }}>
      {children}
    </I18nContext.Provider>
  );
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) {
    throw new Error("useI18n must be used within an I18nProvider");
  }
  return ctx;
}
