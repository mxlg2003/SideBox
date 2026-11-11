import { useState } from "react";
import { useI18n } from "@/lib/i18n";

interface WorkflowItem {
  id: string;
  title: string;
  badge: string;
  badgeColor: string;
  description: string;
  hotkey: string;
  apps: { name: string; color: string; iconText: string }[];
  widgets: string[];
}

const workflowsZh: WorkflowItem[] = [
  {
    id: "general",
    title: "日常情景 (General)",
    badge: "日常办公与通讯",
    badgeColor: "text-emerald-400 bg-emerald-500/10 border-emerald-500/20",
    description: "高频使用的网页、即时通讯工具与随手记录的灵感便笺，全在顺手可及的屏幕边缘。",
    hotkey: "Win + Shift + D",
    apps: [
      { name: "Edge 浏览器", color: "bg-sky-500/20 text-sky-400", iconText: "🌐" },
      { name: "工作微信", color: "bg-emerald-500/20 text-emerald-400", iconText: "💬" },
      { name: "设计稿文件夹", color: "bg-amber-500/20 text-amber-300", iconText: "📁" },
    ],
    widgets: ["本地时钟", "快速便笺", "自然白噪音"],
  },
  {
    id: "deep-work",
    title: "深度专注 (Deep Work)",
    badge: "编程研发与沉浸创作",
    badgeColor: "text-blue-400 bg-blue-500/10 border-blue-500/20",
    description: "隐藏冗杂的通讯软件，只留下核心代码编辑器、终端工具，搭配 25 分钟番茄钟与硬件监视。",
    hotkey: "Win + Shift + D",
    apps: [
      { name: "VS Code", color: "bg-blue-500/20 text-blue-400", iconText: "</>" },
      { name: "Windows Terminal", color: "bg-purple-500/20 text-purple-400", iconText: ">_" },
      { name: "Git 项目库", color: "bg-red-500/20 text-red-400", iconText: "📦" },
    ],
    widgets: ["25:00 番茄钟", "CPU+内存双环", "网络实时速率"],
  },
  {
    id: "media",
    title: "休憩放松 (Relax & Media)",
    badge: "壁纸轮换与大盘关注",
    badgeColor: "text-amber-400 bg-amber-500/10 border-amber-500/20",
    description: "每日一换的 Bing 4K 高清壁纸，关注大盘走势与下一次重要日程倒数，张弛有度。",
    hotkey: "Win + Shift + D",
    apps: [
      { name: "阅读笔记", color: "bg-amber-500/20 text-amber-300", iconText: "📖" },
      { name: "音乐播放", color: "bg-pink-500/20 text-pink-400", iconText: "🎵" },
    ],
    widgets: ["Bing 每日超清壁纸", "股票微折线", "日程倒数日"],
  },
];

const workflowsEn: WorkflowItem[] = [
  {
    id: "general",
    title: "General Profile",
    badge: "Everyday Work & Chat",
    badgeColor: "text-emerald-400 bg-emerald-500/10 border-emerald-500/20",
    description: "High-frequency browser tabs, instant messenger, and sticky notes — right at your screen edge.",
    hotkey: "Win + Shift + D",
    apps: [
      { name: "Edge Browser", color: "bg-sky-500/20 text-sky-400", iconText: "🌐" },
      { name: "Team Chat", color: "bg-emerald-500/20 text-emerald-400", iconText: "💬" },
      { name: "Projects Folder", color: "bg-amber-500/20 text-amber-300", iconText: "📁" },
    ],
    widgets: ["Local Clock", "Sticky Note", "White Noise"],
  },
  {
    id: "deep-work",
    title: "Deep Work Profile",
    badge: "Coding & Focused Flow",
    badgeColor: "text-blue-400 bg-blue-500/10 border-blue-500/20",
    description: "Hide noisy chat apps, keep code editors and terminal with a 25-minute Pomodoro timer and hardware monitors.",
    hotkey: "Win + Shift + D",
    apps: [
      { name: "VS Code", color: "bg-blue-500/20 text-blue-400", iconText: "</>" },
      { name: "Terminal", color: "bg-purple-500/20 text-purple-400", iconText: ">_" },
      { name: "Git Repos", color: "bg-red-500/20 text-red-400", iconText: "📦" },
    ],
    widgets: ["25:00 Focus Timer", "CPU & RAM Rings", "Live Network Rate"],
  },
  {
    id: "media",
    title: "Relax & Media",
    badge: "Wallpapers & Markets",
    badgeColor: "text-amber-400 bg-amber-500/10 border-amber-500/20",
    description: "Daily rotating Bing 4K wallpaper, market watch ticker, and milestone countdown.",
    hotkey: "Win + Shift + D",
    apps: [
      { name: "Reading Notes", color: "bg-amber-500/20 text-amber-300", iconText: "📖" },
      { name: "Music Player", color: "bg-pink-500/20 text-pink-400", iconText: "🎵" },
    ],
    widgets: ["Bing Daily Wallpaper", "Stock Sparkline", "Milestone Countdown"],
  },
];

export function WorkflowShowcase() {
  const { lang, t } = useI18n();
  const workflows = lang === "en" ? workflowsEn : workflowsZh;
  const [selectedId, setSelectedId] = useState("general");
  const current = workflows.find((w) => w.id === selectedId) || workflows[0];

  return (
    <section className="scroll-mt-20 py-20">
      <div className="page">
        <div className="grid gap-12 lg:grid-cols-[1fr_1.2fr] lg:items-center">
          {/* Left Column: Copy & Selectors */}
          <div>
            <span className="feature-label">
              <span className="h-1.5 w-1.5 rounded-full bg-purple-400 animate-pulse" />
              {t.workflow.badge}
            </span>
            <h2 className="display mt-4 text-4xl sm:text-5xl leading-tight">
              {t.workflow.titleLine1}
              <br />
              {t.workflow.titleLine2}
            </h2>
            <p className="mt-4 text-base leading-7 text-mute">{t.workflow.subtitle}</p>

            {/* Workflow Buttons */}
            <div className="mt-8 space-y-3">
              {workflows.map((wf) => {
                const isActive = wf.id === selectedId;
                return (
                  <button
                    key={wf.id}
                    type="button"
                    onClick={() => setSelectedId(wf.id)}
                    className={`w-full rounded-2xl border p-4 text-left transition-all duration-200 ${
                      isActive
                        ? "border-white/30 bg-white/[0.08] shadow-lg shadow-black/40"
                        : "border-white/5 bg-white/[0.02] hover:border-white/15 hover:bg-white/[0.04]"
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-semibold text-white">{wf.title}</span>
                      <span className={`rounded-full border px-2.5 py-0.5 text-[10px] font-medium ${wf.badgeColor}`}>
                        {wf.badge}
                      </span>
                    </div>
                    <p className="mt-2 text-xs leading-relaxed text-mute">{wf.description}</p>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Right Column: Visual Stage for Selected Workflow */}
          <div className="relative rounded-3xl border border-white/10 bg-gradient-to-br from-[#161722] to-[#0e0f14] p-6 sm:p-8 shadow-2xl backdrop-blur-2xl">
            {/* Top trigger hint bar */}
            <div className="flex items-center justify-between border-b border-white/10 pb-4">
              <div className="flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-accent-blue" />
                <span className="text-xs text-white/70">{t.workflow.triggerHint}</span>
              </div>
              <div className="flex items-center gap-1.5 text-xs text-white/90">
                <span className="keycap">Win</span>
                <span className="keycap">Shift</span>
                <span className="keycap">D</span>
              </div>
            </div>

            {/* Simulated Island in selected state */}
            <div className="mt-8 rounded-2xl border border-white/10 bg-[#1f202c]/90 p-5 shadow-inner">
              <div className="text-[11px] font-medium text-white/50 mb-3 flex items-center justify-between">
                <span>SideBox · {current.title}</span>
                <span className="text-emerald-400 text-[10px]">{t.workflow.activeBadge}</span>
              </div>

              {/* Island preview strip */}
              <div className="flex flex-wrap items-center gap-2.5 rounded-xl border border-white/15 bg-black/40 p-2.5 shadow-lg">
                {/* Apps in this profile */}
                {current.apps.map((app) => (
                  <div
                    key={app.name}
                    className={`flex h-10 items-center gap-2 rounded-lg ${app.color} px-3 text-xs font-medium shadow-sm transition-transform hover:scale-105`}
                  >
                    <span>{app.iconText}</span>
                    <span className="text-white">{app.name}</span>
                  </div>
                ))}

                {/* Divider */}
                <div className="h-6 w-px bg-white/15 my-auto" />

                {/* Widgets in this profile */}
                {current.widgets.map((widget) => (
                  <div
                    key={widget}
                    className="flex h-10 items-center gap-1.5 rounded-lg border border-white/10 bg-white/10 px-3 text-xs text-white"
                  >
                    <span className="h-1.5 w-1.5 rounded-full bg-accent-cyan" />
                    <span>{widget}</span>
                  </div>
                ))}
              </div>
            </div>

            {/* Undo trigger note */}
            <div className="mt-6 flex items-center justify-between rounded-xl bg-white/[0.03] border border-white/5 p-3 text-xs text-mute">
              <span>{t.workflow.undoNote}</span>
              <div className="flex items-center gap-1 text-white">
                <span className="keycap">Win</span>
                <span className="keycap">Shift</span>
                <span className="keycap">Z</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
