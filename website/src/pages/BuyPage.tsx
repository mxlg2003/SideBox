import { FormEvent, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { PriceChooser } from "@/components/PriceChooser";
import { apiError, createOrder, yuan } from "@/lib/api";
import { lowestPerDevice, useCatalog } from "@/lib/catalog";
import { useI18n } from "@/lib/i18n";

export function BuyPage() {
  const { lang, t, l } = useI18n();
  const { catalog } = useCatalog();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const requested = params.get("sku") ?? "";
  const initial = catalog.skus.some((item) => item.sku === requested) ? requested : catalog.skus[0].sku;
  const [sku, setSku] = useState(initial);
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const best = useMemo(() => lowestPerDevice(catalog.skus), [catalog.skus]);
  const selected = catalog.skus.find((item) => item.sku === sku) ?? catalog.skus[0];

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setPending(true);
    try {
      const order = await createOrder(email.trim(), selected.sku);
      navigate(l(`/pay/${order.order_id}`));
    } catch (cause) {
      setError(apiError(cause));
      setPending(false);
    }
  }

  return (
    <div className="page max-w-3xl py-20">
      <h1 className="display text-center text-5xl">{t.buyPage.title}</h1>
      <p className="mx-auto mt-4 max-w-xl text-center leading-7 text-mute">
        {t.buyPage.subtitle}
      </p>

      <form className="panel mt-10 space-y-6 p-5 sm:p-7" onSubmit={onSubmit}>
        <PriceChooser skus={catalog.skus} selected={selected.sku} bestSku={best.sku} onSelect={setSku} />
        <label className="block">
          <span className="mb-2 block text-sm">{t.buyPage.email}</span>
          <input
            className="field"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="you@example.com"
          />
        </label>
        {error ? <p className="text-sm text-[#ffb4b4]">{error}</p> : null}
        <button className="btn btn-primary" type="submit" disabled={pending}>
          {pending
            ? t.buyPage.pendingOrder
            : lang === "en"
            ? `${t.buyPage.payPrefix} ¥${yuan(selected.amount)}`
            : `${t.buyPage.payPrefix} ${yuan(selected.amount)} ${t.buyPage.payUnit}`}
        </button>
      </form>

      <p className="mt-8 text-sm text-mute">
        <Link to={l("/addon")} className="link">
          {t.buyPage.addonLink}
        </Link>
      </p>
    </div>
  );
}
