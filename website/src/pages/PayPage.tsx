import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import axios from "axios";
import { toDataURL } from "qrcode";
import { apiError, fetchOrder, isAddonSku, isMockCodeUrl, mockPay, yuan, type Order } from "@/lib/api";
import { useI18n } from "@/lib/i18n";

export function PayPage() {
  const { lang, t, l } = useI18n();
  const { orderId = "" } = useParams();
  const [order, setOrder] = useState<Order | null>(null);
  const [error, setError] = useState("");
  const [qr, setQr] = useState("");
  const [paying, setPaying] = useState(false);
  const [copied, setCopied] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const previous = document.title;
    document.title = t.payPage.pageTitle;
    let robots = document.querySelector('meta[name="robots"]');
    const created = !robots;
    if (!robots) {
      robots = document.createElement("meta");
      robots.setAttribute("name", "robots");
      document.head.appendChild(robots);
    }
    const previousRobots = robots.getAttribute("content");
    robots.setAttribute("content", "noindex");
    return () => {
      document.title = previous;
      if (created) {
        robots?.remove();
      } else if (previousRobots) {
        robots?.setAttribute("content", previousRobots);
      }
    };
  }, [t.payPage.pageTitle]);

  useEffect(() => {
    let cancelled = false;
    let timer = 0;
    async function load() {
      try {
        const next = await fetchOrder(orderId);
        if (cancelled) {
          return;
        }
        setOrder(next);
        setError("");
        if (next.status === "paid" || next.status === "expired") {
          window.clearInterval(timer);
        }
      } catch (cause) {
        if (!cancelled) {
          setError(apiError(cause, lang === "en" ? "Order not found" : "订单不存在"));
          if (axios.isAxiosError(cause) && cause.response?.status === 404) {
            window.clearInterval(timer);
          }
        }
      }
    }
    void load();
    timer = window.setInterval(() => {
      void load();
    }, 2000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [orderId, lang]);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!order) {
      return;
    }
    document.title = order.status === "paid" ? t.payPage.paidTitle : t.payPage.pageTitle;
  }, [order, t.payPage.paidTitle, t.payPage.pageTitle]);

  useEffect(() => {
    if (!order || order.status !== "pending" || isMockCodeUrl(order.code_url)) {
      setQr("");
      return;
    }
    let cancelled = false;
    toDataURL(order.code_url, { margin: 1, width: 240 })
      .then((url) => {
        if (!cancelled) {
          setQr(url);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setQr("");
        }
      });
    return () => {
      cancelled = true;
    };
  }, [order]);

  async function finishMock() {
    setPaying(true);
    setError("");
    try {
      setOrder(await mockPay(orderId));
    } catch (cause) {
      setError(apiError(cause));
    } finally {
      setPaying(false);
    }
  }

  async function copyCode() {
    if (!order?.license_code) {
      return;
    }
    try {
      await navigator.clipboard.writeText(order.license_code);
      setCopied(true);
    } catch {
      const field = document.createElement("textarea");
      field.value = order.license_code;
      field.setAttribute("readonly", "");
      field.style.position = "fixed";
      field.style.left = "-9999px";
      document.body.appendChild(field);
      field.select();
      const copiedByCommand = document.execCommand("copy");
      field.remove();
      if (copiedByCommand) {
        setCopied(true);
      } else {
        setError(t.payPage.copyFailed);
      }
    }
  }

  const pending = order?.status === "pending";
  const paid = order?.status === "paid";
  const addon = order ? isAddonSku(order.sku) : false;
  const remain = order ? Math.max(0, new Date(order.expires_at).getTime() - now) : 0;

  return (
    <div className="page max-w-xl py-16">
      <h1 className="display text-5xl">{paid ? t.payPage.headingPaid : t.payPage.headingPay}</h1>
      {order ? (
        <p className="mt-3 text-sm text-mute">
          {t.payPage.orderPrefix} {order.order_id} · {order.product_name} ·{" "}
          {lang === "en" ? `¥${yuan(order.amount)}` : `${yuan(order.amount)} 元`}
        </p>
      ) : null}

      {error ? <p className="mt-6 text-sm text-[#ffb4b4]">{error}</p> : null}

      {!order && !error ? <p className="mt-8 text-mute">{t.payPage.loadingOrder}</p> : null}

      {order && pending ? (
        <div className="mt-8">
          {isMockCodeUrl(order.code_url) ? (
            <>
              <p className="leading-8 text-mute">{t.payPage.mockNotice}</p>
              <button
                className="btn btn-primary mt-6"
                type="button"
                onClick={() => void finishMock()}
                disabled={paying}
              >
                {paying ? t.payPage.confirming : t.payPage.finishMock}
              </button>
            </>
          ) : (
            <>
              <p className="leading-8 text-mute">{t.payPage.wechatNotice}</p>
              {qr ? (
                <img
                  className="mt-6 rounded-xl bg-white p-3"
                  src={qr}
                  width={240}
                  height={240}
                  alt={t.payPage.qrAlt}
                />
              ) : null}
            </>
          )}
          <p className="mt-4 text-sm text-mute">
            {remain > 0
              ? `${t.payPage.remainPrefix} ${Math.floor(remain / 60000)} ${t.payPage.minuteUnit} ${String(
                  Math.floor((remain % 60000) / 1000)
                ).padStart(2, "0")} ${t.payPage.secondUnit}`
              : t.payPage.orderExpiring}
          </p>
        </div>
      ) : null}

      {order && paid ? (
        <div className="mt-8">
          <p className="leading-8 text-mute">
            {addon
              ? t.payPage.addonSuccess
              : `${t.payPage.buySuccessPrefix} ${order.device_limit ?? ""} ${t.payPage.buySuccessSuffix}`}
          </p>
          {order.license_code ? (
            <div className="mt-6 rounded-2xl border border-white/10 bg-[#1c1c1c] p-4">
              <p className="text-sm text-mute">{t.payPage.codeLabel}</p>
              <p className="mt-2 font-mono text-lg tracking-wide">{order.license_code}</p>
              <button className="btn btn-line mt-4 h-9" type="button" onClick={() => void copyCode()}>
                {copied ? t.payPage.copySuccess : t.payPage.copyBtn}
              </button>
            </div>
          ) : null}
          <p className="mt-4 text-sm leading-7 text-mute">
            {mailNote(order.email_status, order.email, t)}
          </p>
          <Link to={l("/download")} className="btn btn-primary mt-6">
            {t.payPage.downloadBtn}
          </Link>
        </div>
      ) : null}

      {order && !pending && !paid ? (
        <div className="mt-8">
          <p className="leading-8 text-mute">
            {order.status === "expired" ? t.payPage.orderExpired : t.payPage.orderNotDone}
          </p>
          <Link to={l(addon ? "/addon" : "/pricing")} className="btn btn-primary mt-6">
            {t.payPage.reorderBtn}
          </Link>
        </div>
      ) : null}
    </div>
  );
}

function mailNote(status: string, email: string, t: any) {
  if (status === "sent") {
    return `${t.payPage.mailSentPrefix} ${email}${t.payPage.mailSentSuffix}`;
  }
  if (status === "failed") {
    return t.payPage.mailFailed;
  }
  if (status === "skipped") {
    return t.payPage.mailSkipped;
  }
  return t.payPage.mailSending;
}
