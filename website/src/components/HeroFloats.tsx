import { clockLabel, useNow, weekdayLabel } from "@/lib/clock";

export function HeroFloats() {
  const now = useNow();

  return (
    <div className="pointer-events-none absolute inset-0 hidden lg:block" aria-hidden="true">
      <div className="float-card absolute left-[4%] top-[18%] px-4 py-3">
        <div className="text-[11px] text-mute">{weekdayLabel(now)}</div>
        <div className="mt-1 text-2xl font-medium">{clockLabel(now)}</div>
      </div>
      <div className="float-card note absolute left-[7%] top-[46%] px-4 py-3">
        <div className="text-sm">下午对一下稿</div>
      </div>
      <div className="float-card absolute bottom-[12%] left-[10%] px-4 py-3">
        <div className="text-lg">18°</div>
        <div className="text-[11px] text-mute">多云</div>
      </div>
      <div className="float-card absolute right-[6%] top-[16%] w-40 px-4 py-3">
        <div className="text-[11px] text-mute">专注</div>
        <div className="mt-1 text-xl">25:00</div>
      </div>
      <div className="float-card absolute right-[4%] top-[42%] w-44 px-4 py-3">
        <div className="flex items-end justify-between">
          <span className="text-sm">1,428</span>
          <span className="text-xs text-[#ff6b6b]">+1.2%</span>
        </div>
        <svg className="mt-2 h-8 w-full" viewBox="0 0 120 32" fill="none">
          <path d="M0 24 C16 22 24 18 40 16 S70 20 88 10 112 6 120 4" stroke="#ff6b6b" strokeWidth="1.6" />
        </svg>
      </div>
      <div className="float-card absolute bottom-[14%] right-[9%] px-4 py-3">
        <div className="text-[11px] text-[#ff8a80]">周三</div>
        <div className="text-2xl leading-none">7</div>
        <div className="mt-1 text-xs text-mute">09:30 评审</div>
      </div>
    </div>
  );
}
