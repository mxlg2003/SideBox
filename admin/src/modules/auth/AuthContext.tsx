import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { getApiErrorMessage } from "@/services/api/client";
import { fetchMe, loginAdmin } from "@/services/api/resources";
import {
  clearStoredToken,
  clearStoredUsername,
  getStoredToken,
  getStoredUsername,
  setStoredToken,
  setStoredUsername,
} from "@/modules/auth/auth-storage";

interface AuthContextValue {
  token: string | null;
  username: string | null;
  initializing: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [username, setUsername] = useState<string | null>(() => getStoredUsername());
  const [initializing, setInitializing] = useState(true);

  const logout = useCallback(() => {
    clearStoredToken();
    clearStoredUsername();
    setToken(null);
    setUsername(null);
  }, []);

  useEffect(() => {
    const run = async () => {
      if (!token) {
        setInitializing(false);
        return;
      }

      try {
        const me = await fetchMe(token);
        setUsername(me.username);
        setStoredUsername(me.username);
      } catch {
        logout();
      } finally {
        setInitializing(false);
      }
    };

    void run();
  }, [logout, token]);

  const login = useCallback(async (name: string, password: string) => {
    try {
      const response = await loginAdmin(name, password);
      setStoredToken(response.access_token);
      setStoredUsername(response.username);
      setToken(response.access_token);
      setUsername(response.username);
    } catch (error) {
      throw new Error(getApiErrorMessage(error, "登录失败"));
    }
  }, []);

  const value = useMemo(
    () => ({
      token,
      username,
      initializing,
      login,
      logout,
    }),
    [initializing, login, logout, token, username],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return context;
}
