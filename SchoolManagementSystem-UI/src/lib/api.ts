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
  /** Null = no campus header (HQ "All campuses", or server default). */
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
