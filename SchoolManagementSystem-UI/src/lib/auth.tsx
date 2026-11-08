import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { api, tokenStore, unwrap } from "./api";
import type { AuthResult, CurrentUser } from "./types";

type AuthContextValue = {
  user: CurrentUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshMe: () => Promise<void>;
  setActiveCampus: (campusId: number) => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshMe = useCallback(async () => {
    if (!tokenStore.getAccess()) {
      setUser(null);
      return;
    }
    const me = await unwrap(api.get("/auth/me"));
    setUser(me as CurrentUser);
    if ((me as CurrentUser).activeCampusId) {
      tokenStore.setCampusId((me as CurrentUser).activeCampusId!);
    }
  }, []);

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
    const result = await unwrap(api.post("/auth/login", { email, password })) as AuthResult;
    tokenStore.setAccess(result.tokens.accessToken);
    if (result.user.activeCampusId) {
      tokenStore.setCampusId(result.user.activeCampusId);
    }
    setUser(result.user);
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

  const setActiveCampus = useCallback(async (campusId: number) => {
    tokenStore.setCampusId(campusId);
    await refreshMe();
  }, [refreshMe]);

  const value = useMemo(
    () => ({ user, loading, login, logout, refreshMe, setActiveCampus }),
    [user, loading, login, logout, refreshMe, setActiveCampus],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
