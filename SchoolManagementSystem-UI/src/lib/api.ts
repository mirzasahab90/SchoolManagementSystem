import axios from "axios";
import type { ApiResponse } from "./types";

const ACCESS_KEY = "schoolms.accessToken";
const CAMPUS_KEY = "schoolms.activeCampusId";

function read(key: string) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string | null) {
  try {
    if (value == null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    /* storage unavailable */
  }
}

export const tokenStore = {
  getAccess: () => read(ACCESS_KEY),
  setAccess: (token: string) => write(ACCESS_KEY, token),
  clearAccess: () => write(ACCESS_KEY, null),
  /** Null = no campus header (Main Campus "All campuses", or server default). */
  getCampusId: () => {
    const raw = read(CAMPUS_KEY);
    return raw ? Number(raw) : null;
  },
  setCampusId: (id: number | null) => write(CAMPUS_KEY, id == null ? null : String(id)),
};

export const api = axios.create({
  baseURL: "/api",
  withCredentials: true,
});

api.interceptors.request.use((config) => {
  const token = tokenStore.getAccess();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  const campusId = tokenStore.getCampusId();
  if (campusId) {
    config.headers["X-Campus-Id"] = String(campusId);
  }
  return config;
});

/** Listeners told when the server rejects the stored working campus (e.g. access was removed). */
const invalidCampusListeners = new Set<() => void>();

export function onInvalidCampus(listener: () => void) {
  invalidCampusListeners.add(listener);
  return () => {
    invalidCampusListeners.delete(listener);
  };
}

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const response = error?.response;
    if (response?.status === 403 && response.headers?.["x-campus-invalid"] === "1") {
      tokenStore.setCampusId(null);
      invalidCampusListeners.forEach((fn) => fn());
    }
    const message = response?.data?.message;
    return Promise.reject(message ? new Error(message) : error);
  },
);

export async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await promise;
  if (!data.succeeded) {
    throw new Error(data.message || "Request failed");
  }
  return data.data as T;
}

/** Downloads a protected file through the API (auth + campus headers) and returns it with its file name. */
export async function downloadBlob(url: string): Promise<{ blob: Blob; fileName: string | null }> {
  const res = await api.get(url, { responseType: "blob" });
  const disposition = String(res.headers["content-disposition"] ?? "");
  const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1] ?? match[2]) : null;
  return { blob: res.data as Blob, fileName };
}

/** Saves a blob to the user's device under the given name. */
export function saveBlob(blob: Blob, fileName: string) {
  const href = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = href;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
}
