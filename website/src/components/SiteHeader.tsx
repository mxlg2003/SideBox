import { Link, NavLink } from "react-router-dom";
import { useI18n } from "@/lib/i18n";

export function SiteHeader() {
  const { lang, toggleLang, t, l } = useI18n();

  const links = [
    { to: "/#product", label: t.header.product },
    { to: "/#modules", label: t.header.modules },
    { to: "/#features", label: t.header.features },
    { to: "/#faq", label: t.header.faq },
    { to: "/download", label: t.header.download },
  ];

  return (
    <header className="sticky top-0 z-40 border-b border-white/[0.06] bg-[#0c0c0f]/80 backdrop-blur-xl transition-all">
      <div className="page flex h-16 items-center justify-between">
        {/* Brand */}
        <Link to={l("/")} className="flex items-center gap-2.5 text-sm font-semibold tracking-tight text-white group">
          <img src="/logo.png" alt="SideBox" className="h-7 w-7 rounded-lg object-contain shadow-sm" />
          <span className="group-hover:text-white/90">SideBox</span>
        </Link>

        {/* Center Nav */}
        <nav className="hidden md:flex items-center gap-6 text-sm text-mute">
          {links.map((link) =>
            link.to.includes("#") ? (
              <Link key={link.to} to={l(link.to)} className="transition-colors hover:text-white">
                {link.label}
              </Link>
            ) : (
              <NavLink
                key={link.to}
                to={l(link.to)}
                className={({ isActive }) => (isActive ? "text-white font-medium" : "transition-colors hover:text-white")}
              >
                {link.label}
              </NavLink>
            ),
          )}
        </nav>

        {/* Right Action buttons */}
        <div className="flex items-center gap-2 sm:gap-2.5">
          {/* Language Switcher Pill */}
          <button
            type="button"
            onClick={toggleLang}
            className="flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.04] px-2.5 py-1.5 text-xs text-white/80 hover:bg-white/10 hover:text-white transition-colors"
            title={lang === "zh" ? "Switch to English" : "切换为中文"}
          >
            <span className="text-xs">🌐</span>
            <span className="font-medium text-[11px]">{lang === "zh" ? "EN" : "中"}</span>
          </button>

          <Link to={l("/download")} className="btn btn-primary h-9 px-4 text-xs font-semibold">
            {t.header.downloadFree}
          </Link>
        </div>
      </div>
    </header>
  );
}
