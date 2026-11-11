export function formatDateTime(value?: string | null) {
  if (!value) {
    return "--";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

export function formatFen(fen?: number | null, currency = "CNY") {
  if (fen == null) {
    return "--";
  }

  const yuan = (fen / 100).toFixed(2);
  return currency === "CNY" ? `¥${yuan}` : `${yuan} ${currency}`;
}
