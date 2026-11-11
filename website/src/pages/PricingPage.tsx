import { Link } from "react-router-dom";
import { FaqList } from "@/components/FaqList";
import { getFaq } from "@/content";
import { useI18n } from "@/lib/i18n";

export function PricingPage() {
  const { lang, t, l } = useI18n();

  return (
    <div className="page max-w-4xl py-20 text-center">
      <div className="inline-flex items-center justify-center">
        <span className="feature-label">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
          {lang === "en" ? "100% Free Software" : "完全免费软件"}
        </span>
      </div>

      <h1 className="display mt-4 text-5xl sm:text-6xl text-white">{t.pricingPage.title}</h1>
      <p className="mx-auto mt-4 max-w-xl text-base sm:text-lg text-mute">{t.pricingPage.subtitle}</p>

      {/* Free Pillars Grid */}
      <div className="mt-12 grid gap-5 sm:grid-cols-2 text-left">
        <div className="bento-card p-6">
          <div className="flex items-center gap-3 mb-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-400 text-sm">
              ✓
            </span>
            <h3 className="text-base font-semibold text-white">{t.pricingPage.freeFeature1Title}</h3>
          </div>
          <p className="text-sm leading-relaxed text-mute">{t.pricingPage.freeFeature1Desc}</p>
        </div>

        <div className="bento-card p-6">
          <div className="flex items-center gap-3 mb-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-400 text-sm">
              ✓
            </span>
            <h3 className="text-base font-semibold text-white">{t.pricingPage.freeFeature2Title}</h3>
          </div>
          <p className="text-sm leading-relaxed text-mute">{t.pricingPage.freeFeature2Desc}</p>
        </div>

        <div className="bento-card p-6">
          <div className="flex items-center gap-3 mb-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-400 text-sm">
              ✓
            </span>
            <h3 className="text-base font-semibold text-white">{t.pricingPage.freeFeature3Title}</h3>
          </div>
          <p className="text-sm leading-relaxed text-mute">{t.pricingPage.freeFeature3Desc}</p>
        </div>

        <div className="bento-card p-6">
          <div className="flex items-center gap-3 mb-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-400 text-sm">
              ✓
            </span>
            <h3 className="text-base font-semibold text-white">{t.pricingPage.freeFeature4Title}</h3>
          </div>
          <p className="text-sm leading-relaxed text-mute">{t.pricingPage.freeFeature4Desc}</p>
        </div>
      </div>

      {/* Download Action Card */}
      <div className="panel mt-10 p-8 sm:p-10 text-center">
        <h3 className="text-xl sm:text-2xl font-semibold text-white">
          {lang === "en" ? "Ready to elevate your desktop?" : "准备好开启高效桌面悬浮岛了吗？"}
        </h3>
        <p className="mt-2 text-sm text-mute">
          {lang === "en"
            ? "Download the installer directly — no registration or license codes needed."
            : "直接下载 Windows 安装包，无需注册，无需激活码。"}
        </p>

        <div className="mt-6 flex flex-wrap items-center justify-center gap-4">
          <Link to={l("/download")} className="btn btn-primary px-8 h-11 text-sm font-semibold">
            {t.pricingPage.buyBtn}
          </Link>
          <Link to={l("/#modules")} className="btn btn-quiet px-6 h-11 text-sm font-medium">
            {t.hero.exploreWidgets}
          </Link>
        </div>
      </div>

      {/* FAQ */}
      <div className="mt-20 text-left">
        <h2 className="display text-3xl text-white">{t.pricingPage.beforePay}</h2>
        <div className="mt-6">
          <FaqList items={getFaq(lang)} />
        </div>
      </div>
    </div>
  );
}
