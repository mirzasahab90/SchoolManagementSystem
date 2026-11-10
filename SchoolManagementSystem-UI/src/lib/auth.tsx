import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { api, onInvalidCampus, tokenStore, unwrap } from "./api";
import type { AuthResult, CurrentUser } from "./types";

type AuthContextValue = {
  user: CurrentUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshMe: () => Promise<void>;
  /** Null = Main Campus "All campuses". */
  setActiveCampus: (campusId: number | null) => Promise<void>;
  can: (moduleKey: string, action: string) => boolean;
  changePassword: (current: string, next: string) => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

function normalizeUser(raw: CurrentUser): CurrentUser {
  return {
    ...raw,
    permissions: raw.permissions ?? {},
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshMe = useCallback(async () => {
    if (!tokenStore.getAccess()) {
      setUser(null);
      return;
    }
    const me = normalizeUser((await unwrap(api.get("/auth/me"))) as CurrentUser);
    setUser(me);
    tokenStore.setCampusId(me.activeCampusId ?? null);
  }, []);

  useEffect(
    () =>
      onInvalidCampus(() => {
        void refreshMe().catch(() => undefined);
      }),
    [refreshMe],
  );

  useEffect(() => {
    (async () => {
      try {
        if (tokenStore.getAccess()) {
          await refreshMe();
        }
      } catch {
        tokenStore.clearAccess();
        setUser(null);
      } finally {
        setLoading(false);
      }
    })();
  }, [refreshMe]);

  const login = useCallback(async (email: string, password: string) => {
    const result = (await unwrap(api.post("/auth/login", { email, password }))) as AuthResult;
    tokenStore.setAccess(result.tokens.accessToken);
    const next = normalizeUser(result.user);
    tokenStore.setCampusId(next.activeCampusId ?? null);
    setUser(next);
  }, []);

  const logout = useCallback(async () => {
    try {
      await api.post("/auth/logout");
    } catch {
      /* ignore */
    }
    tokenStore.clearAccess();
    tokenStore.setCampusId(null);
    setUser(null);
  }, []);

  const setActiveCampus = useCallback(
    async (campusId: number | null) => {
      tokenStore.setCampusId(campusId);
      await refreshMe();
    },
    [refreshMe],
  );

  const changePassword = useCallback(
    async (current: string, next: string) => {
      // The server signs out other sessions and hands this device fresh tokens.
      const result = (await unwrap(
        api.post("/auth/change-password", { currentPassword: current, newPassword: next }),
      )) as AuthResult;
      tokenStore.setAccess(result.tokens.accessToken);
      await refreshMe();
    },
    [refreshMe],
  );

  const can = useCallback(
    (moduleKey: string, action: string) => {
      if (!user) return false;
      if (user.isSuperAdmin) return true;
      const actions = user.permissions?.[moduleKey] ?? [];
      return actions.some((a) => a.toLowerCase() === action.toLowerCase());
    },
    [user],
  );

  const value = useMemo(
    () => ({ user, loading, login, logout, refreshMe, setActiveCampus, can, changePassword }),
    [user, loading, login, logout, refreshMe, setActiveCampus, can, changePassword],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
