import { useEffect, useState } from "react";
import { fetchCatalog, type Catalog, type Sku } from "@/lib/api";

export const fallbackCatalog: Catalog = {
  currency: "CNY",
  trial_days: 14,
  provider: "",
  skus: [
    { sku: "buy_1", product_name: "SideBox Pro 1 台", amount: 2900, currency: "CNY", device_limit: 1, kind: "buy" },
    { sku: "buy_2", product_name: "SideBox Pro 2 台", amount: 3900, currency: "CNY", device_limit: 2, kind: "buy" },
    { sku: "buy_3", product_name: "SideBox Pro 3 台", amount: 4900, currency: "CNY", device_limit: 3, kind: "buy" },
  ],
  addon: {
    sku: "add_1",
    product_name: "增购 1 台",
    amount: 1000,
    currency: "CNY",
    device_limit: 1,
    kind: "add",
    extra_devices: 1,
  },
};

export function lowestPerDevice(skus: Sku[]) {
  return skus.reduce((best, item) =>
    item.amount / item.device_limit < best.amount / best.device_limit ? item : best,
  );
}

export function useCatalog() {
  const [catalog, setCatalog] = useState<Catalog>(fallbackCatalog);
  const [source, setSource] = useState<"loading" | "live" | "fallback">("loading");

  useEffect(() => {
    let cancelled = false;
    fetchCatalog()
      .then((data) => {
        if (cancelled) {
          return;
        }
        if (data.skus?.length) {
          setCatalog(data);
          setSource("live");
          return;
        }
        setSource("fallback");
      })
      .catch(() => {
        if (!cancelled) {
          setSource("fallback");
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return { catalog, live: source === "live", source };
}
