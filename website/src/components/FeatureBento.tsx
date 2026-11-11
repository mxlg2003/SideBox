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
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-4">
                <div className="flex items-center gap-3">
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-amber-500/15 border border-amber-500/30 text-amber-300 font-bold text-lg shadow-lg group-hover:scale-105 transition-transform">
                    P
                  </div>
                  <span className="text-white/40 text-xs">➔</span>
                  <div className="flex items-center gap-1.5 rounded-xl border border-white/10 bg-[#1f2029] p-2 px-3 shadow-md">
                    <span className="h-2 w-2 rounded-full bg-emerald-400" />
                    <span className="text-xs font-medium text-white">{t.bento.card1Action}</span>
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white">{t.bento.card1Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card1Desc}</p>
            </div>
          </article>

          {/* Card 2: 快捷键随时切换与唤出 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-4">
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
              <h3 className="text-lg font-semibold text-white">{t.bento.card2Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card2Desc}</p>
            </div>
          </article>

          {/* Card 3: 屏幕边缘停靠与自动隐藏 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-4">
                <div className="relative h-20 w-32 rounded-lg border border-white/10 bg-white/[0.02] p-1.5 flex flex-col justify-between">
                  {/* Top island indicator */}
                  <div className="mx-auto h-2 w-12 rounded-full bg-accent-blue shadow-sm shadow-blue-500/50" />
                  <div className="flex justify-between items-center px-1">
                    <div className="h-8 w-1.5 rounded-full bg-white/20" />
                    <span className="text-[10px] text-white/30">{t.bento.autoHideTag}</span>
                    <div className="h-8 w-1.5 rounded-full bg-white/20" />
                  </div>
                  {/* Taskbar indicator */}
                  <div className="h-1.5 w-full rounded-sm bg-white/10" />
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white">{t.bento.card3Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card3Desc}</p>
            </div>
          </article>

          {/* Card 4: 原生轻量，不改系统 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-4">
                <div className="flex flex-col gap-2 text-xs">
                  <div className="flex items-center gap-2 text-emerald-400">
                    <span>✓</span>
                    <span>{t.bento.card4Point1}</span>
                  </div>
                  <div className="flex items-center gap-2 text-emerald-400">
                    <span>✓</span>
                    <span>{t.bento.card4Point2}</span>
                  </div>
                  <div className="flex items-center gap-2 text-emerald-400">
                    <span>✓</span>
                    <span>{t.bento.card4Point3}</span>
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white">{t.bento.card4Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card4Desc}</p>
            </div>
          </article>

          {/* Card 5: 100% 隐私，数据永在本地 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-4">
                <div className="flex flex-col items-center gap-2 text-center">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-white/10 text-white">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                    </svg>
                  </div>
                  <span className="font-mono text-xs text-white/70">%AppData%\SideBox</span>
                  <span className="text-[10px] text-white/40">{t.bento.card5Note}</span>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white">{t.bento.card5Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card5Desc}</p>
            </div>
          </article>

          {/* Card 6: 买断授权，无订阅负担 */}
          <article className="bento-card group flex flex-col justify-between">
            <div>
              <div className="mb-6 flex h-36 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-4">
                <div className="flex flex-col items-center gap-1.5 text-center">
                  <div className="text-xl font-bold text-white">{t.bento.card6Badge1}</div>
                  <div className="text-xs text-mute">{t.bento.card6Badge2}</div>
                  <div className="mt-1 rounded-full bg-emerald-500/15 border border-emerald-500/30 px-3 py-0.5 text-[11px] font-medium text-emerald-300">
                    {t.bento.card6Badge3}
                  </div>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white">{t.bento.card6Title}</h3>
              <p className="mt-2 text-sm leading-6 text-mute">{t.bento.card6Desc}</p>
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}
