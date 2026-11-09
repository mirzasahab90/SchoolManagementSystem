import { useSyncExternalStore } from "react";

export type Theme = "light" | "dark";

const THEME_KEY = "schoolms.theme";

function readStored(): Theme | null {
  try {
    const value = localStorage.getItem(THEME_KEY);
    return value === "light" || value === "dark" ? value : null;
  } catch {
    return null;
  }
}

function systemTheme(): Theme {
  return typeof window !== "undefined" && window.matchMedia?.("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

let current: Theme = readStored() ?? systemTheme();
const listeners = new Set<() => void>();

function apply(theme: Theme) {
  document.documentElement.classList.toggle("dark", theme === "dark");
}

/** Call once before first render so the page never flashes the wrong theme. */
export function initTheme() {
  apply(current);
}

export function setTheme(next: Theme) {
  current = next;
  try {
    localStorage.setItem(THEME_KEY, next);
  } catch {
    /* storage unavailable: theme still applies for this visit */
  }
  apply(next);
  listeners.forEach((fn) => fn());
}

function subscribe(fn: () => void) {
  listeners.add(fn);
  return () => {
    listeners.delete(fn);
  };
}

export function useTheme() {
  const theme = useSyncExternalStore(subscribe, () => current);
  return {
    theme,
    setTheme,
    toggle: () => setTheme(theme === "dark" ? "light" : "dark"),
  };
}
