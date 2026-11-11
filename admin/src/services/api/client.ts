import axios, { AxiosError } from "axios";

export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  data: T;
}

const baseURL = import.meta.env.VITE_API_BASE_URL ?? "https://SideBox.microsoftware.app/api/v1";

const axiosInstance = axios.create({
  baseURL,
  timeout: 20000,
});

function authHeaders(token?: string | null) {
  if (!token) {
    return undefined;
  }

  return {
    Authorization: `Bearer ${token}`,
  };
}

function unwrap<T>(response: { data: ApiEnvelope<T> }) {
  if (!response.data.success) {
    throw new Error(response.data.message || "请求失败");
  }
  return response.data.data;
}

export function getApiErrorMessage(error: unknown, fallback = "请求失败") {
  if (axios.isAxiosError(error)) {
    return (
      (typeof error.response?.data?.detail === "string" && error.response.data.detail) ||
      (typeof error.response?.data?.message === "string" && error.response.data.message) ||
      error.message ||
      fallback
    );
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallback;
}

export const apiClient = {
  async get<T>(url: string, token?: string | null, params?: object) {
    const response = await axiosInstance.get<ApiEnvelope<T>>(url, {
      headers: authHeaders(token),
      params,
    });
    return unwrap(response);
  },
  async post<T>(url: string, body?: unknown, token?: string | null) {
    const response = await axiosInstance.post<ApiEnvelope<T>>(url, body, {
      headers: authHeaders(token),
    });
    return unwrap(response);
  },
  async delete<T>(url: string, token?: string | null) {
    const response = await axiosInstance.delete<ApiEnvelope<T>>(url, {
      headers: authHeaders(token),
    });
    return unwrap(response);
  },
  axiosError(error: unknown) {
    return error as AxiosError;
  },
};
