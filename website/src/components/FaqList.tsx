import { useState } from "react";

export function FaqList({ items }: { items: { id: string; q: string; a: string }[] }) {
  const [openIds, setOpenIds] = useState<Record<string, boolean>>({
    [items[0]?.id || ""]: true,
  });

  const toggle = (id: string) => {
    setOpenIds((prev) => ({
      ...prev,
      [id]: !prev[id],
    }));
  };

  return (
    <div className="space-y-3">
      {items.map((item) => {
        const isOpen = !!openIds[item.id];
        return (
          <div
            key={item.id}
            className="overflow-hidden rounded-2xl border border-slate-200 dark:border-white/[0.08] bg-white/80 dark:bg-[#14141a]/80 backdrop-blur-md shadow-sm dark:shadow-none transition-colors"
          >
            <button
              type="button"
              onClick={() => toggle(item.id)}
              className="flex w-full items-center justify-between p-5 text-left font-medium text-slate-900 dark:text-white transition-colors hover:text-slate-700 dark:hover:text-white"
            >
              <span className="text-base font-medium">{item.q}</span>
              <svg
                className={`ml-4 h-4 w-4 shrink-0 text-slate-400 dark:text-white/50 transition-transform duration-200 ${
                  isOpen ? "rotate-180 text-slate-900 dark:text-white" : ""
                }`}
                viewBox="0 0 16 16"
                fill="none"
              >
                <path
                  d="M4 6L8 10L12 6"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </button>
            {isOpen && (
              <div className="px-5 pb-5 pt-1 text-sm leading-7 text-mute border-t border-slate-200/80 dark:border-white/5">
                {item.a}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
