import { yuan, type Sku } from "@/lib/api";
import { useI18n } from "@/lib/i18n";

export function PriceChooser({
  skus,
  selected,
  bestSku,
  onSelect,
}: {
  skus: Sku[];
  selected: string;
  bestSku: string;
  onSelect: (sku: string) => void;
}) {
  const { lang, t } = useI18n();

  return (
    <div
      className="grid gap-3 sm:grid-cols-3"
      role="radiogroup"
      aria-label={lang === "en" ? "Select device count" : "电脑台数"}
    >
      {skus.map((item) => {
        const active = item.sku === selected;
        return (
          <button
            key={item.sku}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => onSelect(item.sku)}
            className={
              active
                ? "rounded-2xl border border-[#5b8def] bg-[#232833] px-4 py-4 text-left"
                : "rounded-2xl border border-white/10 bg-[#161616] px-4 py-4 text-left hover:border-white/25"
            }
          >
            <span className="flex items-center justify-between gap-2 text-sm">
              <span>
                {item.device_limit} {lang === "en" ? (item.device_limit === 1 ? "PC" : "PCs") : t.priceChooser.unit}
                {item.sku === bestSku ? (
                  <span className="ml-2 text-xs text-mute">{t.priceChooser.bestValue}</span>
                ) : null}
              </span>
              <Check active={active} />
            </span>
            <span className="mt-4 block text-2xl font-medium">
              {lang === "en" ? `¥${yuan(item.amount)}` : `${yuan(item.amount)} ${t.priceChooser.currency}`}
            </span>
          </button>
        );
      })}
    </div>
  );
}

function Check({ active }: { active: boolean }) {
  return (
    <span
      className={
        active
          ? "flex h-5 w-5 items-center justify-center rounded-full bg-[#5b8def] text-[#111]"
          : "h-5 w-5 rounded-full border border-white/25"
      }
      aria-hidden="true"
    >
      {active ? (
        <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
          <path d="M2.5 6.2 4.8 8.4 9.5 3.6" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
        </svg>
      ) : null}
    </span>
  );
}
