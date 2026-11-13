import { useState, useEffect } from "react";
import { clockLabel, useNow, weekdayLabel } from "@/lib/clock";
import { useI18n } from "@/lib/i18n";
import { useTheme } from "@/lib/theme";

type Position = "top" | "left" | "right";
type Material = "mica" | "frosted" | "glass";

export function IslandStage() {
  const { lang, t } = useI18n();
  const { resolvedTheme } = useTheme();
  const now = useNow();

  const [position, setPosition] = useState<Position>("top");
  const [material, setMaterial] = useState<Material>("mica");
  const [stageTheme, setStageTheme] = useState<"auto" | "light" | "dark">("auto");
  const [autoHide, setAutoHide] = useState(false);
  const [isHovered, setIsHovered] = useState(false);
  const [noteOpen, setNoteOpen] = useState(false);
  const [noteText, setNoteText] = useState(
    lang === "en" ? "2:00 PM Design Review" : "下午 2:00 产品评审"
  );
  const [timerRunning, setTimerRunning] = useState(false);
  const [timerSeconds, setTimerSeconds] = useState(25 * 60);

  const isStageLight = stageTheme === "auto" ? resolvedTheme === "light" : stageTheme === "light";
  const isSide = position === "left" || position === "right";

  // Sync note sample text if language changes while untouched
  useEffect(() => {
    setNoteText(lang === "en" ? "2:00 PM Design Review" : "下午 2:00 产品评审");
  }, [lang]);

  // Timer simulation
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

  const isIslandHidden = autoHide && !isHovered && !noteOpen;

  return (
    <div className="w-full">
      {/* Interactive Controls Bar (Toolbar) */}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200/80 dark:border-white/10 bg-white/80 dark:bg-[#16161c]/90 p-3 px-4 backdrop-blur-xl shadow-sm dark:shadow-none transition-colors">
        <div className="flex flex-wrap items-center gap-4 text-xs">
          {/* Position Picker */}
          <div className="flex items-center gap-1.5">
            <span className="text-slate-500 dark:text-mute">{t.simulator.edge}</span>
            <div className="inline-flex rounded-lg bg-slate-200/70 dark:bg-black/40 p-0.5 border border-slate-300/60 dark:border-white/5">
              <button
                type="button"
                onClick={() => setPosition("top")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  position === "top"
                    ? "bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm"
                    : "text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {t.simulator.topCenter}
              </button>
              <button
                type="button"
                onClick={() => setPosition("left")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  position === "left"
                    ? "bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm"
                    : "text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {t.simulator.leftEdge}
              </button>
              <button
                type="button"
                onClick={() => setPosition("right")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  position === "right"
                    ? "bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm"
                    : "text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {t.simulator.rightEdge}
              </button>
            </div>
          </div>

          {/* Material Switcher */}
          <div className="hidden sm:flex items-center gap-1.5">
            <span className="text-slate-500 dark:text-mute">{t.simulator.material}</span>
            <div className="inline-flex rounded-lg bg-slate-200/70 dark:bg-black/40 p-0.5 border border-slate-300/60 dark:border-white/5">
              <button
                type="button"
                onClick={() => setMaterial("mica")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  material === "mica"
                    ? "bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm"
                    : "text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {t.simulator.mica}
              </button>
              <button
                type="button"
                onClick={() => setMaterial("frosted")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  material === "frosted"
                    ? "bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm"
                    : "text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {t.simulator.frosted}
              </button>
              <button
                type="button"
                onClick={() => setMaterial("glass")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  material === "glass"
                    ? "bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm"
                    : "text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {t.simulator.glass}
              </button>
            </div>
          </div>

          {/* Stage Desktop Theme Switcher */}
          <div className="hidden md:flex items-center gap-1.5">
            <span className="text-slate-500 dark:text-mute">{t.simulator.themeMode}</span>
            <div className="inline-flex rounded-lg bg-slate-200/70 dark:bg-black/40 p-0.5 border border-slate-300/60 dark:border-white/5">
              <button
                type="button"
                onClick={() => setStageTheme(isStageLight ? "dark" : "light")}
                className="flex items-center gap-1 rounded-md px-2.5 py-1 font-medium bg-white dark:bg-white/15 text-slate-900 dark:text-white shadow-sm transition-colors"
                title="Toggle Stage Theme"
              >
                <span>{isStageLight ? "☀️" : "🌙"}</span>
                <span>{isStageLight ? t.simulator.themeLight : t.simulator.themeDark}</span>
              </button>
            </div>
          </div>
        </div>

        {/* Auto-hide toggle */}
        <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-600 dark:text-mute hover:text-slate-900 dark:hover:text-white select-none transition-colors">
          <input
            type="checkbox"
            checked={autoHide}
            onChange={(e) => setAutoHide(e.target.checked)}
            className="h-3.5 w-3.5 rounded border-slate-300 dark:border-white/20 bg-white dark:bg-black/40 text-accent-blue focus:ring-0"
          />
          <span>{t.simulator.autoHide}</span>
        </label>
      </div>

      {/* Virtual Windows Desktop Screen */}
      <div
        className={`desk-simulator ${isStageLight ? "desk-light" : ""}`}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        {/* Background Simulated App Window (Code editor / Workspace) */}
        <div
          className={`absolute left-[12%] top-[14%] right-[12%] bottom-[12%] rounded-xl border shadow-xl backdrop-blur-md overflow-hidden pointer-events-none transition-all duration-300 ${
            isStageLight
              ? "border-slate-300/80 bg-white/75 text-slate-800 shadow-blue-900/5"
              : "border-white/10 bg-[#12131a]/80 text-white/30 opacity-85 shadow-2xl"
          }`}
        >
          {/* Window title bar */}
          <div
            className={`flex h-9 items-center justify-between border-b px-3.5 transition-colors ${
              isStageLight
                ? "border-slate-200/80 bg-slate-100/90 text-slate-700"
                : "border-white/5 bg-[#171822]/90 text-white/60"
            }`}
          >
            <div className="flex items-center gap-2">
              <span className="h-3 w-3 rounded-full bg-[#38bdf8]/60" />
              <span className={`text-xs font-mono font-medium ${isStageLight ? "text-slate-700" : "text-white/60"}`}>
                Workspace — Project_WinDock
              </span>
            </div>
            <div className={`flex items-center gap-3 text-xs ${isStageLight ? "text-slate-400" : "text-white/30"}`}>
              <span>—</span>
              <span>□</span>
              <span>✕</span>
            </div>
          </div>
          {/* Mock editor body */}
          <div className={`p-5 font-mono text-[11px] leading-relaxed ${isStageLight ? "text-slate-600" : "text-white/30"}`}>
            <p>
              <span className={isStageLight ? "text-purple-600 font-semibold" : "text-purple-400"}>const</span> dock ={" "}
              <span className={isStageLight ? "text-blue-600 font-semibold" : "text-blue-400"}>new</span>{" "}
              <span className={isStageLight ? "text-emerald-600 font-semibold" : "text-emerald-400"}>SideBoxIsland</span>(&#123;
            </p>
            <p className="pl-4">
              edge: <span className={isStageLight ? "text-amber-700 font-medium" : "text-amber-300"}>"{position}"</span>,
            </p>
            <p className="pl-4">
              material: <span className={isStageLight ? "text-amber-700 font-medium" : "text-amber-300"}>"{material}"</span>,
            </p>
            <p className="pl-4">
              theme: <span className={isStageLight ? "text-purple-600 font-medium" : "text-purple-400"}>"{isStageLight ? "light" : "dark"}"</span>,
            </p>
            <p>&#125;);</p>
          </div>
        </div>

        {/* Subtle Auto-hide Edge Hairline Strip (DockTokens.AutoHideStrip = 4) */}
        {isIslandHidden && (
          <div
            className={`absolute pointer-events-none z-30 transition-all duration-300 ${
              position === "top"
                ? "top-0 left-1/2 -translate-x-1/2 h-1 w-20 rounded-b-sm bg-accent-blue shadow-[0_0_8px_rgba(59,130,246,0.6)]"
                : position === "left"
                ? "left-0 top-1/2 -translate-y-1/2 w-1 h-20 rounded-r-sm bg-accent-blue shadow-[0_0_8px_rgba(59,130,246,0.6)]"
                : "right-0 top-1/2 -translate-y-1/2 w-1 h-20 rounded-l-sm bg-accent-blue shadow-[0_0_8px_rgba(59,130,246,0.6)]"
            }`}
          />
        )}

        {/* The SideBox Island (Interactive Component) */}
        <div
          className={`sim-island pos-${position} material-${material} ${
            isSide ? "items-stretch" : "items-center"
          } ${isIslandHidden ? "is-hidden" : ""}`}
        >
          {/* App 1: Browser */}
          <div
            className={`group relative flex h-10 ${
              isSide ? "w-full" : "w-10"
            } items-center justify-center rounded-lg transition-all cursor-pointer ${
              isStageLight
                ? "bg-black/5 hover:bg-black/10"
                : "bg-white/5 hover:bg-white/15"
            }`}
            title={t.simulator.browserTip}
          >
            <BrowserIcon />
            <span
              className={`absolute ${
                isSide ? "right-1.5 top-1/2 -translate-y-1/2" : "bottom-1"
              } h-1 w-1 rounded-full bg-accent-cyan`}
            />
          </div>

          {/* App 2: Chat */}
          <div
            className={`group relative flex h-10 ${
              isSide ? "w-full" : "w-10"
            } items-center justify-center rounded-lg transition-all cursor-pointer ${
              isStageLight
                ? "bg-black/5 hover:bg-black/10"
                : "bg-white/5 hover:bg-white/15"
            }`}
            title={t.simulator.chatTip}
          >
            <ChatIcon />
            <span
              className={`absolute ${
                isSide ? "right-1.5 top-1/2 -translate-y-1/2" : "bottom-1"
              } h-1 w-1 rounded-full bg-emerald-500`}
            />
          </div>

          {/* App 3: Code Editor */}
          <div
            className={`group relative flex h-10 ${
              isSide ? "w-full" : "w-10"
            } items-center justify-center rounded-lg transition-all cursor-pointer ${
              isStageLight
                ? "bg-black/5 hover:bg-black/10"
                : "bg-white/5 hover:bg-white/15"
            }`}
            title={t.simulator.ideTip}
          >
            <CodeIcon />
            <span
              className={`absolute ${
                isSide ? "right-1.5 top-1/2 -translate-y-1/2" : "bottom-1"
              } h-1 w-1 rounded-full bg-blue-500`}
            />
          </div>

          {/* Widget: Clock */}
          <div
            className={`flex ${
              isSide
                ? "w-full flex-col justify-center items-center py-1.5 h-auto min-h-[42px]"
                : "h-10 items-center gap-2 px-3"
            } rounded-lg transition-all cursor-default select-none ${
              isStageLight
                ? "bg-black/5 hover:bg-black/10 text-slate-800"
                : "bg-white/8 hover:bg-white/15 text-white"
            }`}
            title={t.simulator.clockTip}
          >
            {!isSide && <ClockIcon isLight={isStageLight} />}
            <div className={`flex ${isSide ? "items-center gap-1.5" : "flex-col text-left"}`}>
              <span className="text-xs font-semibold leading-tight">{clockLabel(now)}</span>
              <span className={`text-[10px] leading-tight ${isStageLight ? "text-slate-500" : "text-white/50"}`}>
                {weekdayLabel(now, lang)}
              </span>
            </div>
          </div>

          {/* Widget: Sticky Note (Interactive click to edit!) */}
          <div className={`relative ${isSide ? "w-full" : ""}`}>
            <button
              type="button"
              onClick={() => setNoteOpen(!noteOpen)}
              className={`flex h-10 ${
                isSide ? "w-full px-2 justify-center" : "px-3"
              } items-center gap-1.5 rounded-lg bg-[#f6e27a] text-[#1c1b19] transition-all hover:brightness-105 active:scale-95 shadow-sm overflow-hidden`}
              title={t.simulator.noteTip}
            >
              <div className="shrink-0">
                <NoteIcon />
              </div>
              <span
                className={`${
                  isSide ? "w-full truncate text-center" : "max-w-[110px] truncate"
                } text-xs font-medium`}
              >
                {noteText}
              </span>
            </button>

            {/* Popover note editing panel */}
            {noteOpen && (
              <div
                className={`absolute z-50 w-56 rounded-xl border border-amber-300/40 bg-[#fef8d3] p-3 shadow-2xl text-[#1c1b19] animate-in fade-in zoom-in-95 ${
                  position === "left"
                    ? "left-full ml-3 top-0"
                    : position === "right"
                    ? "right-full mr-3 top-0"
                    : "left-0 top-12"
                }`}
              >
                <div className="flex items-center justify-between pb-1.5 border-b border-black/10">
                  <span className="text-[11px] font-semibold text-black/70">
                    {t.simulator.noteTitle}
                  </span>
                  <button
                    type="button"
                    onClick={() => setNoteOpen(false)}
                    className="text-black/40 hover:text-black text-xs font-bold"
                  >
                    ✕
                  </button>
                </div>
                <textarea
                  value={noteText}
                  onChange={(e) => setNoteText(e.target.value)}
                  rows={2}
                  className="mt-2 w-full resize-none rounded-md bg-transparent p-1 text-xs outline-none text-[#1c1b19]"
                  placeholder={t.simulator.notePlaceholder}
                  autoFocus
                />
              </div>
            )}
          </div>

          {/* Widget: Focus Timer (Interactive Pomodoro) */}
          <button
            type="button"
            onClick={() => setTimerRunning(!timerRunning)}
            className={`flex h-10 ${
              isSide ? "w-full px-2 justify-center" : "px-3"
            } items-center gap-1.5 rounded-lg border transition-all ${
              isStageLight
                ? "bg-blue-500/10 border-blue-500/25 text-blue-900 hover:bg-blue-500/20"
                : "bg-accent-blue/20 border-accent-blue/30 text-white hover:bg-accent-blue/30"
            }`}
            title={t.simulator.timerTip}
          >
            <span className="relative flex h-2 w-2 shrink-0">
              {timerRunning && (
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-sky-400 opacity-75" />
              )}
              <span className="relative inline-flex rounded-full h-2 w-2 bg-accent-blue" />
            </span>
            <span className="text-xs font-mono font-bold tracking-wider">
              {formatTimer(timerSeconds)}
            </span>
            {!isSide && (
              <span className={`text-[10px] ${isStageLight ? "text-blue-700/80" : "text-white/50"}`}>
                {timerRunning ? t.simulator.timerPause : t.simulator.timerStart}
              </span>
            )}
          </button>

          {/* Widget: System Activity (CPU + RAM Dual Rings) */}
          <div
            className={`flex h-10 ${
              isSide ? "w-full px-2 justify-center" : "hidden md:flex px-2.5"
            } items-center gap-2 rounded-lg ${
              isStageLight ? "bg-black/5 text-slate-800" : "bg-white/8 text-white"
            }`}
            title={t.simulator.sysTip}
          >
            <div className="relative h-6 w-6 shrink-0">
              {/* Outer ring: CPU */}
              <svg className="h-6 w-6 -rotate-90 transform" viewBox="0 0 24 24">
                <circle
                  cx="12"
                  cy="12"
                  r="10"
                  stroke={isStageLight ? "rgba(0,0,0,0.12)" : "rgba(255,255,255,0.15)"}
                  strokeWidth="2.4"
                  fill="none"
                />
                <circle
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="#a855f7"
                  strokeWidth="2.4"
                  strokeDasharray="62.8"
                  strokeDashoffset="48"
                  strokeLinecap="round"
                  fill="none"
                />
              </svg>
              {/* Inner ring: RAM */}
              <svg className="absolute inset-1 h-4 w-4 -rotate-90 transform" viewBox="0 0 16 16">
                <circle
                  cx="8"
                  cy="8"
                  r="6"
                  stroke={isStageLight ? "rgba(0,0,0,0.12)" : "rgba(255,255,255,0.15)"}
                  strokeWidth="2"
                  fill="none"
                />
                <circle
                  cx="8"
                  cy="8"
                  r="6"
                  stroke="#38bdf8"
                  strokeWidth="2"
                  strokeDasharray="37.7"
                  strokeDashoffset="22"
                  strokeLinecap="round"
                  fill="none"
                />
              </svg>
            </div>
            <div className={`text-[10px] leading-tight font-medium ${isStageLight ? "text-slate-700" : "text-white/70"}`}>
              <div>CPU 18%</div>
              <div>RAM 42%</div>
            </div>
          </div>
        </div>

        {/* Windows 11 Centered Taskbar at Bottom */}
        <div className="win11-taskbar">
          {/* Centered Icons */}
          <div className="flex items-center gap-1.5 sm:gap-2">
            {/* Windows Start Button */}
            <div
              className={`flex h-7 w-7 items-center justify-center rounded-md transition-colors cursor-pointer ${
                isStageLight ? "hover:bg-black/10" : "hover:bg-white/10"
              }`}
            >
              <svg width="15" height="15" viewBox="0 0 16 16" fill="none">
                <rect x="1" y="1" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
                <rect x="8.8" y="1" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
                <rect x="1" y="8.8" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
                <rect x="8.8" y="8.8" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
              </svg>
            </div>
            {/* Search */}
            <div
              className={`hidden sm:flex h-7 items-center gap-1.5 rounded-full px-2.5 text-[11px] transition-colors ${
                isStageLight ? "bg-black/5 text-slate-600" : "bg-white/10 text-white/50"
              }`}
            >
              <svg width="12" height="12" viewBox="0 0 16 16" fill="currentColor">
                <path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001c.03.04.062.078.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1.007 1.007 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0z" />
              </svg>
              <span>{t.simulator.search}</span>
            </div>
            {/* Task View */}
            <div
              className={`flex h-7 w-7 items-center justify-center rounded-md transition-colors ${
                isStageLight ? "hover:bg-black/10" : "hover:bg-white/10"
              }`}
            >
              <span
                className={`h-3.5 w-3.5 rounded border ${
                  isStageLight ? "border-slate-600" : "border-white/60"
                }`}
              />
            </div>
            {/* Explorer */}
            <div
              className={`flex h-7 w-7 items-center justify-center rounded-md transition-colors ${
                isStageLight ? "hover:bg-black/10" : "hover:bg-white/10"
              }`}
            >
              <span className="h-3.5 w-3.5 rounded bg-amber-400" />
            </div>
            {/* Edge */}
            <div
              className={`flex h-7 w-7 items-center justify-center rounded-md transition-colors ${
                isStageLight ? "hover:bg-black/10" : "hover:bg-white/10"
              }`}
            >
              <span className="h-3.5 w-3.5 rounded-full bg-sky-500" />
            </div>
          </div>

          {/* System Tray (Right side) */}
          <div
            className={`absolute right-4 flex items-center gap-3 text-[10px] font-medium transition-colors ${
              isStageLight ? "text-slate-700" : "text-white/60"
            }`}
          >
            <span>22°C</span>
            <span>ENG</span>
            <div className="flex flex-col text-right leading-none">
              <span>{clockLabel(now)}</span>
              <span className={`text-[9px] mt-0.5 ${isStageLight ? "text-slate-500" : "text-white/40"}`}>
                2026/10/7
              </span>
            </div>
          </div>
        </div>
      </div>

      <p className="mt-3 text-center text-xs text-mute">{t.simulator.tip}</p>
    </div>
  );
}

// Icon helpers
function BrowserIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-sky-500 dark:text-sky-400">
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  );
}

function ChatIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-emerald-500 dark:text-emerald-400">
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </svg>
  );
}

function CodeIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-blue-500 dark:text-blue-400">
      <polyline points="16 18 22 12 16 6" />
      <polyline points="8 6 2 12 8 18" />
    </svg>
  );
}

function ClockIcon({ isLight }: { isLight?: boolean }) {
  return (
    <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" className={isLight ? "text-slate-700" : "text-white/80"}>
      <circle cx="8" cy="8" r="6" />
      <path d="M8 5v3l2 1.5" strokeLinecap="round" />
    </svg>
  );
}

function NoteIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" className="text-[#1c1b19]">
      <path d="M3 2h7l4 4v8H3V2z" />
      <path d="M10 2v4h4" />
    </svg>
  );
}
