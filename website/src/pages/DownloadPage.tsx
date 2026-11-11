import { Link } from "react-router-dom";
import { downloadUrl } from "@/lib/api";
import { useI18n } from "@/lib/i18n";

export function DownloadPage() {
  const { t, l } = useI18n();

  return (
    <div className="page max-w-3xl py-20">
      <div className="text-center">
        <span className="feature-label">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
          {t.downloadPage.badge}
        </span>
        <h1 className="display mt-4 text-5xl sm:text-6xl text-white">{t.downloadPage.title}</h1>
        <p className="mx-auto mt-4 max-w-lg leading-relaxed text-mute">
          {t.downloadPage.subtitle}
        </p>

        {/* Download Action Card */}
        <div className="bento-card mx-auto mt-10 max-w-lg p-8 text-center">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-accent-blue/20 text-accent-cyan mb-4">
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
          </div>

          <h3 className="text-lg font-semibold text-white">{t.downloadPage.cardTitle}</h3>
          <p className="mt-1 text-xs text-mute">{t.downloadPage.cardSubtitle}</p>

          <div className="mt-6">
            {downloadUrl ? (
              <a className="btn btn-primary w-full h-11 text-sm font-semibold" href={downloadUrl}>
                {t.downloadPage.downloadBtn}
              </a>
            ) : (
              <div className="rounded-xl border border-white/10 bg-white/[0.04] p-3 text-xs text-mute">
                {t.downloadPage.pendingPkg}
              </div>
            )}
          </div>

          <div className="mt-4 flex items-center justify-center gap-4 text-[11px] text-white/50">
            <span>{t.downloadPage.sysReq}</span>
            <span>·</span>
            <span>{t.downloadPage.noRuntime}</span>
          </div>
        </div>
      </div>

      {/* Steps Bento */}
      <div className="mt-16">
        <h2 className="display text-2xl text-center text-white">{t.downloadPage.stepsTitle}</h2>
        <div className="mt-8 grid gap-4 sm:grid-cols-3">
          <div className="bento-card p-5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-white/10 text-xs font-bold text-white mb-3">
              1
            </span>
            <div className="text-sm font-semibold text-white">{t.downloadPage.step1Title}</div>
            <p className="mt-1.5 text-xs leading-relaxed text-mute">{t.downloadPage.step1Desc}</p>
          </div>

          <div className="bento-card p-5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-white/10 text-xs font-bold text-white mb-3">
              2
            </span>
            <div className="text-sm font-semibold text-white">{t.downloadPage.step2Title}</div>
            <p className="mt-1.5 text-xs leading-relaxed text-mute">{t.downloadPage.step2Desc}</p>
          </div>

          <div className="bento-card p-5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-white/10 text-xs font-bold text-white mb-3">
              3
            </span>
            <div className="text-sm font-semibold text-white">{t.downloadPage.step3Title}</div>
            <p className="mt-1.5 text-xs leading-relaxed text-mute">{t.downloadPage.step3Desc}</p>
          </div>
        </div>
      </div>

      <div className="mt-14 text-center">
        <p className="text-sm text-mute">
          {t.downloadPage.noCodeYet}{" "}
          <Link to={l("/#faq")} className="link font-medium">
            {t.downloadPage.viewPricingLink}
          </Link>
        </p>
      </div>
    </div>
  );
}
