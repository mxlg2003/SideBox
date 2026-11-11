import { useEffect } from "react";
import { Route, Routes, useLocation, Link, Navigate } from "react-router-dom";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteHeader } from "@/components/SiteHeader";
import { DownloadPage } from "@/pages/DownloadPage";
import { HomePage } from "@/pages/HomePage";
import { PricingPage } from "@/pages/PricingPage";
import { I18nProvider, useI18n } from "@/lib/i18n";

function RedirectToDownload() {
  const { l } = useI18n();
  return <Navigate to={l("/download")} replace />;
}

function ScrollManager() {
  const { pathname, hash } = useLocation();

  useEffect(() => {
    if (!hash) {
      window.scrollTo(0, 0);
      return;
    }
    const id = decodeURIComponent(hash.slice(1));
    document.getElementById(id)?.scrollIntoView();
  }, [pathname, hash]);

  return null;
}

function NotFound() {
  const { lang, l } = useI18n();
  return (
    <div className="page py-24 text-center">
      <h1 className="display text-4xl">{lang === "en" ? "Page not found." : "没有这一页。"}</h1>
      <Link to={l("/")} className="btn btn-primary mt-6">
        {lang === "en" ? "Back to Home" : "回首页"}
      </Link>
    </div>
  );
}

export default function App() {
  return (
    <I18nProvider>
      <div className="flex min-h-screen flex-col">
        <ScrollManager />
        <SiteHeader />
        <main className="flex-1">
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/pricing" element={<PricingPage />} />
            <Route path="/buy" element={<RedirectToDownload />} />
            <Route path="/pay/:orderId" element={<RedirectToDownload />} />
            <Route path="/addon" element={<RedirectToDownload />} />
            <Route path="/download" element={<DownloadPage />} />

            {/* Localized routes */}
            <Route path="/en" element={<HomePage />} />
            <Route path="/en/pricing" element={<PricingPage />} />
            <Route path="/en/buy" element={<RedirectToDownload />} />
            <Route path="/en/pay/:orderId" element={<RedirectToDownload />} />
            <Route path="/en/addon" element={<RedirectToDownload />} />
            <Route path="/en/download" element={<DownloadPage />} />

            <Route path="*" element={<NotFound />} />
          </Routes>
        </main>
        <SiteFooter />
      </div>
    </I18nProvider>
  );
}
