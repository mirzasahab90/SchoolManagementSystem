import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router-dom";

/** Keys that are list state, not filters: they never count as "active" and survive "Clear filters" (except page). */
const NOT_FILTERS = new Set(["page", "pageSize", "sort", "dir", "tab", "view", "open"]);

export type SortDir = "asc" | "desc";

/**
 * List filters kept in the URL so back, refresh and shared links keep them.
 * A value equal to its default is left out of the URL. Changing any filter sends the list back to page 1.
 */
export function useUrlFilters<T extends Record<string, string>>(defaults: T) {
  const [params, setParams] = useSearchParams();
  // Defaults are a literal at the call site; key them by content so the memo below stays stable.
  const key = JSON.stringify(defaults);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const base = useMemo(() => defaults, [key]);

  const values = useMemo(() => {
    const out = { ...base } as Record<string, string>;
    for (const k of Object.keys(base)) if (params.has(k)) out[k] = params.get(k) ?? "";
    return out as T;
  }, [params, base]);

  const set = useCallback(
    (patch: Partial<T>) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          for (const [k, v] of Object.entries(patch)) {
            if (v === undefined || v === base[k]) next.delete(k);
            else next.set(k, v as string);
          }
          // Any filter change starts again from the first page.
          if (!("page" in patch) && Object.keys(patch).some((k) => !NOT_FILTERS.has(k) || k === "pageSize")) next.delete("page");
          return next;
        },
        { replace: true },
      );
    },
    [setParams, base],
  );

  /** Resets every filter to its default; sort and page size stay. */
  const clear = useCallback(() => {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const k of Object.keys(base)) if (!NOT_FILTERS.has(k) || k === "page") next.delete(k);
        return next;
      },
      { replace: true },
    );
  }, [setParams, base]);

  const activeCount = useMemo(
    () => Object.keys(base).filter((k) => !NOT_FILTERS.has(k) && values[k] !== base[k]).length,
    [values, base],
  );

  return { values, set, clear, activeCount };
}

/** Case-insensitive "does any of these fields contain the search text". */
export function matches(q: string, ...fields: (string | number | null | undefined)[]) {
  const needle = q.trim().toLowerCase();
  if (!needle) return true;
  return fields.some((f) => f != null && String(f).toLowerCase().includes(needle));
}

/** Sorts a copy of the list by a value picked from each item; nulls go last. */
export function sortRows<T>(rows: T[], pick: ((row: T) => string | number | null | undefined) | undefined, dir: SortDir) {
  if (!pick) return rows;
  const sign = dir === "desc" ? -1 : 1;
  return [...rows].sort((a, b) => {
    const x = pick(a);
    const y = pick(b);
    if (x == null && y == null) return 0;
    if (x == null) return 1;
    if (y == null) return -1;
    if (typeof x === "number" && typeof y === "number") return (x - y) * sign;
    return String(x).localeCompare(String(y), undefined, { numeric: true, sensitivity: "base" }) * sign;
  });
}

/** Client-side paging slice. */
export function pageOf<T>(rows: T[], page: number, pageSize: number) {
  const last = Math.max(1, Math.ceil(rows.length / pageSize));
  const p = Math.min(Math.max(1, page), last);
  return rows.slice((p - 1) * pageSize, p * pageSize);
}

export const PAGE_SIZES = [10, 25, 50, 100];
