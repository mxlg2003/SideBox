import { useState, useEffect } from "react";
import { clockLabel, useNow, weekdayLabel } from "@/lib/clock";
import { useI18n } from "@/lib/i18n";

type Position = "top" | "left" | "right";
type Profile = "general" | "deep-work";
type Material = "mica" | "frosted" | "glass";

export function IslandStage() {
  const { lang, t } = useI18n();
  const now = useNow();
  const [position, setPosition] = useState<Position>("top");
  const [profile, setProfile] = useState<Profile>("general");
  const [material, setMaterial] = useState<Material>("mica");
  const [autoHide, setAutoHide] = useState(false);
  const [isHovered, setIsHovered] = useState(false);
  const [noteOpen, setNoteOpen] = useState(false);
  const [noteText, setNoteText] = useState(
    lang === "en" ? "2:00 PM Design Review" : "下午 2:00 产品评审"
  );
  const [timerRunning, setTimerRunning] = useState(false);
  const [timerSeconds, setTimerSeconds] = useState(25 * 60);
  const [whiteNoisePlaying, setWhiteNoisePlaying] = useState(true);

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
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/10 bg-[#16161c]/90 p-3 px-4 backdrop-blur-xl">
        <div className="flex flex-wrap items-center gap-4 text-xs">
          {/* Position Picker */}
          <div className="flex items-center gap-1.5">
            <span className="text-mute">{t.simulator.edge}</span>
            <div className="inline-flex rounded-lg bg-black/40 p-0.5 border border-white/5">
              <button
                type="button"
                onClick={() => setPosition("top")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  position === "top"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                {t.simulator.topCenter}
              </button>
              <button
                type="button"
                onClick={() => setPosition("left")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  position === "left"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                {t.simulator.leftEdge}
              </button>
              <button
                type="button"
                onClick={() => setPosition("right")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  position === "right"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                {t.simulator.rightEdge}
              </button>
            </div>
          </div>

          {/* Profile Switcher */}
          <div className="flex items-center gap-1.5">
            <span className="text-mute">{t.simulator.profile}</span>
            <div className="inline-flex rounded-lg bg-black/40 p-0.5 border border-white/5">
              <button
                type="button"
                onClick={() => setProfile("general")}
                className={`flex items-center gap-1.5 rounded-md px-2.5 py-1 font-medium transition-colors ${
                  profile === "general"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
                {t.simulator.general}
              </button>
              <button
                type="button"
                onClick={() => setProfile("deep-work")}
                className={`flex items-center gap-1.5 rounded-md px-2.5 py-1 font-medium transition-colors ${
                  profile === "deep-work"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                <span className="h-1.5 w-1.5 rounded-full bg-blue-400" />
                {t.simulator.deepWork}
              </button>
            </div>
          </div>

          {/* Material Switcher */}
          <div className="hidden sm:flex items-center gap-1.5">
            <span className="text-mute">{t.simulator.material}</span>
            <div className="inline-flex rounded-lg bg-black/40 p-0.5 border border-white/5">
              <button
                type="button"
                onClick={() => setMaterial("mica")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  material === "mica"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                {t.simulator.mica}
              </button>
              <button
                type="button"
                onClick={() => setMaterial("frosted")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  material === "frosted"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                {t.simulator.frosted}
              </button>
              <button
                type="button"
                onClick={() => setMaterial("glass")}
                className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                  material === "glass"
                    ? "bg-white/15 text-white shadow-sm"
                    : "text-mute hover:text-white"
                }`}
              >
                {t.simulator.glass}
              </button>
            </div>
          </div>
        </div>

        {/* Auto-hide toggle */}
        <label className="flex cursor-pointer items-center gap-2 text-xs text-mute hover:text-white select-none">
          <input
            type="checkbox"
            checked={autoHide}
            onChange={(e) => setAutoHide(e.target.checked)}
            className="h-3.5 w-3.5 rounded border-white/20 bg-black/40 text-accent-blue focus:ring-0"
          />
          <span>{t.simulator.autoHide}</span>
        </label>
      </div>

      {/* Virtual Windows 11 Desktop Screen */}
      <div
        className="desk-simulator"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        {/* Background Simulated App Window (Code editor / Workspace) */}
        <div className="absolute left-[12%] top-[14%] right-[12%] bottom-[12%] rounded-xl border border-white/10 bg-[#12131a]/80 shadow-2xl backdrop-blur-md overflow-hidden pointer-events-none opacity-85">
          {/* Window title bar */}
          <div className="flex h-9 items-center justify-between border-b border-white/5 bg-[#171822]/90 px-3.5">
            <div className="flex items-center gap-2">
              <span className="h-3 w-3 rounded-full bg-[#38bdf8]/40" />
              <span className="text-xs text-white/60 font-mono">Workspace — Project_SideBox</span>
            </div>
            <div className="flex items-center gap-3 text-white/30 text-xs">
              <span>—</span>
              <span>□</span>
              <span>✕</span>
            </div>
          </div>
          {/* Mock editor body */}
          <div className="p-5 font-mono text-[11px] leading-relaxed text-white/30">
            <p>
              <span className="text-purple-400">const</span> dock ={" "}
              <span className="text-blue-400">new</span>{" "}
              <span className="text-emerald-400">SideBoxIsland</span>(&#123;
            </p>
            <p className="pl-4">
              edge: <span className="text-amber-300">"{position}"</span>,
            </p>
            <p className="pl-4">
              profile: <span className="text-amber-300">"{profile}"</span>,
            </p>
            <p className="pl-4">
              material: <span className="text-amber-300">"{material}"</span>,
            </p>
            <p className="pl-4">
              topmost: <span className="text-purple-400">true</span>,
            </p>
            <p>&#125;);</p>
          </div>
        </div>

        {/* The SideBox Island (Interactive Component) */}
        <div
          className={`sim-island pos-${position} material-${material} ${
            isIslandHidden ? "is-hidden" : ""
          }`}
        >
          {/* Auto-hide indicator line when hidden */}
          {isIslandHidden && (
            <div className="absolute inset-0 bg-accent-blue/80 animate-pulse rounded-full" />
          )}

          {/* Profile: General */}
          {profile === "general" && (
            <>
              {/* App 1: Browser */}
              <div
                className="group relative flex h-10 w-10 items-center justify-center rounded-lg bg-white/5 hover:bg-white/15 transition-all cursor-pointer"
                title={t.simulator.browserTip}
              >
                <BrowserIcon />
                <span className="absolute bottom-1 h-1 w-1 rounded-full bg-accent-cyan" />
              </div>

              {/* App 2: WeChat / Chat */}
              <div
                className="group relative flex h-10 w-10 items-center justify-center rounded-lg bg-white/5 hover:bg-white/15 transition-all cursor-pointer"
                title={t.simulator.chatTip}
              >
                <ChatIcon />
                <span className="absolute bottom-1 h-1 w-1 rounded-full bg-emerald-400" />
              </div>

              {/* Folder Pin */}
              <div
                className="group relative flex h-10 w-10 items-center justify-center rounded-lg bg-amber-500/15 border border-amber-500/30 hover:bg-amber-500/25 transition-all cursor-pointer"
                title={t.simulator.folderTip}
              >
                <span className="text-xs font-bold text-amber-300">P</span>
              </div>

              {/* Widget: Clock */}
              <div
                className="flex h-10 items-center gap-2 rounded-lg bg-white/8 px-3 text-white transition-all hover:bg-white/15 cursor-default select-none"
                title={t.simulator.clockTip}
              >
                <ClockIcon />
                <div className="flex flex-col text-left">
                  <span className="text-xs font-semibold leading-tight">{clockLabel(now)}</span>
                  <span className="text-[10px] text-white/50 leading-tight">
                    {weekdayLabel(now, lang)}
                  </span>
                </div>
              </div>

              {/* Widget: Sticky Note (Interactive click to edit!) */}
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setNoteOpen(!noteOpen)}
                  className="flex h-10 items-center gap-1.5 rounded-lg bg-[#f6e27a] px-3 text-[#1c1b19] transition-all hover:brightness-105 active:scale-95"
                  title={t.simulator.noteTip}
                >
                  <NoteIcon />
                  <span className="max-w-[110px] truncate text-xs font-medium">{noteText}</span>
                </button>

                {/* Popover note editing panel */}
                {noteOpen && (
                  <div className="absolute left-0 top-12 z-50 w-56 rounded-xl border border-amber-300/40 bg-[#fef8d3] p-3 shadow-2xl text-[#1c1b19] animate-in fade-in zoom-in-95">
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

              {/* Widget: White Noise */}
              <button
                type="button"
                onClick={() => setWhiteNoisePlaying(!whiteNoisePlaying)}
                className="flex h-10 items-center gap-1.5 rounded-lg bg-white/8 px-2.5 text-white/90 hover:bg-white/15 transition-all"
                title={t.simulator.whiteNoiseTip}
              >
                <span className="text-xs">🌧️</span>
                <div className="flex items-end gap-0.5 h-3">
                  <span
                    className={`w-0.5 rounded-full bg-accent-cyan transition-all duration-300 ${
                      whiteNoisePlaying ? "h-3 animate-pulse" : "h-1"
                    }`}
                  />
                  <span
                    className={`w-0.5 rounded-full bg-accent-cyan transition-all duration-300 ${
                      whiteNoisePlaying ? "h-2 animate-pulse" : "h-1"
                    }`}
                  />
                  <span
                    className={`w-0.5 rounded-full bg-accent-cyan transition-all duration-300 ${
                      whiteNoisePlaying ? "h-3.5 animate-pulse" : "h-1"
                    }`}
                  />
                </div>
              </button>
            </>
          )}

          {/* Profile: Deep Work */}
          {profile === "deep-work" && (
            <>
              {/* App 1: IDE Code */}
              <div
                className="group relative flex h-10 w-10 items-center justify-center rounded-lg bg-white/5 hover:bg-white/15 transition-all cursor-pointer"
                title={t.simulator.ideTip}
              >
                <CodeIcon />
                <span className="absolute bottom-1 h-1 w-1 rounded-full bg-blue-400" />
              </div>

              {/* App 2: Terminal */}
              <div
                className="group relative flex h-10 w-10 items-center justify-center rounded-lg bg-white/5 hover:bg-white/15 transition-all cursor-pointer"
                title={t.simulator.terminalTip}
              >
                <TerminalIcon />
                <span className="absolute bottom-1 h-1 w-1 rounded-full bg-purple-400" />
              </div>

              {/* Widget: Focus Timer (Interactive Pomodoro) */}
              <button
                type="button"
                onClick={() => setTimerRunning(!timerRunning)}
                className="flex h-10 items-center gap-2 rounded-lg bg-accent-blue/20 border border-accent-blue/30 px-3 text-white hover:bg-accent-blue/30 transition-all"
                title={t.simulator.timerTip}
              >
                <span className="relative flex h-2 w-2">
                  {timerRunning && (
                    <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-sky-400 opacity-75" />
                  )}
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-accent-cyan" />
                </span>
                <span className="text-xs font-mono font-bold tracking-wider">
                  {formatTimer(timerSeconds)}
                </span>
                <span className="text-[10px] text-white/50">
                  {timerRunning ? t.simulator.timerPause : t.simulator.timerStart}
                </span>
              </button>

              {/* Widget: System Activity (CPU + RAM Dual Rings) */}
              <div
                className="flex h-10 items-center gap-2 rounded-lg bg-white/8 px-2.5 text-white"
                title={t.simulator.sysTip}
              >
                <div className="relative h-6 w-6">
                  {/* Outer ring: CPU */}
                  <svg className="h-6 w-6 -rotate-90 transform" viewBox="0 0 24 24">
                    <circle
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="rgba(255,255,255,0.15)"
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
                      stroke="rgba(255,255,255,0.15)"
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
                <div className="text-[10px] text-white/70 leading-tight">
                  <div>CPU 18%</div>
                  <div>RAM 42%</div>
                </div>
              </div>
            </>
          )}
        </div>

        {/* Windows 11 Centered Taskbar at Bottom */}
        <div className="win11-taskbar">
          {/* Centered Icons */}
          <div className="flex items-center gap-1.5 sm:gap-2">
            {/* Windows Start Button */}
            <div className="flex h-7 w-7 items-center justify-center rounded-md hover:bg-white/10 transition-colors cursor-pointer">
              <svg width="15" height="15" viewBox="0 0 16 16" fill="none">
                <rect x="1" y="1" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
                <rect x="8.8" y="1" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
                <rect x="1" y="8.8" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
                <rect x="8.8" y="8.8" width="6.2" height="6.2" fill="#00adef" rx="0.5" />
              </svg>
            </div>
            {/* Search */}
            <div className="hidden sm:flex h-7 items-center gap-1.5 rounded-full bg-white/10 px-2.5 text-[11px] text-white/50">
              <svg width="12" height="12" viewBox="0 0 16 16" fill="currentColor">
                <path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001c.03.04.062.078.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1.007 1.007 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0z" />
              </svg>
              <span>{t.simulator.search}</span>
            </div>
            {/* Task View */}
            <div className="flex h-7 w-7 items-center justify-center rounded-md hover:bg-white/10 transition-colors">
              <span className="h-3.5 w-3.5 rounded border border-white/60" />
            </div>
            {/* Explorer */}
            <div className="flex h-7 w-7 items-center justify-center rounded-md hover:bg-white/10 transition-colors">
              <span className="h-3.5 w-3.5 rounded bg-amber-400/80" />
            </div>
            {/* Edge */}
            <div className="flex h-7 w-7 items-center justify-center rounded-md hover:bg-white/10 transition-colors">
              <span className="h-3.5 w-3.5 rounded-full bg-sky-400/80" />
            </div>
          </div>

          {/* System Tray (Right side) */}
          <div className="absolute right-4 flex items-center gap-3 text-[10px] text-white/60">
            <span>22°C</span>
            <span>ENG</span>
            <div className="flex flex-col text-right leading-none">
              <span>{clockLabel(now)}</span>
              <span className="text-[9px] text-white/40 mt-0.5">2026/10/7</span>
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
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-sky-400">
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  );
}

function ChatIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-emerald-400">
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </svg>
  );
}

function CodeIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-blue-400">
      <polyline points="16 18 22 12 16 6" />
      <polyline points="8 6 2 12 8 18" />
    </svg>
  );
}

function TerminalIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-purple-400">
      <polyline points="4 17 10 11 4 5" />
      <line x1="12" y1="19" x2="20" y2="19" />
    </svg>
  );
}

function ClockIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" className="text-white/80">
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
