import { Link } from "react-router-dom";
import { FaqList } from "@/components/FaqList";
import { FeatureBento } from "@/components/FeatureBento";
import { IslandStage } from "@/components/IslandStage";
import { Testimonials } from "@/components/Testimonials";
import { WidgetGallery } from "@/components/WidgetGallery";
import { WorkflowShowcase } from "@/components/WorkflowShowcase";
import { getFaq } from "@/content";
import { useI18n } from "@/lib/i18n";

export function HomePage() {
  const { lang, t, l } = useI18n();

  return (
    <>
      {/* Hero Section */}
      <section className="relative overflow-hidden pt-12 pb-16 sm:pt-20 sm:pb-24">
        <div className="page text-center">
          {/* Subtle Feature Badge */}
          <div className="inline-flex items-center justify-center">
            <span className="feature-label">
              <span className="h-1.5 w-1.5 rounded-full bg-accent-blue animate-pulse" />
              {t.hero.badge}
            </span>
          </div>

          {/* Headline */}
          <h1 className="display mx-auto mt-6 max-w-4xl text-5xl leading-[1.12] sm:text-6xl lg:text-7xl font-normal tracking-tight text-white">
            {t.hero.titleLine1}
            <br />
            {t.hero.titleLine2}
          </h1>

          {/* Subtitle */}
          <p className="mx-auto mt-6 max-w-xl text-base leading-relaxed text-mute sm:text-lg">
            {t.hero.subtitle}
          </p>

          {/* CTA Buttons */}
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3.5">
            <Link to={l("/download")} className="btn btn-primary px-7 h-11 text-sm font-semibold">
              {t.hero.downloadFree}
            </Link>
            <Link to={l("/#modules")} className="btn btn-quiet px-6 h-11 text-sm font-medium">
              {t.hero.exploreWidgets}
            </Link>
          </div>

          {/* Trust Guarantees */}
          <p className="mt-4 text-xs text-white/50">{t.hero.guarantee}</p>

          {/* Interactive Desktop Simulator (The Core Hero Stage) */}
          <div id="product" className="mt-14 sm:mt-16 scroll-mt-20">
            <IslandStage />
          </div>
        </div>
      </section>

      {/* Dual Value Proposition Section (Coexistence with Windows) */}
      <section className="py-20 border-t border-white/5">
        <div className="page">
          <div className="mx-auto max-w-2xl text-center mb-14">
            <span className="feature-label">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
              {t.values.badge}
            </span>
            <h2 className="display mt-4 text-4xl sm:text-5xl">{t.values.title}</h2>
            <p className="mt-4 text-base text-mute">{t.values.subtitle}</p>
          </div>

          <div className="grid gap-8 md:grid-cols-2">
            {/* Card 1: 屏幕边缘的 SideBox */}
            <article className="bento-card group flex flex-col justify-between">
              <div>
                <div className="mb-6 flex h-48 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-6 overflow-hidden relative">
                  <div className="flex items-center gap-2 rounded-xl border border-white/15 bg-white/10 px-4 py-3 shadow-xl backdrop-blur-md group-hover:scale-105 transition-transform duration-300">
                    <span className="h-2 w-2 rounded-full bg-emerald-400" />
                    <span className="text-sm font-medium text-white">Edge</span>
                    <span className="text-white/30">|</span>
                    <span className="text-sm font-medium text-white">VS Code</span>
                    <span className="text-white/30">|</span>
                    <span className="rounded bg-[#f6e27a] px-2 py-0.5 text-xs text-[#1c1b19] font-medium">
                      {lang === "en" ? "Note" : "便笺"}
                    </span>
                    <span className="text-white/30">|</span>
                    <span className="text-xs text-white/80 font-mono">25:00</span>
                  </div>
                </div>
                <span className="text-xs font-semibold uppercase tracking-wider text-accent-blue">
                  {t.values.card1Label}
                </span>
                <h3 className="mt-2 text-2xl font-semibold text-white">{t.values.card1Title}</h3>
                <p className="mt-3 text-sm leading-7 text-mute">{t.values.card1Desc}</p>
              </div>
            </article>

            {/* Card 2: 保持熟悉的原生任务栏 */}
            <article className="bento-card group flex flex-col justify-between">
              <div>
                <div className="mb-6 flex h-48 items-center justify-center rounded-2xl border border-white/5 bg-black/40 p-6 overflow-hidden relative">
                  <div className="w-full max-w-xs rounded-xl border border-white/10 bg-[#161824]/90 p-3 shadow-xl flex items-center justify-between group-hover:scale-105 transition-transform duration-300">
                    <div className="flex items-center gap-2">
                      <span className="h-4 w-4 rounded-sm bg-sky-500" />
                      <span className="text-xs text-white/80 font-medium">
                        {lang === "en" ? "Windows Taskbar" : "Windows 任务栏"}
                      </span>
                    </div>
                    <span className="rounded-full bg-emerald-500/20 px-2 py-0.5 text-[10px] text-emerald-300">
                      {t.values.taskbarBadge}
                    </span>
                  </div>
                </div>
                <span className="text-xs font-semibold uppercase tracking-wider text-emerald-400">
                  {t.values.card2Label}
                </span>
                <h3 className="mt-2 text-2xl font-semibold text-white">{t.values.card2Title}</h3>
                <p className="mt-3 text-sm leading-7 text-mute">{t.values.card2Desc}</p>
              </div>
            </article>
          </div>
        </div>
      </section>

      {/* 3-Row Widget Wall Marquee */}
      <section id="modules" className="scroll-mt-20 py-20 border-t border-white/5 overflow-hidden">
        <div className="page text-center">
          <span className="feature-label">
            <span className="h-1.5 w-1.5 rounded-full bg-amber-400 animate-pulse" />
            {t.widgetsSection.badge}
          </span>
          <h2 className="display mt-4 text-4xl sm:text-5xl">{t.widgetsSection.title}</h2>
          <p className="mx-auto mt-4 max-w-xl text-base leading-7 text-mute">
            {t.widgetsSection.subtitle}
          </p>

          <WidgetGallery />
        </div>
      </section>

      {/* Scenario / Workflow Showcase */}
      <WorkflowShowcase />

      {/* Feature Bento Grid (6 cards) */}
      <FeatureBento />

      {/* Social Proof / Testimonials */}
      <Testimonials />

      {/* FAQ Section */}
      <section id="faq" className="scroll-mt-20 py-20 border-t border-white/5">
        <div className="page grid items-start gap-12 lg:grid-cols-[0.8fr_1.2fr]">
          <div>
            <span className="feature-label">
              <span className="h-1.5 w-1.5 rounded-full bg-blue-400" />
              {t.faqSection.badge}
            </span>
            <h2 className="display mt-4 text-4xl sm:text-5xl leading-tight">
              {t.faqSection.titleLine1}
              <br />
              {t.faqSection.titleLine2}
            </h2>
            <p className="mt-4 text-base leading-relaxed text-mute">{t.faqSection.subtitle}</p>
            <div className="mt-8">
              <Link to={l("/download")} className="btn btn-quiet text-xs">
                {t.faqSection.tryFreeBtn}
              </Link>
            </div>
          </div>
          <FaqList items={getFaq(lang)} />
        </div>
      </section>

      {/* Final Call to Action (Download CTA) */}
      <section id="download-cta" className="scroll-mt-20 py-24 border-t border-white/5">
        <div className="page">
          <div className="rounded-3xl border border-white/10 bg-gradient-to-b from-[#181924] to-[#101017] p-8 sm:p-14 text-center shadow-2xl relative overflow-hidden">
            {/* Ambient background glow */}
            <div className="pointer-events-none absolute -top-24 left-1/2 -translate-x-1/2 h-64 w-96 rounded-full bg-accent-blue/15 blur-3xl" />

            <div className="relative z-10 mx-auto max-w-2xl">
              <span className="feature-label">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
                {t.pricingSection.badge}
              </span>
              <h2 className="display mt-4 text-4xl sm:text-6xl text-white">
                {t.pricingSection.title}
              </h2>
              <p className="mx-auto mt-4 max-w-md text-base text-mute">
                {t.pricingSection.subtitle}
              </p>

              <div className="mt-8 flex flex-wrap items-center justify-center gap-4">
                <Link to={l("/download")} className="btn btn-primary px-7 h-11 text-sm font-semibold">
                  {t.pricingSection.chooseBtn}
                </Link>
                <Link to={l("/#faq")} className="btn btn-quiet px-7 h-11 text-sm">
                  {t.pricingSection.downloadBtn}
                </Link>
              </div>

              <div className="mt-6 flex flex-wrap items-center justify-center gap-6 text-xs text-white/50">
                <span>{t.pricingSection.tag1}</span>
                <span>{t.pricingSection.tag2}</span>
                <span>{t.pricingSection.tag3}</span>
              </div>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
