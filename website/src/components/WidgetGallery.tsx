import { useState, useEffect } from "react";
import { clockLabel, useNow } from "@/lib/clock";
import { useI18n, type Lang } from "@/lib/i18n";

type CategoryId = "all" | "time" | "hardware" | "lifestyle";

interface CategoryOption {
  id: CategoryId;
  label: string;
  count: number;
  desc: string;
}

const subcategoriesZh = [
  { id: "time" as CategoryId, label: "时间与效率", count: 7, desc: "本地时钟、番茄工作法、高精度计时与日程倒数" },
  { id: "hardware" as CategoryId, label: "硬件与状态", count: 5, desc: "CPU/内存双环、网络波动折线、电池与磁盘监视" },
  { id: "lifestyle" as CategoryId, label: "工作与生活", count: 5, desc: "随手便笺、实时天气、股票微折线、白噪音与Bing壁纸" },
];

const totalCountZh = subcategoriesZh.reduce((sum, c) => sum + c.count, 0);

const categoriesZh: CategoryOption[] = [
  { id: "all", label: "全部组件", count: totalCountZh, desc: "动态走马灯展墙 · 全部模块嵌在 SideBox 上" },
  ...subcategoriesZh,
];

const subcategoriesEn = [
  { id: "time" as CategoryId, label: "Time & Productivity", count: 7, desc: "Local clock, Pomodoro focus, stopwatch & schedule countdown" },
  { id: "hardware" as CategoryId, label: "Hardware & Stats", count: 5, desc: "CPU/RAM rings, network sparkline, battery & disk monitor" },
  { id: "lifestyle" as CategoryId, label: "Work & Lifestyle", count: 5, desc: "Sticky notes, live weather, stock ticker, white noise & Bing wallpaper" },
];

const totalCountEn = subcategoriesEn.reduce((sum, c) => sum + c.count, 0);

const categoriesEn: CategoryOption[] = [
  { id: "all", label: "All Widgets", count: totalCountEn, desc: "Dynamic marquee wall · All modules live in the same SideBox" },
  ...subcategoriesEn,
];

export function WidgetGallery() {
  const { lang, t } = useI18n();
  const now = useNow();
  const [activeCategory, setActiveCategory] = useState<CategoryId>("all");

  // Interactive micro states for widgets
  const [timerRunning, setTimerRunning] = useState(false);
  const [timerSeconds, setTimerSeconds] = useState(25 * 60);
  const [whiteNoisePlaying, setWhiteNoisePlaying] = useState(true);
  const [selectedWidgetInfo, setSelectedWidgetInfo] = useState<string | null>(null);

  useEffect(() => {
    if (!timerRunning) return;
    const interval = setInterval(() => {
      setTimerSeconds((prev) => (prev > 0 ? prev - 1 : 25 * 60));
    }, 1000);
    return () => clearInterval(interval);
  }, [timerRunning]);

  const formatTimer = (totalSec: number) => {
    const mins = Math.floor(totalSec / 60);
    const secs = totalSec % 60;
    return `${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
  };

  const categories = lang === "en" ? categoriesEn : categoriesZh;
  const currentCategoryObj = categories.find((c) => c.id === activeCategory) || categories[0];

  return (
    <div className="relative mx-auto mt-12 w-full max-w-6xl overflow-hidden py-4">
      {/* Category Filter Pills (Functional & Interactive!) */}
      <div className="mb-4 flex flex-wrap items-center justify-center gap-2.5">
        {categories.map((cat) => {
          const isActive = activeCategory === cat.id;
          return (
            <button
              key={cat.id}
              type="button"
              onClick={() => {
                setActiveCategory(cat.id);
                setSelectedWidgetInfo(null);
              }}
              className={`flex items-center gap-2 rounded-full px-4 py-2 text-xs font-semibold transition-all duration-200 select-none ${
                isActive
                  ? "bg-gradient-to-r from-blue-600 via-blue-500 to-indigo-600 !text-white shadow-md shadow-blue-500/25 scale-105"
                  : "border border-slate-200 dark:border-white/10 bg-white/80 dark:bg-white/[0.04] text-slate-700 dark:text-white/70 hover:bg-slate-100 dark:hover:bg-white/10 hover:text-slate-900 dark:hover:text-white"
              }`}
            >
              <span>{cat.label}</span>
            </button>
          );
        })}
      </div>

      {/* Category description banner */}
      <div className="mb-8 text-center text-xs text-mute flex items-center justify-center gap-2">
        <span className="h-1.5 w-1.5 rounded-full bg-accent-blue" />
        <span>{currentCategoryObj.desc}</span>
        {activeCategory !== "all" && (
          <button
            type="button"
            onClick={() => setActiveCategory("all")}
            className="text-accent-cyan underline ml-2 hover:text-white"
          >
            {t.widgetsSection.backAll}
          </button>
        )}
      </div>

      {/* Selected Widget Detail Hint */}
      {selectedWidgetInfo && (
        <div className="mb-6 mx-auto max-w-md rounded-xl border border-blue-500/30 bg-blue-500/10 p-3 text-center text-xs text-slate-800 dark:text-white animate-in fade-in duration-200">
          💡 {selectedWidgetInfo}
        </div>
      )}

      {/* VIEW 1: All Widgets Marquee Wall (When 'all' is selected) */}
      {activeCategory === "all" && (
        <div className="relative space-y-4 [mask-image:linear-gradient(to_right,transparent,black_8%,black_92%,transparent)]">
          {/* Row 1 (Forward direction) */}
          <div className="marquee-row group flex overflow-hidden">
            <div className="marquee-track flex shrink-0 animate-marquee items-center gap-3.5 group-hover:[animation-play-state:paused]">
              <RowOneWidgets now={now} lang={lang} onSelect={setSelectedWidgetInfo} />
              <RowOneWidgets now={now} lang={lang} onSelect={setSelectedWidgetInfo} />
            </div>
          </div>

          {/* Row 2 (Reverse direction) */}
          <div className="marquee-row group flex overflow-hidden">
            <div className="marquee-track flex shrink-0 animate-marquee-reverse items-center gap-3.5 group-hover:[animation-play-state:paused]">
              <RowTwoWidgets
                lang={lang}
                timerRunning={timerRunning}
                timerFormatted={formatTimer(timerSeconds)}
                toggleTimer={() => setTimerRunning(!timerRunning)}
                whiteNoisePlaying={whiteNoisePlaying}
                toggleWhiteNoise={() => setWhiteNoisePlaying(!whiteNoisePlaying)}
                onSelect={setSelectedWidgetInfo}
              />
              <RowTwoWidgets
                lang={lang}
                timerRunning={timerRunning}
                timerFormatted={formatTimer(timerSeconds)}
                toggleTimer={() => setTimerRunning(!timerRunning)}
                whiteNoisePlaying={whiteNoisePlaying}
                toggleWhiteNoise={() => setWhiteNoisePlaying(!whiteNoisePlaying)}
                onSelect={setSelectedWidgetInfo}
              />
            </div>
          </div>

          {/* Row 3 (Forward direction) */}
          <div className="marquee-row group flex overflow-hidden">
            <div className="marquee-track flex shrink-0 animate-marquee items-center gap-3.5 group-hover:[animation-play-state:paused]">
              <RowThreeWidgets lang={lang} onSelect={setSelectedWidgetInfo} />
              <RowThreeWidgets lang={lang} onSelect={setSelectedWidgetInfo} />
            </div>
          </div>
        </div>
      )}

      {/* VIEW 2: Category Filtered Grid (When specific category is clicked) */}
      {activeCategory !== "all" && (
        <div className="mx-auto max-w-5xl px-2">
          {activeCategory === "time" && (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 animate-in fade-in zoom-in-95 duration-200">
              {/* 1. 时钟 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Clock: High-precision local time with secondary timezone support."
                      : "时钟：读取本地高精度时间，支持第二时区，每秒实时刷新。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">clock · 1s</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Time" : "时间"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/10 text-white">
                    🕒
                  </div>
                  <div>
                    <div className="text-xl font-bold text-white font-mono">{clockLabel(now)}</div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Local clock · Dual timezone" : "本地时钟 · 支持第二时区"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 2. 专注番茄钟 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Focus Timer: Pomodoro timer with work/break cycles, click to start or pause."
                      : "专注计时器：本地番茄钟，支持专注与休息轮次联动，点击可直接开始/暂停。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-blue-400/50 bg-blue-50/60 dark:bg-blue-950/20"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-blue-600 dark:text-blue-300/70">focustimer · 200ms</span>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setTimerRunning(!timerRunning);
                    }}
                    className="rounded-full bg-accent-blue px-2.5 py-0.5 text-[10px] font-bold text-white hover:bg-blue-600"
                  >
                    {timerRunning ? (lang === "en" ? "Pause" : "暂停") : lang === "en" ? "Start" : "开始"}
                  </button>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">⏳</span>
                  <div>
                    <div className="text-xl font-bold text-slate-900 dark:text-white font-mono">{formatTimer(timerSeconds)}</div>
                    <div className="text-xs text-blue-600 dark:text-blue-300">
                      {lang === "en"
                        ? `Focus Timer ${timerRunning ? "· Running" : "· Paused"}`
                        : `番茄专注计时器 ${timerRunning ? "· 进行中" : "· 已暂停"}`}
                    </div>
                  </div>
                </div>
              </div>

              {/* 3. 时间进度 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Time Progress: Year/month/day elapsed percentage."
                      : "时间进度：年 / 月 / 日已过比例，SideBox 上可选条形、百分比或圆环。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">timeprogress · 1s</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Productivity" : "效率"}
                  </span>
                </div>
                <div className="text-base font-bold text-white mb-2">
                  {lang === "en" ? "2026 is 76.4% completed" : "2026 年已过 76.4%"}
                </div>
                <div className="h-2 w-full overflow-hidden rounded-full bg-white/10">
                  <div className="h-full w-[76.4%] rounded-full bg-gradient-to-r from-sky-400 to-indigo-500" />
                </div>
                <div className="mt-2 text-[11px] text-mute">
                  {lang === "en" ? "Cherish every passing moment" : "珍惜今日分秒，不负时光"}
                </div>
              </div>

              {/* 4. 秒表 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Stopwatch: High-precision lap timer, starts on click."
                      : "秒表：高精度本地秒表，支持分段计圈，点开即启动。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">stopwatch</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Timer" : "计时"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">⏱️</span>
                  <div>
                    <div className="text-xl font-bold text-white font-mono">00:14.28</div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Precision lap stopwatch" : "高精度计圈秒表"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 5. 倒计时 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Countdown: Single countdown and named presets with auto alerts."
                      : "倒计时：单次倒数与命名预设，到零自动发送通知。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">countdown</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Countdown" : "倒数"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">⏰</span>
                  <div>
                    <div className="text-xl font-bold text-white font-mono">08:30</div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Presentation & meeting preset" : "演讲与会议倒计时预设"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 6. 闹钟 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Alarm: Set next local alert time with chime and notifications."
                      : "闹钟：设定下一次本地提醒时刻，到点播放提示音并显示通知。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">alarm</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Reminder" : "提醒"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">🔔</span>
                  <div>
                    <div className="text-xl font-bold text-white font-mono">
                      {lang === "en" ? "07:30 Standup" : "07:30 晨会"}
                    </div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Tray chime & notification" : "托盘发音与通知"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 7. 第二时区 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Dual Timezone: Local clock displays a secondary global timezone."
                      : "双时区：本地时钟可选第二时区，方便跨国远程团队。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">timezone</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "World" : "世界"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">🌍</span>
                  <div>
                    <div className="text-xl font-bold text-white font-mono">
                      {lang === "en" ? "San Francisco 19:42" : "旧金山 19:42"}
                    </div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Global remote team sync" : "跨国远程团队协作"}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeCategory === "hardware" && (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 animate-in fade-in zoom-in-95 duration-200">
              {/* 1. 系统状态 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "System Activity: PerformanceCounter sampled CPU and physical RAM load."
                      : "系统状态：基于 PerformanceCounter 采样 CPU 与物理内存占用。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">system · 1s</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Hardware" : "硬件"}
                  </span>
                </div>
                <div className="flex items-center gap-4">
                  <div className="relative h-12 w-12">
                    <svg className="h-12 w-12 -rotate-90 transform" viewBox="0 0 24 24">
                      <circle cx="12" cy="12" r="10" stroke="rgba(255,255,255,0.12)" strokeWidth="2.5" fill="none" />
                      <circle
                        cx="12"
                        cy="12"
                        r="10"
                        stroke="#a855f7"
                        strokeWidth="2.5"
                        strokeDasharray="62.8"
                        strokeDashoffset="48"
                        strokeLinecap="round"
                        fill="none"
                      />
                    </svg>
                    <svg className="absolute inset-1.5 h-9 w-9 -rotate-90 transform" viewBox="0 0 16 16">
                      <circle cx="8" cy="8" r="6" stroke="rgba(255,255,255,0.12)" strokeWidth="2" fill="none" />
                      <circle
                        cx="8"
                        cy="8"
                        r="6"
                        stroke="#38bdf8"
                        strokeWidth="2"
                        strokeDasharray="37.7"
                        strokeDashoffset="20"
                        strokeLinecap="round"
                        fill="none"
                      />
                    </svg>
                  </div>
                  <div>
                    <div className="text-base font-bold text-white">CPU 18% · RAM 42%</div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "CPU & RAM dual rings" : "系统双环负载监视"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 2. 实时网络 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Network: Real-time adapter delta rate with 60s sparkline."
                      : "网络：适配器实时速率差值，点开展示 60 秒波动图。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">network · 1s</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Network" : "网络"}
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <div className="text-lg font-bold text-white font-mono">↓ 2.4 MB/s</div>
                    <div className="text-xs text-mute font-mono">↑ 480 KB/s</div>
                  </div>
                  <svg className="h-8 w-24" viewBox="0 0 60 20" fill="none">
                    <path d="M1 15 L12 12 L24 16 L36 6 L48 10 L59 4" stroke="#38bdf8" strokeWidth="2" strokeLinecap="round" />
                  </svg>
                </div>
              </div>

              {/* 3. 电源与外设 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Power & Battery: System battery and Bluetooth peripheral levels."
                      : "电源：系统电池与蓝牙外设电量监视，低电量主动提醒。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">power</span>
                  <span className="rounded bg-emerald-500/20 px-2 py-0.5 text-[10px] text-emerald-700 dark:text-emerald-300 font-medium">
                    {lang === "en" ? "Battery" : "电池"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 text-xl">
                    ⚡
                  </div>
                  <div>
                    <div className="text-lg font-bold text-slate-900 dark:text-white">
                      {lang === "en" ? "88% Charging" : "88% 充电中"}
                    </div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "BT Mouse & Keyboard 90%" : "蓝牙键鼠外设 90%"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 4. 磁盘空间 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Disk Usage: Periodic sampling of fixed SSD free space."
                      : "磁盘：定时采样固定硬盘剩余空间，不扫描移动盘，避免卡顿。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">disk · 2s</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Storage" : "存储"}
                  </span>
                </div>
                <div className="text-base font-bold text-white mb-2">NVMe SSD 512 GB</div>
                <div className="h-2 w-full overflow-hidden rounded-full bg-white/10">
                  <div className="h-full w-[65%] rounded-full bg-accent-blue" />
                </div>
                <div className="mt-2 text-xs text-mute">
                  {lang === "en" ? "178 GB free (65% used)" : "剩余 178 GB 可用 (65% 已用)"}
                </div>
              </div>

              {/* 5. GPU */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "GPU: Dedicated graphics card load and temperature state."
                      : "GPU：显卡占用率与轻重载检测，读不到计数器时自动显示友好状态。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">gpu · 2s</span>
                  <span className="rounded bg-purple-500/20 px-2 py-0.5 text-[10px] text-purple-700 dark:text-purple-300 font-medium">
                    {lang === "en" ? "GPU" : "显卡"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-purple-500/20 text-purple-700 dark:text-purple-300 font-mono font-bold text-xs">
                    GPU
                  </div>
                  <div>
                    <div className="text-lg font-bold text-slate-900 dark:text-white">RTX 4080 · 14%</div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Low temp · Silent idle" : "低温轻载静音中"}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeCategory === "lifestyle" && (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 animate-in fade-in zoom-in-95 duration-200">
              {/* 1. 随手便笺 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Sticky Note: Saved locally in notes folder with real-time auto-save."
                      : "便笺：保存在本地 notes 目录，编辑时自动实时保存，无需点保存按钮。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-amber-400/50 bg-[#fef8d3]/30 dark:bg-[#fef8d3]/5 border-amber-300/30 dark:border-amber-300/20"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-amber-700 dark:text-amber-300/80">notes</span>
                  <span className="rounded bg-amber-400/20 px-2 py-0.5 text-[10px] text-amber-800 dark:text-amber-300 font-medium">
                    {lang === "en" ? "Notes" : "便笺"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">📌</span>
                  <div>
                    <div className="text-base font-bold text-amber-900 dark:text-amber-200">
                      {lang === "en" ? "2:00 PM Release Review" : "下午 2:00 评审版本"}
                    </div>
                    <div className="text-xs text-amber-800/80 dark:text-amber-300/70">
                      {lang === "en" ? "Local auto-save · Instant capture" : "本地自动保存 · 点开即记"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 2. 实时天气 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Weather: Open-Meteo forecasts updated every ~30 minutes."
                      : "天气：通过 Open-Meteo 预报约每 30 分钟刷新，免配置 API Key。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">weather · 30m</span>
                  <span className="rounded bg-sky-500/15 dark:bg-sky-500/20 px-2 py-0.5 text-[10px] font-medium text-sky-700 dark:text-sky-300">
                    {lang === "en" ? "Weather" : "天气"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <div className="text-3xl">⛅</div>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-xl font-bold text-white">22°C</span>
                      <span className="text-xs font-medium text-sky-700 dark:text-sky-300">
                        {lang === "en" ? "Partly Cloudy" : "多云"}
                      </span>
                    </div>
                    <div className="text-xs text-mute">
                      {lang === "en" ? "Open-Meteo · 30m refresh" : "Open-Meteo · 30分钟更新"}
                    </div>
                  </div>
                </div>
              </div>

              {/* 3. 股票行情 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Stocks: Third-party quotes refreshed every ~20s with sparkline."
                      : "股票：第三方行情约 20s 刷新，红涨绿跌，支持自选代号。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">stocks · 20s</span>
                  <span className="rounded bg-emerald-500/15 dark:bg-emerald-500/20 px-2 py-0.5 text-[10px] font-medium text-emerald-700 dark:text-emerald-300">
                    {lang === "en" ? "Market" : "行情"}
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <div className="flex items-center gap-1.5">
                      <span className="text-base font-bold text-white">AAPL</span>
                      <span className="text-sm font-semibold text-emerald-400 font-mono">+2.14%</span>
                    </div>
                    <div className="text-xs text-mute font-mono">$234.50 USD</div>
                  </div>
                  <svg className="h-8 w-20" viewBox="0 0 60 24" fill="none">
                    <path d="M1 18 C12 16 18 8 28 11 S42 4 58 2" stroke="#10b981" strokeWidth="2" strokeLinecap="round" />
                  </svg>
                </div>
              </div>

              {/* 4. 自然白噪音 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "White Noise: Local multi-track natural sound mixing (rain/waves/cafe)."
                      : "白噪音：本地多轨自然混音（雨声/雷声/浪声/风声/咖啡馆/键盘声），可与番茄钟联动自动启闭。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">whitenoise</span>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setWhiteNoisePlaying(!whiteNoisePlaying);
                    }}
                    className="rounded-full bg-white/10 px-2.5 py-0.5 text-[10px] text-white hover:bg-white/20"
                  >
                    {whiteNoisePlaying ? (lang === "en" ? "Mute" : "静音") : lang === "en" ? "Play" : "播放"}
                  </button>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-2xl">🌧️</span>
                  <div>
                    <div className="text-base font-bold text-white">
                      {lang === "en" ? "Rain + Café Ambience" : "雨声 + 咖啡馆氛围"}
                    </div>
                    <div className="flex items-center gap-1.5 mt-1">
                      <span className="text-xs text-mute">
                        {lang === "en" ? "Spectrum:" : "频谱律动:"}
                      </span>
                      <div className="flex items-end gap-0.5 h-3">
                        <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-3 animate-pulse" : "h-1"}`} />
                        <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-2 animate-pulse" : "h-1"}`} />
                        <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-3.5 animate-pulse" : "h-1"}`} />
                        <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-1.5 animate-pulse" : "h-1"}`} />
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* 5. Bing 每日壁纸 */}
              <div
                onClick={() =>
                  setSelectedWidgetInfo(
                    lang === "en"
                      ? "Bing Wallpaper: Official UHD wallpapers from the last 8 days."
                      : "Bing 壁纸：近 8 日 Bing 官方超清壁纸，支持在 SideBox 上一键应用并下载设置为桌面背景。"
                  )
                }
                className="bento-card p-5 cursor-pointer hover:border-white/30"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-white/50">bing · UHD</span>
                  <span className="rounded bg-white/10 px-2 py-0.5 text-[10px] text-white/70">
                    {lang === "en" ? "Wallpaper" : "壁纸"}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <div className="h-12 w-16 rounded-lg bg-gradient-to-tr from-indigo-500 via-sky-400 to-amber-300 shadow-inner flex items-center justify-center text-[10px] font-bold text-black/70">
                    4K UHD
                  </div>
                  <div>
                    <div className="text-base font-bold text-white">
                      {lang === "en" ? "Bing Daily Wallpaper" : "Bing 每日壁纸"}
                    </div>
                    <div className="text-xs text-accent-cyan hover:underline cursor-pointer">
                      {lang === "en" ? "Apply as desktop wallpaper" : "点开即设为桌面"}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Bottom Notes */}
      <div className="mt-8 flex flex-wrap items-center justify-center gap-4 text-xs text-mute">
        <span>{t.widgetsSection.footerNote1}</span>
        <span>·</span>
        <span>{t.widgetsSection.footerNote2}</span>
        <span>·</span>
        <span>{t.widgetsSection.footerNote3}</span>
      </div>
    </div>
  );
}

// Subcomponents for the Marquee Wall
function RowOneWidgets({
  now,
  lang,
  onSelect,
}: {
  now: Date;
  lang: Lang;
  onSelect: (info: string) => void;
}) {
  return (
    <>
      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Clock: High-precision local time with secondary timezone support."
              : "时钟：读取本地高精度时间，支持第二时区，每秒实时刷新。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/10 text-white">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
        </div>
        <div className="text-left">
          <div className="text-sm font-semibold tracking-wide text-white font-mono">{clockLabel(now)}</div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Local clock · Dual timezone" : "本地时钟 · 第二时区可选"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Sticky Note: Saved locally in notes directory with automatic real-time saving."
              : "便笺：保存在本地 notes 目录，编辑时自动实时保存。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-amber-300/30 bg-[#f6e27a] px-4 py-2.5 text-[#1c1b19] shadow-lg shadow-amber-500/10 transition-all hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">📌</span>
        <div className="text-left">
          <div className="text-xs font-bold leading-tight">
            {lang === "en" ? "2:00 PM Release Review" : "下午 2:00 评审版本"}
          </div>
          <div className="text-[10px] text-black/60 leading-tight">
            {lang === "en" ? "Local auto-save · Instant capture" : "本地自动保存 · 点开即记"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Weather: Open-Meteo forecasts updated every ~30 minutes."
              : "天气：通过 Open-Meteo 预报约每 30 分钟刷新。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="text-2xl">⛅</div>
        <div className="text-left">
          <div className="flex items-center gap-1.5">
            <span className="text-sm font-semibold text-white">22°C</span>
            <span className="rounded bg-sky-500/15 dark:bg-sky-500/20 px-1 text-[9px] font-medium text-sky-700 dark:text-sky-300">
              {lang === "en" ? "Cloudy" : "多云"}
            </span>
          </div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Open-Meteo · 30m refresh" : "Open-Meteo · 30分钟更新"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Stocks: Live market ticker updated every ~20s."
              : "股票：第三方行情约 20s 刷新，红涨绿跌。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="text-left">
          <div className="flex items-center gap-1.5">
            <span className="text-xs font-semibold text-white">AAPL</span>
            <span className="text-xs font-semibold text-emerald-400 font-mono">+2.14%</span>
          </div>
          <div className="text-[10px] text-white/50">$234.50</div>
        </div>
        <svg className="h-6 w-14" viewBox="0 0 60 24" fill="none">
          <path d="M1 18 C12 16 18 8 28 11 S42 4 58 2" stroke="#10b981" strokeWidth="2" strokeLinecap="round" />
        </svg>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "System Activity: Sampled CPU and physical RAM loads via PerformanceCounter."
              : "系统状态：基于 PerformanceCounter 采样 CPU 与物理内存占用。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="relative h-7 w-7">
          <svg className="h-7 w-7 -rotate-90 transform" viewBox="0 0 24 24">
            <circle cx="12" cy="12" r="10" stroke="rgba(255,255,255,0.12)" strokeWidth="2.5" fill="none" />
            <circle
              cx="12"
              cy="12"
              r="10"
              stroke="#a855f7"
              strokeWidth="2.5"
              strokeDasharray="62.8"
              strokeDashoffset="48"
              strokeLinecap="round"
              fill="none"
            />
          </svg>
          <svg className="absolute inset-1 h-5 w-5 -rotate-90 transform" viewBox="0 0 16 16">
            <circle cx="8" cy="8" r="6" stroke="rgba(255,255,255,0.12)" strokeWidth="2" fill="none" />
            <circle
              cx="8"
              cy="8"
              r="6"
              stroke="#38bdf8"
              strokeWidth="2"
              strokeDasharray="37.7"
              strokeDashoffset="20"
              strokeLinecap="round"
              fill="none"
            />
          </svg>
        </div>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">CPU 18% · RAM 42%</div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Dual load rings" : "点开查看双圆环与内存"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Alarm: Set next local alert time with chime and notifications."
              : "闹钟：设定下一次本地提醒时刻，到点播放提示音。"
          )
        }
        className="flex h-16 items-center gap-2.5 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">⏰</span>
        <div className="text-left">
          <div className="text-xs font-semibold text-white font-mono">
            {lang === "en" ? "07:30 Standup" : "07:30 晨会"}
          </div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Tray chime & notification" : "托盘发音与通知"}
          </div>
        </div>
      </div>
    </>
  );
}

function RowTwoWidgets({
  lang,
  timerRunning,
  timerFormatted,
  toggleTimer,
  whiteNoisePlaying,
  toggleWhiteNoise,
  onSelect,
}: {
  lang: Lang;
  timerRunning: boolean;
  timerFormatted: string;
  toggleTimer: () => void;
  whiteNoisePlaying: boolean;
  toggleWhiteNoise: () => void;
  onSelect: (info: string) => void;
}) {
  return (
    <>
      <div
        onClick={() => {
          toggleTimer();
          onSelect(
            lang === "en"
              ? "Focus Timer: Click to start or pause the 25-minute Pomodoro timer."
              : "专注计时器：点击可直接开始/暂停 25 分钟番茄钟。"
          );
        }}
        className="flex h-16 items-center gap-3 rounded-2xl border border-blue-500/20 dark:border-blue-500/30 bg-blue-50/80 dark:bg-blue-950/40 px-4 py-2.5 backdrop-blur-md transition-all hover:border-blue-400 hover:scale-105 cursor-pointer shadow-sm dark:shadow-none"
      >
        <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-blue-500/15 dark:bg-blue-500/20 text-blue-600 dark:text-blue-300">
          <span className={`h-2 w-2 rounded-full bg-accent-blue dark:bg-accent-cyan ${timerRunning ? "animate-ping" : ""}`} />
        </div>
        <div className="text-left">
          <div className="text-xs font-semibold text-slate-900 dark:text-white font-mono">
            {timerFormatted}{" "}
            {timerRunning
              ? lang === "en"
                ? "Focusing"
                : "专注中"
              : lang === "en"
              ? "Paused"
              : "已暂停"}
          </div>
          <div className="text-[10px] text-blue-600 dark:text-blue-300/80 font-medium">
            {lang === "en" ? "Pomodoro · Click to toggle" : "番茄钟 · 点击开始/暂停"}
          </div>
        </div>
      </div>

      <div
        onClick={() => {
          toggleWhiteNoise();
          onSelect(
            lang === "en"
              ? "White Noise: Local multi-track sound mixer, click to toggle audio."
              : "白噪音：本地多轨自然混音，点击可启闭声波。"
          );
        }}
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">🌧️</span>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">
            {lang === "en" ? "Rain + Café" : "雨声 + 咖啡馆"}
          </div>
          <div className="flex items-center gap-1 mt-0.5">
            <span className="text-[10px] text-white/50">
              {whiteNoisePlaying
                ? lang === "en"
                  ? "Playing"
                  : "播放中"
                : lang === "en"
                ? "Paused"
                : "已暂停"}
            </span>
            <div className="flex items-end gap-0.5 h-2.5">
              <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-2 animate-pulse" : "h-1"}`} />
              <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-3 animate-pulse" : "h-1"}`} />
              <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-1.5 animate-pulse" : "h-1"}`} />
              <span className={`w-0.5 bg-accent-cyan rounded-full ${whiteNoisePlaying ? "h-2.5 animate-pulse" : "h-1"}`} />
            </div>
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Network: Real-time traffic rate with 60s sparkline."
              : "网络：适配器实时速率差值，点开展示 60 秒波动图。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="text-left">
          <div className="text-xs font-semibold text-white font-mono">↓ 2.4 MB/s</div>
          <div className="text-[10px] text-white/50 font-mono">↑ 480 KB/s</div>
        </div>
        <svg className="h-5 w-12" viewBox="0 0 50 20" fill="none">
          <path d="M1 15 L12 12 L24 16 L36 6 L49 10" stroke="#38bdf8" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Power: System battery and Bluetooth peripheral battery monitor."
              : "电源：系统电池与蓝牙外设电量监视。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-400">
          ⚡
        </div>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">
            {lang === "en" ? "88% Charging" : "88% 充电中"}
          </div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "BT Keyboard 90%" : "外设键盘 90%"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Bing Wallpaper: Daily official UHD wallpapers, one-click to set desktop."
              : "Bing 每日壁纸：近 8 日 Bing 官方超清壁纸，一键应用设为桌面。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-3.5 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="h-10 w-14 rounded-lg bg-gradient-to-tr from-indigo-500 via-sky-400 to-amber-300 shadow-inner flex items-center justify-center text-[10px] font-bold text-black/70">
          UHD
        </div>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">
            {lang === "en" ? "Bing Daily Wallpaper" : "Bing 每日壁纸"}
          </div>
          <div className="text-[10px] text-accent-cyan hover:underline">
            {lang === "en" ? "Apply as wallpaper" : "一键应用为桌面"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Schedule Countdown: Local handwritten milestones and target dates."
              : "日程倒数：本机手写的今日事项与倒数日。"
          )
        }
        className="flex h-16 items-center gap-2.5 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">🎯</span>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">
            {lang === "en" ? "3 days left" : "还有 3 天"}
          </div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Major release milestone" : "大版本发布里程碑"}
          </div>
        </div>
      </div>
    </>
  );
}

function RowThreeWidgets({
  lang,
  onSelect,
}: {
  lang: Lang;
  onSelect: (info: string) => void;
}) {
  return (
    <>
      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Disk Usage: Periodic sampling of fixed SSD free space."
              : "磁盘：定时采样固定硬盘剩余空间，不扫描移动盘。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="text-left">
          <div className="text-xs font-semibold text-white">NVMe SSD 512G</div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "178 GB free (65%)" : "剩余 178 GB (65%)"}
          </div>
        </div>
        <div className="h-2 w-14 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[65%] rounded-full bg-accent-blue" />
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "GPU: Dedicated graphics card load and temperature state."
              : "GPU：显卡占用率与轻重载检测。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-purple-500/20 text-purple-700 dark:text-purple-300 text-xs font-bold font-mono">
          GPU
        </div>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">RTX 4080 · 14%</div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Silent idle" : "低功耗待机"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Time Progress: Year/month/day elapsed percentage."
              : "时间进度：年 / 月 / 日已过比例。"
          )
        }
        className="flex h-16 items-center gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <div className="text-left">
          <div className="text-xs font-semibold text-white">
            {lang === "en" ? "2026 is 76.4% passed" : "2026 年已过 76.4%"}
          </div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Cherish today" : "珍惜今日分秒"}
          </div>
        </div>
        <div className="h-2 w-16 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[76.4%] rounded-full bg-gradient-to-r from-sky-400 to-indigo-500" />
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Stopwatch: High-precision lap timer, starts on click."
              : "秒表：高精度本地秒表，支持分段计圈。"
          )
        }
        className="flex h-16 items-center gap-2.5 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">⏱️</span>
        <div className="text-left">
          <div className="text-xs font-semibold text-white font-mono">00:14.28</div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Precision lap timer" : "高精度本地计圈"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Countdown: Single countdown and named presets with auto alerts."
              : "倒计时：单次倒数预设，到零提醒通知。"
          )
        }
        className="flex h-16 items-center gap-2.5 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">⏳</span>
        <div className="text-left">
          <div className="text-xs font-semibold text-white font-mono">08:30</div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Presentation preset" : "演讲倒数预设"}
          </div>
        </div>
      </div>

      <div
        onClick={() =>
          onSelect(
            lang === "en"
              ? "Dual Timezone: Local clock displays a secondary global timezone."
              : "双时区：本地时钟可选第二时区，方便跨国远程团队。"
          )
        }
        className="flex h-16 items-center gap-2.5 rounded-2xl border border-white/10 bg-[#16161c]/90 px-4 py-2.5 backdrop-blur-md transition-all hover:border-white/30 hover:scale-105 cursor-pointer"
      >
        <span className="text-lg">🌍</span>
        <div className="text-left">
          <div className="text-xs font-semibold text-white">
            {lang === "en" ? "San Francisco 19:42" : "旧金山 19:42"}
          </div>
          <div className="text-[10px] text-white/50">
            {lang === "en" ? "Global remote team" : "跨国远程团队"}
          </div>
        </div>
      </div>
    </>
  );
}
