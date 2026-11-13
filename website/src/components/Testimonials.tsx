import { useI18n } from "@/lib/i18n";

export function Testimonials() {
  const { lang, t } = useI18n();

  const quotesZh = [
    {
      content:
        "“在 Windows 上找了很久这样一款轻量的工具。它最大的优点就是不折腾系统、不修改任务栏，就安安静静悬浮在屏幕上沿。拖入快捷方式和便笺实在太顺手了。”",
      author: "Alex Z.",
      role: "全栈开发者 · 双屏工作站",
      tag: "验证用户",
    },
    {
      content:
        "“不用每次看时间或备忘还要去切窗口，番茄钟和便笺直接嵌在 SideBox 上，点开就能记。而且云母与磨砂材质和系统桌面完全融为一体，毫无违和感。”",
      author: "Lin Wei",
      role: "UI/UX 设计师",
      tag: "验证用户",
    },
    {
      content:
        "“轻巧且纯粹！内存占用才 40MB 出头，没有烦人的广告和强制登录，数据完全留在本地。Win + Shift + D 秒切日常和专注模式，把常用应用和便笺放在屏幕边缘，生产力直接拉满。”",
      author: "David C.",
      role: "独立软件创作者",
      tag: "验证用户",
    },
  ];

  const quotesEn = [
    {
      content:
        "“I've been searching for a lightweight tool like this on Windows for a long time. Its greatest strength is leaving the core OS and taskbar untouched. Dragging shortcuts and pinning notes feels completely natural.”",
      author: "Alex Z.",
      role: "Full-Stack Engineer · Dual Displays",
      tag: "Verified User",
    },
    {
      content:
        "“No need to switch windows just to check the time or jot down quick thoughts. The Pomodoro timer and notes live right on SideBox, blending seamlessly with the desktop background.”",
      author: "Lin Wei",
      role: "Product & UI Designer",
      tag: "Verified User",
    },
    {
      content:
        "“Incredibly lightweight and clean! Idles at only ~40MB RAM with zero ads or forced accounts. Win + Shift + D instantly toggles profiles — putting essentials at the screen edge maximizes productivity.”",
      author: "David C.",
      role: "Indie Software Creator",
      tag: "Verified User",
    },
  ];

  const quotes = lang === "en" ? quotesEn : quotesZh;

  return (
    <section className="scroll-mt-20 py-20 border-t border-slate-200/80 dark:border-white/5">
      <div className="page">
        <div className="mx-auto max-w-2xl text-center">
          <span className="feature-label">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
            {t.testimonials.badge}
          </span>
          <h2 className="display mt-4 text-4xl sm:text-5xl">{t.testimonials.title}</h2>
          <p className="mt-4 text-base text-mute">{t.testimonials.subtitle}</p>
        </div>

        <div className="mt-14 grid gap-6 md:grid-cols-3">
          {quotes.map((q, idx) => (
            <article key={idx} className="bento-card flex flex-col justify-between">
              <blockquote className="text-sm leading-relaxed text-slate-800 dark:text-white/90 font-normal">
                {q.content}
              </blockquote>
              <footer className="mt-6 flex items-center justify-between border-t border-slate-200/80 dark:border-white/5 pt-4">
                <div>
                  <div className="text-sm font-semibold text-slate-900 dark:text-white">{q.author}</div>
                  <div className="text-[11px] text-mute">{q.role}</div>
                </div>
                <span className="rounded-md bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 px-2 py-0.5 text-[10px] text-emerald-600 dark:text-emerald-400 font-medium">
                  {q.tag}
                </span>
              </footer>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
