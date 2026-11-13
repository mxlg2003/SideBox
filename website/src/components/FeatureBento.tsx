import { useI18n } from "@/lib/i18n";

export function FeatureBento() {
  const { t } = useI18n();

  return (
    <section id="features" className="scroll-mt-20 py-20">
      <div className="page">
        {/* Section header */}
        <div className="mx-auto max-w-2xl text-center">
          <span className="feature-label">
            <span className="h-1.5 w-1.5 rounded-full bg-accent-blue animate-pulse" />
            {t.bento.badge}
          </span>
          <h2 className="display mt-4 text-4xl sm:text-5xl">{t.bento.title}</h2>
          <p className="mt-4 text-base text-mute">{t.bento.subtitle}</p>
        </div>

        {/* Bento Grid (6 cards) */}
        <div className="mt-14 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {/* Card 1: 拖入即钉 & 单实例激活 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-slate-200/80 dark:border-white/5 bg-slate-100/80 dark:bg-black/40 p-4">
                <div className="flex items-center gap-3">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-amber-500/15 border border-amber-500/30 text-amber-600 dark:text-amber-300 font-bold text-lg shadow-md group-hover:scale-105 transition-transform">
                    P
                  </div>
                  <span className="text-slate-400 dark:text-white/40 text-xs">➔</span>
                  <div className="flex items-center gap-1.5 rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-[#1f2029] p-2 px-3 shadow-sm dark:shadow-md">
                    <span className="h-2 w-2 rounded-full bg-emerald-500" />
                    <span className="text-xs font-medium text-slate-800 dark:text-white">{t.bento.card1Action}</span>
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{t.bento.card1Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card1Desc}</p>
            </div>
          </article>

          {/* Card 2: 快捷键随时切换与唤出 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-slate-200/80 dark:border-white/5 bg-slate-100/80 dark:bg-black/40 p-4">
                <div className="flex flex-col gap-2.5">
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-mute">{t.bento.hotkeySwitch}</span>
                    <span className="keycap">Win</span>
                    <span className="keycap">Shift</span>
                    <span className="keycap">D</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-mute">{t.bento.hotkeySummon}</span>
                    <span className="keycap">Win</span>
                    <span className="keycap">Shift</span>
                    <span className="keycap">A</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-mute">{t.bento.hotkeyUndo}</span>
                    <span className="keycap">Win</span>
                    <span className="keycap">Shift</span>
                    <span className="keycap">Z</span>
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{t.bento.card2Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card2Desc}</p>
            </div>
          </article>

          {/* Card 3: 屏幕边缘停靠与自动隐藏 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-slate-200/80 dark:border-white/5 bg-slate-100/80 dark:bg-black/40 p-4">
                <div className="relative h-20 w-32 rounded-lg border border-slate-300/80 dark:border-white/10 bg-slate-200/50 dark:bg-white/[0.02] p-1.5 flex flex-col justify-between">
                  {/* Top island indicator */}
                  <div className="mx-auto h-2 w-12 rounded-full bg-accent-blue shadow-sm shadow-blue-500/50" />
                  <div className="flex justify-between items-center px-1">
                    <div className="h-8 w-1.5 rounded-full bg-slate-400/40 dark:bg-white/20" />
                    <span className="text-[10px] text-slate-500 dark:text-white/30">{t.bento.autoHideTag}</span>
                    <div className="h-8 w-1.5 rounded-full bg-slate-400/40 dark:bg-white/20" />
                  </div>
                  {/* Taskbar indicator */}
                  <div className="h-1.5 w-full rounded-sm bg-slate-300 dark:bg-white/10" />
                </div>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{t.bento.card3Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card3Desc}</p>
            </div>
          </article>

          {/* Card 4: 原生轻量，不改系统 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-slate-200/80 dark:border-white/5 bg-slate-100/80 dark:bg-black/40 p-4">
                <div className="flex flex-col gap-2 text-xs font-medium">
                  <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
                    <span>✓</span>
                    <span>{t.bento.card4Point1}</span>
                  </div>
                  <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
                    <span>✓</span>
                    <span>{t.bento.card4Point2}</span>
                  </div>
                  <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
                    <span>✓</span>
                    <span>{t.bento.card4Point3}</span>
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{t.bento.card4Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card4Desc}</p>
            </div>
          </article>

          {/* Card 5: 100% 隐私，数据永在本地 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-slate-200/80 dark:border-white/5 bg-slate-100/80 dark:bg-black/40 p-4">
                <div className="flex flex-col items-center gap-2 text-center">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-slate-200 dark:bg-white/10 text-slate-800 dark:text-white">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                    </svg>
                  </div>
                  <span className="font-mono text-xs text-slate-700 dark:text-white/70">%AppData%\SideBox</span>
                  <span className="text-[10px] text-slate-500 dark:text-white/40">{t.bento.card5Note}</span>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{t.bento.card5Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card5Desc}</p>
            </div>
          </article>

          {/* Card 6: 原生轻量，低能耗静默驻留 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-slate-200/80 dark:border-white/5 bg-slate-100/80 dark:bg-black/40 p-4">
                <div className="flex flex-col items-center gap-2 text-center">
                  <div className="flex items-baseline gap-1">
                    <span className="text-3xl font-extrabold tracking-tight text-slate-900 dark:text-white font-mono">~40</span>
                    <span className="text-sm font-semibold text-accent-blue">MB</span>
                  </div>
                  <div className="flex items-center gap-2 text-xs text-mute font-mono">
                    <span>CPU &lt; 0.1%</span>
                    <span>·</span>
                    <span className="text-emerald-600 dark:text-emerald-400 font-sans font-medium">{t.bento.card6Badge2}</span>
                  </div>
                  <div className="mt-1 rounded-full bg-blue-500/10 border border-blue-500/25 px-2.5 py-0.5 text-[10px] font-medium text-blue-700 dark:text-blue-300">
                    {t.bento.card6Badge3}
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{t.bento.card6Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card6Desc}</p>
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}
