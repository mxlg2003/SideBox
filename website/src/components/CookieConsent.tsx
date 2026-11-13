import { useState, useEffect } from "react";
import { useI18n } from "@/lib/i18n";

const STORAGE_KEY = "sidebox_cookie_consent";

export function CookieConsent() {
  const { t } = useI18n();
  const [isVisible, setIsVisible] = useState(false);
  const [showDetails, setShowDetails] = useState(false);

  useEffect(() => {
    // Check if user has already made a consent choice
    const saved = localStorage.getItem(STORAGE_KEY);
    if (!saved) {
      // Delay showing slightly for a smooth entrance experience
      const timer = setTimeout(() => {
        setIsVisible(true);
      }, 700);
      return () => clearTimeout(timer);
    }
  }, []);

  useEffect(() => {
    // Listen for custom event to reopen preferences from footer
    const handleOpen = () => {
      setIsVisible(true);
      setShowDetails(true);
    };

    window.addEventListener("sidebox-open-cookie-settings", handleOpen);
    return () => window.removeEventListener("sidebox-open-cookie-settings", handleOpen);
  }, []);

  const handleAcceptAll = () => {
    localStorage.setItem(STORAGE_KEY, "all");
    setIsVisible(false);
  };

  const handleEssentialOnly = () => {
    localStorage.setItem(STORAGE_KEY, "essential");
    setIsVisible(false);
  };

  if (!isVisible) {
    return null;
  }

  return (
    <aside
      aria-label="Cookie consent banner"
      className="fixed bottom-4 left-4 right-4 sm:left-auto sm:right-6 sm:bottom-6 sm:max-w-md z-50 animate-in fade-in slide-in-from-bottom-5 duration-300"
    >
      <div className="rounded-2xl border border-slate-200 dark:border-white/15 bg-white/95 dark:bg-[#14141c]/95 p-5 shadow-2xl backdrop-blur-2xl transition-colors">
        {/* Header */}
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-2">
            <span className="text-xl" role="img" aria-label="Cookie">
              🍪
            </span>
            <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
              {t.cookie.title}
            </h3>
          </div>
          <button
            type="button"
            onClick={handleEssentialOnly}
            className="text-slate-400 hover:text-slate-700 dark:hover:text-white transition-colors text-xs font-bold p-1 -mr-1"
            title="Close"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Body Text */}
        <p className="mt-2.5 text-xs leading-relaxed text-slate-600 dark:text-mute">
          {t.cookie.desc}
        </p>

        {/* Optional Expandable Details */}
        {showDetails && (
          <div className="mt-3.5 space-y-2.5 rounded-xl border border-slate-200/80 dark:border-white/10 bg-slate-50 dark:bg-white/[0.03] p-3 text-xs animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between font-medium">
              <span className="text-slate-900 dark:text-white">{t.cookie.essentialTitle}</span>
              <span className="rounded-md bg-emerald-500/15 text-emerald-700 dark:text-emerald-400 border border-emerald-500/20 px-2 py-0.5 text-[10px] font-semibold">
                Always Active
              </span>
            </div>
            <p className="text-[11px] leading-relaxed text-slate-500 dark:text-mute">
              {t.cookie.essentialDesc}
            </p>
          </div>
        )}

        {/* Action Controls */}
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-slate-100 dark:border-white/5">
          <button
            type="button"
            onClick={() => setShowDetails(!showDetails)}
            className="text-xs font-medium text-slate-500 dark:text-mute hover:text-slate-800 dark:hover:text-white underline underline-offset-4 transition-colors"
          >
            {showDetails ? t.cookie.hideDetails : t.cookie.manage}
          </button>

          <div className="flex items-center gap-2 ml-auto">
            <button
              type="button"
              onClick={handleEssentialOnly}
              className="btn btn-quiet h-8 px-3 text-xs"
            >
              {t.cookie.essentialOnly}
            </button>
            <button
              type="button"
              onClick={handleAcceptAll}
              className="btn btn-primary h-8 px-3.5 text-xs font-semibold"
            >
              {t.cookie.acceptAll}
            </button>
          </div>
        </div>
      </div>
    </aside>
  );
}
