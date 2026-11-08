import axios from "axios";
import type { ApiResponse } from "./types";

const ACCESS_KEY = "schoolms.accessToken";
const CAMPUS_KEY = "schoolms.activeCampusId";

export const tokenStore = {
  getAccess: () => localStorage.getItem(ACCESS_KEY),
  setAccess: (token: string) => localStorage.setItem(ACCESS_KEY, token),
  clearAccess: () => localStorage.removeItem(ACCESS_KEY),
  getCampusId: () => {
    const raw = localStorage.getItem(CAMPUS_KEY);
    return raw ? Number(raw) : null;
  },
  setCampusId: (id: number | null) => {
    if (id == null) localStorage.removeItem(CAMPUS_KEY);
    else localStorage.setItem(CAMPUS_KEY, String(id));
  },
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

export async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await promise;
  if (!data.succeeded) {
    throw new Error(data.message || "Request failed");
  }
  return data.data as T;
}
