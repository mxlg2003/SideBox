import { apiClient } from "@/services/api/client";
import { AdminConfig, AdminSummary, AppReleaseRecord, CreateReleasePayload, LicenseRecord, PaginatedData, PaymentOrder } from "@/types/admin";

export function fetchMe(token: string) {
  return apiClient.get<{ username: string }>("/admin/me", token);
}

export function loginAdmin(username: string, password: string) {
  return apiClient.post<{ access_token: string; token_type: string; username: string }>("/admin/login", {
    username,
    password,
  });
}

export function fetchSummary(token: string) {
  return apiClient.get<AdminSummary>("/admin/summary", token);
}

export function fetchConfig(token: string) {
  return apiClient.get<AdminConfig>("/admin/config", token);
}

export function fetchLicenses(token: string, params: object) {
  return apiClient.get<PaginatedData<LicenseRecord>>("/admin/licenses", token, params);
}

export function fetchLicense(token: string, id: number) {
  return apiClient.get<LicenseRecord>(`/admin/licenses/${id}`, token);
}

export function issueLicenses(token: string, body: { email: string; kind: string; days?: number; count?: number; device_limit?: number }) {
  return apiClient.post<{ items: LicenseRecord[] }>("/admin/licenses", body, token);
}

export function unbindLicenseDevice(token: string, id: number, deviceId: string) {
  return apiClient.post<LicenseRecord>(`/admin/licenses/${id}/unbind`, { device_id: deviceId }, token);
}

export function extendLicense(token: string, id: number, days: number) {
  return apiClient.post<LicenseRecord>(`/admin/licenses/${id}/extend`, { days }, token);
}

export function revokeLicense(token: string, id: number) {
  return apiClient.post<LicenseRecord>(`/admin/licenses/${id}/revoke`, {}, token);
}

export function resendLicenseEmail(token: string, id: number) {
  return apiClient.post<LicenseRecord>(`/admin/licenses/${id}/resend-email`, {}, token);
}

export function fetchOrders(token: string, params: object) {
  return apiClient.get<PaginatedData<PaymentOrder>>("/admin/orders", token, params);
}

export function fetchOrder(token: string, orderId: string) {
  return apiClient.get<PaymentOrder>(`/admin/orders/${encodeURIComponent(orderId)}`, token);
}

export function markOrderPaid(token: string, orderId: string) {
  return apiClient.post<PaymentOrder>(`/admin/orders/${encodeURIComponent(orderId)}/mark-paid`, {}, token);
}

export function resendOrderEmail(token: string, orderId: string) {
  return apiClient.post<PaymentOrder>(`/admin/orders/${encodeURIComponent(orderId)}/resend-email`, {}, token);
}

export function fetchTrials(token: string, params: object) {
  return apiClient.get<PaginatedData<LicenseRecord>>("/admin/trials", token, params);
}

export function createTrial(token: string, email: string, sendEmail = true) {
  return apiClient.post<LicenseRecord>("/admin/trials", { email, send_email: sendEmail }, token);
}

export function testLicenseEmail(token: string, email: string) {
  return apiClient.post<{ email: string; email_status: string; email_error: string | null }>(
    "/admin/email/test",
    { email },
    token,
  );
}

export function fetchReleases(token: string) {
  return apiClient.get<{ items: AppReleaseRecord[]; total: number }>("/admin/releases", token);
}

export function createRelease(token: string, body: CreateReleasePayload) {
  return apiClient.post<AppReleaseRecord>("/admin/releases", body, token);
}

export function toggleReleaseActive(token: string, id: number) {
  return apiClient.post<AppReleaseRecord>(`/admin/releases/${id}/toggle-active`, {}, token);
}

export function deleteRelease(token: string, id: number) {
  return apiClient.delete<{ deleted: boolean; id: number }>(`/admin/releases/${id}`, token);
}
