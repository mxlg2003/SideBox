export interface PaginatedData<T> {
  items: T[];
  page: number;
  size: number;
  total: number;
  pages: number;
}

export interface DeviceBinding {
  id: number;
  device_id: string;
  device_name: string;
  bound_at: string | null;
}

export interface LicenseRecord {
  id: number;
  code: string;
  email: string;
  kind: "pro" | "trial" | string;
  permanent: boolean;
  revoked: boolean;
  expires_at: string | null;
  created_at: string | null;
  device_count: number;
  device_limit: number;
  status: "valid" | "expired" | "revoked" | string;
  devices?: DeviceBinding[];
  email_status?: string;
  email_error?: string | null;
}

export interface PaymentOrder {
  order_id: string;
  sku: string;
  status: string;
  email: string;
  amount: number;
  currency: string;
  product_name: string;
  code_url: string;
  expires_at: string | null;
  paid_at: string | null;
  license_code: string | null;
  email_status: string;
  provider?: string;
  wechat_transaction_id?: string | null;
  email_error?: string | null;
  created_at?: string | null;
}

export interface AdminSku {
  sku: string;
  product_name: string;
  amount: number;
  currency: string;
  device_limit: number;
}

export interface AdminAddon {
  sku: string;
  product_name: string;
  amount: number;
  extra_devices: number;
}

export interface AdminConfig {
  provider: string;
  pro_price_fen: number;
  trial_days: number;
  device_limit: number;
  product_name: string;
  smtp_configured: boolean;
  wechat_ready: boolean;
  skus?: AdminSku[];
  addon?: AdminAddon;
}

export interface AdminSummary {
  licenses: {
    pro_active: number;
    trial_active: number;
    expired_or_revoked: number;
    total: number;
  };
  orders: {
    pending: number;
    paid: number;
    expired: number;
    today_paid_count: number;
    today_paid_amount: number;
  };
  trials: number;
  device_bindings: number;
  recent_orders: PaymentOrder[];
}

export interface AppReleaseRecord {
  id: number;
  version: string;
  min_supported_version: string;
  download_url: string;
  sha256: string;
  changelog: string;
  mandatory: boolean;
  is_active: boolean;
  created_at: string | null;
  updated_at: string | null;
}

export interface CreateReleasePayload {
  version: string;
  min_supported_version?: string;
  download_url: string;
  sha256?: string;
  changelog?: string;
  mandatory?: boolean;
  is_active?: boolean;
}
