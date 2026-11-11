import axios from "axios";

export const apiBase = (import.meta.env.VITE_API_BASE || "https://SideBox.microsoftware.app").replace(/\/$/, "");

export const downloadUrl = (import.meta.env.VITE_DOWNLOAD_URL || "https://github.com/mxlg2003/WinDock/releases/latest/download/SideBox-setup.exe").trim();

export const api = axios.create({
  baseURL: apiBase,
  timeout: 20000,
});

export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface Sku {
  sku: string;
  product_name: string;
  amount: number;
  currency: string;
  device_limit: number;
  kind: string;
  extra_devices?: number;
}

export interface Catalog {
  currency: string;
  trial_days: number;
  provider: string;
  skus: Sku[];
  addon: Sku;
}

export interface Order {
  order_id: string;
  sku: string;
  status: string;
  email: string;
  amount: number;
  currency: string;
  product_name: string;
  code_url: string;
  expires_at: string;
  paid_at: string | null;
  license_code: string | null;
  email_status: string;
  device_limit: number | null;
}

export function yuan(amountFen: number) {
  const value = amountFen / 100;
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}

export function isMockCodeUrl(codeUrl: string) {
  return codeUrl.startsWith("SideBox://mock-pay/");
}

export function isAddonSku(sku: string) {
  return sku.startsWith("add_");
}

export function apiError(error: unknown, fallback = "请求失败，请稍后再试") {
  if (axios.isAxiosError(error)) {
    const detail = error.response?.data?.detail;
    if (typeof detail === "string" && detail.trim()) {
      return detail;
    }
    if (Array.isArray(detail)) {
      const text = detail
        .map((item) => (typeof item?.msg === "string" ? item.msg : ""))
        .filter(Boolean)
        .join("；");
      if (text) {
        return text;
      }
    }
    if (!error.response) {
      return "暂时连不上许可证服务";
    }
  }
  return fallback;
}

export async function fetchCatalog() {
  const response = await api.get<ApiEnvelope<Catalog>>("/api/v1/license-payment/catalog");
  return response.data.data;
}

export async function createOrder(email: string, sku: string, code = "") {
  const response = await api.post<ApiEnvelope<Order>>("/api/v1/license-payment/orders", {
    email,
    sku,
    code,
  });
  return response.data.data;
}

export async function fetchOrder(orderId: string) {
  const response = await api.get<ApiEnvelope<Order>>(`/api/v1/license-payment/orders/${orderId}`);
  return response.data.data;
}

export async function mockPay(orderId: string) {
  const response = await api.post<ApiEnvelope<Order>>(`/api/v1/license-payment/orders/${orderId}/mock-pay`);
  return response.data.data;
}
