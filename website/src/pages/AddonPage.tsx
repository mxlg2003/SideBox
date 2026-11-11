import { FormEvent, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { apiError, createOrder, yuan } from "@/lib/api";
import { useCatalog } from "@/lib/catalog";
import { useI18n } from "@/lib/i18n";

export function AddonPage() {
  const { lang, t, l } = useI18n();
  const { catalog } = useCatalog();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const amount = yuan(catalog.addon.amount);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setPending(true);
    try {
      const order = await createOrder(email.trim(), catalog.addon.sku, code.trim());
      navigate(l(`/pay/${order.order_id}`));
    } catch (cause) {
      setError(apiError(cause));
      setPending(false);
    }
  }

  return (
    <div className="page max-w-xl py-20">
      <h1 className="display text-5xl">{t.addonPage.title}</h1>
      <p className="mt-4 leading-8 text-mute">
        {lang === "en" ? `¥${amount}. ` : `${amount} 元。`}
        {t.addonPage.descPrefix}
      </p>
      <form className="mt-10 space-y-5" onSubmit={onSubmit}>
        <label className="block">
          <span className="mb-2 block text-sm">{t.addonPage.email}</span>
          <input
            className="field"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <label className="block">
          <span className="mb-2 block text-sm">{t.addonPage.code}</span>
          <input
            className="field font-mono"
            required
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="SBX-XXXX-XXXX-XXXX"
            autoComplete="off"
          />
        </label>
        {error ? <p className="text-sm text-[#ffb4b4]">{error}</p> : null}
        <button className="btn btn-primary" type="submit" disabled={pending}>
          {pending
            ? t.addonPage.pendingOrder
            : lang === "en"
            ? `${t.addonPage.payPrefix} ¥${amount}`
            : `${t.addonPage.payPrefix} ${amount} ${t.addonPage.payUnit}`}
        </button>
      </form>
      <p className="mt-8 text-sm text-mute">
        <Link to={l("/pricing")} className="link">
          {t.addonPage.noCodeLink}
        </Link>
      </p>
    </div>
  );
}
