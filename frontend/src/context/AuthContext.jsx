import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { api } from "../api/client";

const AuthContext = createContext(null);

export function pathForRole(role) {
  if (role === "Admin") return "/admin";
  if (role === "Professor") return "/professor";
  if (role === "Student") return "/student";
  return "/login";
}

export function roleLabel(role) {
  if (role === "Admin") return "Адміністратор";
  if (role === "Professor") return "Викладач";
  if (role === "Student") return "Студент";
  return role || "";
}

function persistSession(data) {
  localStorage.setItem("eger_token", data.token);
  localStorage.setItem("eger_user", JSON.stringify(data.user));
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem("eger_token");
    if (!token) {
      setReady(true);
      return;
    }
    api
      .get("/auth/me")
      .then((response) => {
        setUser(response.data);
        localStorage.setItem("eger_user", JSON.stringify(response.data));
      })
      .catch(() => {
        localStorage.removeItem("eger_token");
        localStorage.removeItem("eger_user");
        setUser(null);
      })
      .finally(() => setReady(true));
  }, []);

  const login = async (email, password) => {
    const { data } = await api.post("/auth/login", { email, password });
    if (!data.requires2FA) {
      persistSession(data);
      setUser(data.user);
    }
    return data;
  };

  const sendCode = async (userId, channel) => {
    const { data } = await api.post("/auth/2fa/send", { userId, channel });
    return data;
  };

  const verify = async (userId, code) => {
    const { data } = await api.post("/auth/verify-2fa", { userId, code });
    persistSession(data);
    setUser(data.user);
    return data;
  };

  const logout = async () => {
    try {
      await api.post("/auth/logout");
    } catch {
      /* сесію все одно прибираємо локально */
    }
    localStorage.removeItem("eger_token");
    localStorage.removeItem("eger_user");
    setUser(null);
  };

  const refresh = async () => {
    const { data } = await api.get("/auth/me");
    setUser(data);
    localStorage.setItem("eger_user", JSON.stringify(data));
    return data;
  };

  const value = useMemo(
    () => ({ user, ready, login, verify, sendCode, logout, refresh }),
    [user, ready]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("Авторизація недоступна");
  }
  return context;
}
