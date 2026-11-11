import { useEffect, useState } from "react";

export function useNow() {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  return now;
}

export function clockLabel(date: Date) {
  return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

export function weekdayLabel(date: Date, lang: "zh" | "en" = "zh") {
  if (lang === "en") {
    return ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][date.getDay()];
  }
  return ["周日", "周一", "周二", "周三", "周四", "周五", "周六"][date.getDay()];
}
