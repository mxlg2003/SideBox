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
  },
  hero: {
    badge: "Windows 11 原生 SideBox",
    titleLine1: "把常用的东西，",
    titleLine2: "放在屏幕边缘。",
    subtitle:
      "SideBox 贴在 Windows 11 屏幕边缘。钉上程序、文件和文件夹，把时钟、便笺与硬件状态嵌在 SideBox 上。它不替换任务栏，也不接管窗口。",
    downloadFree: "免费下载 Windows 版",
    exploreWidgets: "浏览 17 款内置模块",
    guarantee: "适用于 Windows 11 22H2+ · 100% 完全免费 · 无需账号 · 纯本地存储",
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
      "SideBox 默认悬浮在屏幕上沿或侧边，不占主屏底部，不篡改系统外壳，不修改 Explorer。你既能享受 Windows 11 原生的任务栏体验，又拥有随时触达的 SideBox。",
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
    search: "搜索",
    tip: "💡 真实还原 Windows 11 运行环境。SideBox 悬浮在屏幕边缘，不接管窗口，不替换任务栏。",
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
    title: "17 款小组件，全部包含。",
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
    badge: "Windows 11 原生级设计",
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
      "采用标准的 Windows 11 Win32 顶层悬浮架构，拒绝高危底层键盘钩子与系统注入，安静稳定不卡顿。",
    card4Point1: "不注入 Explorer 资源管理器",
    card4Point2: "不篡改系统任务栏与开始菜单",
    card4Point3: "不要求管理员权限常驻",
    card5Title: "便笺与钉选，留在你电脑上。",
    card5Desc:
      "不需要注册 SideBox 账号。你的所有便笺内容、钉选清单与情景配置均保存在本机，隐私完全属于你。",
    card5Note: "无需注册账号 · 零云端同步泄露",
    card6Title: "完全免费，零门槛使用。",
    card6Desc:
      "告别付费与买断门槛。所有模块与功能下载即用，无需激活码，零使用限制。",
    card6Badge1: "完全免费使用",
    card6Badge2: "全部功能无保留开放",
    card6Badge3: "纯净无广告",
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
    badge: "完全免费开放",
    title: "把常用的东西，放在屏幕边缘。",
    subtitle: "全部 17 款内置模块已完全开放。无需注册，无需激活，立即下载体验。",
    addonInfo: "",
    chooseBtn: "免费下载 Windows 安装包",
    downloadBtn: "查看常见问题",
    tag1: "⚡ 下载即用，无需激活",
    tag2: "🔒 纯本地数据隐私",
    tag3: "🛡️ 绿色纯净，不改任务栏",
  },
  footer: {
    desc: "Windows 11 屏幕边缘的 SideBox。钉应用、查便笺、看状态，不打扰原生任务栏。",
    minSys: "最低系统：Windows 11 22H2 (22621+)",
    navTitle: "导航",
    licenseTitle: "资源与下载",
    pricingPlan: "免费说明",
    addonDev: "下载安装包",
    downloadPkg: "安装与使用指南",
    guaranteeTitle: "使用保障",
    guaranteeHead: "✨ 永久免费无捆绑",
    guaranteeDesc: "全部模块开放使用，无广告，不修改系统文件，数据永驻本地。",
    rights: "© 2026 SideBox · 保留所有权利",
    tagLocal: "100% 本地隐私",
    tagNoSub: "完全免费",
    tagCoexist: "不接管任务栏",
  },
  pricingPage: {
    title: "SideBox 现已完全免费。",
    subtitle: "无需购买，无订阅，所有 17 款小组件全量开放。",
    fallbackNotice: "",
    chooseDevice: "",
    payNote: "",
    wechatPay: "",
    mockPay: "",
    buyBtn: "立即免费下载",
    alreadyBought: "",
    addonLink: "",
    trialNote: "",
    beforePay: "关于免费与使用。",
    freeFeature1Title: "17 款小组件全免费",
    freeFeature1Desc: "时钟、便笺、天气、股票、白噪音、系统双环、网络折线等全部开放，任意组合自由上岛。",
    freeFeature2Title: "无需激活码与账号",
    freeFeature2Desc: "安装即用，没有登录门槛，不绑定设备台数，您可以在任意多台电脑上自由安装。",
    freeFeature3Title: "零内购 · 零广告 · 零捆绑",
    freeFeature3Desc: "坚持做纯粹高效的 Windows 桌面工具，无后台偷跑，无强制弹窗，专注服务您的桌面日常。",
    freeFeature4Title: "100% 本地存储",
    freeFeature4Desc: "所有快捷方式、便笺和配置保存在本地 %AppData%，不上传任何云端，隐私完全属于你。",
  },
  downloadPage: {
    badge: "完全免费 · 无需激活码",
    title: "下载 SideBox。",
    subtitle: "专为 Windows 11 屏幕边缘设计的轻量悬浮岛。安装即可直接使用全部 17 款内置模块，无需注册，无需激活。",
    cardTitle: "SideBox for Windows 11",
    cardSubtitle: "x64 架构 · 自包含安装包 · 纯净无捆绑",
    downloadBtn: "下载 Windows 安装包 (.exe)",
    pendingPkg: "📦 安装包已打包完成，即将挂载至公开 CDN。最低系统要求：Windows 11 22H2。",
    sysReq: "系统：Win 11 22H2 (Build 22621+)",
    noRuntime: "无需额外运行时",
    stepsTitle: "极简 3 步开始使用",
    step1Title: "安装并启动",
    step1Desc: "双击运行安装包。启动后屏幕边缘将自动悬浮初始 SideBox，包含时钟与便笺。",
    step2Title: "定制你的 SideBox",
    step2Desc: "从资源管理器拖入常用程序、文件夹，在设置中挑选心仪的实用小组件。",
    step3Title: "立即开始使用",
    step3Desc: "所有模块与情景均已全部解锁，开箱即用，无需输入任何激活码或绑定设备。",
    noCodeYet: "遇到使用问题？",
    viewPricingLink: "查看常见问答 ➔",
  },
  priceChooser: {
    unit: "台",
    bestValue: "单台最低",
    currency: "元",
  },
  buyPage: {
    title: "购买激活码。",
    subtitle: "选择台数并填写邮箱。支付完成后，激活码会留在支付页，并发送到这个邮箱。到 SideBox 设置里填写后，这台电脑才算绑定。",
    email: "邮箱",
    pendingOrder: "正在创建订单…",
    payPrefix: "支付",
    payUnit: "元",
    addonLink: "已有激活码，增购一台",
  },
  addonPage: {
    title: "增购一台。",
    descPrefix: "填写购买时使用的邮箱，以及已经在一台电脑上激活过的激活码。支付后激活码不变，可绑定台数加一。试用不能增购。",
    email: "邮箱",
    code: "激活码",
    pendingOrder: "正在创建订单…",
    payPrefix: "支付",
    payUnit: "元",
    noCodeLink: "还没有激活码，去购买",
  },
  payPage: {
    pageTitle: "支付 — SideBox",
    paidTitle: "付款完成 — SideBox",
    headingPaid: "付款完成",
    headingPay: "支付",
    orderPrefix: "订单",
    loadingOrder: "正在读取订单…",
    mockNotice: "这是本地模拟收款。点击下面的按钮完成本次订单。正式环境这里会显示微信收款码。",
    finishMock: "完成本地支付",
    confirming: "正在确认…",
    wechatNotice: "使用微信扫码支付。完成后此页会显示激活码。",
    qrAlt: "微信收款码",
    remainPrefix: "请在",
    minuteUnit: "分",
    secondUnit: "秒内完成",
    orderExpiring: "订单即将过期",
    addonSuccess: "增购已完成。激活码没有更换，可再绑定 1 台电脑。",
    buySuccessPrefix: "购买完成。这串激活码可绑定",
    buySuccessSuffix: "台电脑。打开 SideBox，在设置的许可证里填入。",
    codeLabel: "激活码",
    copySuccess: "已复制",
    copyBtn: "复制激活码",
    copyFailed: "复制失败，请手动选中激活码",
    downloadBtn: "去下载",
    orderExpired: "订单已过期。请重新下单。",
    orderNotDone: "这笔订单没有完成。",
    reorderBtn: "重新购买",
    mailSentPrefix: "激活码已发到",
    mailSentSuffix: "。如果没看到，看一下垃圾邮件。",
    mailFailed: "邮件没有发出去。请先保存上面的激活码。",
    mailSkipped: "邮件服务还没接上。请先保存上面的激活码。",
    mailSending: "正在发送邮件。页面上的激活码已经可以使用。",
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
  },
  hero: {
    badge: "Windows 11 Native Floating SideBox",
    titleLine1: "Keep what you use most,",
    titleLine2: "right at your screen edge.",
    subtitle:
      "SideBox rests neatly at your Windows 11 screen edge. Pin everyday apps, files, and folders, with live clocks, sticky notes, and system stats in one strip. It never replaces your taskbar or hijacks your windows.",
    downloadFree: "Download Free for Windows",
    exploreWidgets: "Explore All 17 Widgets",
    guarantee: "Windows 11 22H2+ · 100% Free · No Account · Local Data",
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
      "SideBox floats on the top or sides by default, leaving your bottom taskbar intact. No shell tampering, no Explorer injection. Enjoy native Windows 11 while having a dedicated dock right at hand.",
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
    search: "Search",
    tip: "💡 Realistic Windows 11 simulation. SideBox floats at the screen edge without hijacking windows or replacing the taskbar.",
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
    title: "17 widgets. All included.",
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
    badge: "Windows 11 Native Architecture",
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
      "Built on standard Windows 11 Win32 topmost overlay architecture. Never uses low-level keyboard hooks, never injects Explorer, and never requires administrator rights.",
    card4Point1: "Never injects File Explorer",
    card4Point2: "Never tampers with taskbar or Start menu",
    card4Point3: "Never requires administrator privileges",
    card5Title: "Notes and pins stay on your machine.",
    card5Desc:
      "No SideBox account required. All notes, pinned shortcuts, and profile configurations remain 100% on your local disk — zero cloud leaks.",
    card5Note: "No account required · Zero cloud tracking",
    card6Title: "Completely free, zero friction.",
    card6Desc:
      "No subscriptions, no licenses, and no paid paywalls. Every widget and feature is ready to use right out of the box.",
    card6Badge1: "100% Free to Use",
    card6Badge2: "All Features Included",
    card6Badge3: "No Ads, No Bundles",
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
    badge: "100% Free & Open",
    title: "Keep what you use most, at your screen edge.",
    subtitle: "All 17 widgets are completely unlocked. No registration, no license keys. Download and get started now.",
    addonInfo: "",
    chooseBtn: "Download for Windows (.exe)",
    downloadBtn: "Browse FAQ",
    tag1: "⚡ Instant setup, no activation",
    tag2: "🔒 100% local privacy",
    tag3: "🛡️ Clean & native, taskbar intact",
  },
  footer: {
    desc: "A floating module strip at your Windows 11 screen edge. Pin apps, check notes, and glance at status without taskbar disruption.",
    minSys: "Minimum OS: Windows 11 22H2 (22621+)",
    navTitle: "Navigation",
    licenseTitle: "Resources",
    pricingPlan: "Free Announcement",
    addonDev: "Download Setup",
    downloadPkg: "Usage Guide",
    guaranteeTitle: "Assurance",
    guaranteeHead: "✨ 100% Free & Clean",
    guaranteeDesc: "All modules unlocked. No ads, no system tampering, and all data stays strictly on your PC.",
    rights: "© 2026 SideBox · All rights reserved",
    tagLocal: "100% Local Privacy",
    tagNoSub: "100% Free",
    tagCoexist: "Taskbar Independent",
  },
  pricingPage: {
    title: "SideBox is now 100% free.",
    subtitle: "No purchases, no subscriptions. All 17 widgets are completely unlocked.",
    fallbackNotice: "",
    chooseDevice: "",
    payNote: "",
    wechatPay: "",
    mockPay: "",
    buyBtn: "Download Free Now",
    alreadyBought: "",
    addonLink: "",
    trialNote: "",
    beforePay: "About Free & Usage.",
    freeFeature1Title: "All 17 Widgets Included",
    freeFeature1Desc: "Clocks, notes, weather, stocks, ambient audio, hardware activity, network sparklines, and more — fully unlocked.",
    freeFeature2Title: "No License Keys or Accounts",
    freeFeature2Desc: "Ready immediately upon launch. No accounts, no activation codes, and no PC limits.",
    freeFeature3Title: "Zero In-App Purchases or Ads",
    freeFeature3Desc: "A clean and focused Windows utility with zero background adware or intrusive popups.",
    freeFeature4Title: "100% Local Data Privacy",
    freeFeature4Desc: "All notes and shortcuts stay strictly on your local disk in %AppData% — zero cloud tracking.",
  },
  downloadPage: {
    badge: "100% Free · No Activation Needed",
    title: "Download SideBox.",
    subtitle: "A lightweight floating island crafted for the Windows 11 screen edge. Download and use all 17 widgets immediately — no accounts, no license keys.",
    cardTitle: "SideBox for Windows 11",
    cardSubtitle: "x64 architecture · Self-contained installer · Clean & adware-free",
    downloadBtn: "Download Windows Setup (.exe)",
    pendingPkg: "📦 Installer packaged. Available soon on CDN. Minimum requirement: Windows 11 22H2.",
    sysReq: "OS: Win 11 22H2 (Build 22621+)",
    noRuntime: "No extra runtimes required",
    stepsTitle: "3 Easy Steps to Start",
    step1Title: "Install & Launch",
    step1Desc: "Double-click the installer. Upon launch, SideBox floats at the screen edge with clock & notes.",
    step2Title: "Customize your SideBox",
    step2Desc: "Drag in everyday apps and folders from Explorer, and choose your favorite widgets in Settings.",
    step3Title: "Ready to Use",
    step3Desc: "All modules and profiles are unlocked and ready to use — no activation codes or bound devices required.",
    noCodeYet: "Have questions?",
    viewPricingLink: "Browse FAQ ➔",
  },
  priceChooser: {
    unit: "PC",
    bestValue: "Best Value",
    currency: "CNY",
  },
  buyPage: {
    title: "Purchase License Key.",
    subtitle: "Select device count and enter your email. Once payment is complete, the license key will be displayed on the confirmation page and emailed to you. Enter it in SideBox Settings to activate.",
    email: "Email",
    pendingOrder: "Creating order...",
    payPrefix: "Pay",
    payUnit: "CNY",
    addonLink: "Already have a key? Add another PC",
  },
  addonPage: {
    title: "Add a Device.",
    descPrefix: "Enter your original purchase email and currently activated license key. After payment, your key remains unchanged while its allowed device count increases by one. Trials cannot add devices.",
    email: "Email",
    code: "License Key",
    pendingOrder: "Creating order...",
    payPrefix: "Pay",
    payUnit: "CNY",
    noCodeLink: "Don't have a license key yet? Buy now",
  },
  payPage: {
    pageTitle: "Payment — SideBox",
    paidTitle: "Payment Complete — SideBox",
    headingPaid: "Payment Complete",
    headingPay: "Payment",
    orderPrefix: "Order",
    loadingOrder: "Loading order...",
    mockNotice: "This is a local simulated payment. Click below to complete this order. Live environment displays a WeChat Pay QR code.",
    finishMock: "Complete Test Payment",
    confirming: "Confirming...",
    wechatNotice: "Scan the QR code with WeChat Pay. Your license key will appear on this page upon completion.",
    qrAlt: "WeChat Pay QR Code",
    remainPrefix: "Please complete within",
    minuteUnit: "m",
    secondUnit: "s",
    orderExpiring: "Order is expiring soon",
    addonSuccess: "Device addon complete. Your license key is unchanged and can now activate 1 more PC.",
    buySuccessPrefix: "Purchase complete. This license key can activate",
    buySuccessSuffix: "PCs. Open SideBox and enter it in Settings → License.",
    codeLabel: "License Key",
    copySuccess: "Copied",
    copyBtn: "Copy License Key",
    copyFailed: "Copy failed. Please manually select the key.",
    downloadBtn: "Download SideBox",
    orderExpired: "Order has expired. Please place a new order.",
    orderNotDone: "This order was not completed.",
    reorderBtn: "Order Again",
    mailSentPrefix: "License key sent to",
    mailSentSuffix: ". Check your spam folder if not found.",
    mailFailed: "Email delivery failed. Please save the license key above.",
    mailSkipped: "Email delivery not configured. Please save the license key above.",
    mailSending: "Sending email. The license key above is already valid and ready to use.",
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
