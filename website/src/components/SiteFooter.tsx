import { Link } from "react-router-dom";
import { useI18n } from "@/lib/i18n";

export function SiteFooter() {
  const { toggleLang, lang, t, l } = useI18n();

  return (
    <footer className="border-t border-slate-200 dark:border-white/[0.08] bg-slate-100/90 dark:bg-[#09090c] py-14 text-sm text-slate-600 dark:text-mute transition-colors">
      <div className="page">
        <div className="grid gap-10 sm:grid-cols-2 md:grid-cols-4">
          {/* Brand Info */}
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-slate-900 dark:text-white font-semibold">
              <img src="/logo.png" alt="SideBox" className="h-6 w-6 rounded-md object-contain" />
              <span>SideBox</span>
            </div>
            <p className="text-xs leading-relaxed text-slate-500 dark:text-mute">{t.footer.desc}</p>
            <p className="text-[11px] text-slate-400 dark:text-white/40">{t.footer.minSys}</p>
          </div>

          {/* Navigation Links */}
          <div>
            <div className="font-medium text-slate-900 dark:text-white mb-3 text-xs tracking-wider uppercase">
              {t.footer.navTitle}
            </div>
            <ul className="space-y-2 text-xs">
              <li>
                <Link to={l("/#product")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.header.product}
                </Link>
              </li>
              <li>
                <Link to={l("/#modules")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.header.modules}
                </Link>
              </li>
              <li>
                <Link to={l("/#features")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.header.features}
                </Link>
              </li>
              <li>
                <Link to={l("/#faq")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.header.faq}
                </Link>
              </li>
            </ul>
          </div>

          {/* Resources & Download */}
          <div>
            <div className="font-medium text-slate-900 dark:text-white mb-3 text-xs tracking-wider uppercase">
              {t.footer.licenseTitle}
            </div>
            <ul className="space-y-2 text-xs">
              <li>
                <Link to={l("/download")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.footer.addonDev}
                </Link>
              </li>
              <li>
                <Link to={l("/#features")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.footer.pricingPlan}
                </Link>
              </li>
              <li>
                <Link to={l("/#faq")} className="hover:text-slate-900 dark:hover:text-white transition-colors">
                  {t.header.faq}
                </Link>
              </li>
            </ul>
          </div>

          {/* Trust & Guarantee */}
          <div>
            <div className="font-medium text-slate-900 dark:text-white mb-3 text-xs tracking-wider uppercase">
              {t.footer.guaranteeTitle}
            </div>
            <div className="rounded-xl border border-slate-200 dark:border-white/5 bg-white/70 dark:bg-white/[0.02] p-3 text-xs leading-relaxed text-slate-600 dark:text-white/70 shadow-sm dark:shadow-none">
              <div className="font-medium text-slate-900 dark:text-white mb-1">{t.footer.guaranteeHead}</div>
              {t.footer.guaranteeDesc}
            </div>
          </div>
        </div>

        <div className="mt-12 flex flex-col items-center justify-between gap-4 border-t border-slate-200 dark:border-white/5 pt-8 text-xs text-slate-500 dark:text-white/40 sm:flex-row">
          <p>{t.footer.rights}</p>
          <div className="flex items-center gap-4">
            <button
              type="button"
              onClick={() => window.dispatchEvent(new CustomEvent("sidebox-open-cookie-settings"))}
              className="text-slate-600 dark:text-white/60 hover:text-slate-900 dark:hover:text-white transition-colors flex items-center gap-1"
            >
              <span>🍪</span>
              <span>{t.cookie.cookieSettings}</span>
            </button>
            <span>·</span>
            <button
              type="button"
              onClick={toggleLang}
              className="text-slate-600 dark:text-white/60 hover:text-slate-900 dark:hover:text-white transition-colors flex items-center gap-1"
            >
              <span>🌐</span>
              <span>{lang === "zh" ? "English" : "中文 (简体)"}</span>
            </button>
            <span>·</span>
            <span>{t.footer.tagLocal}</span>
            <span>·</span>
            <span>{t.footer.tagNoSub}</span>
            <span>·</span>
            <span>{t.footer.tagCoexist}</span>
          </div>
        </div>
      </div>
    </footer>
  );
}
