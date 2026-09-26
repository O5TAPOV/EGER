import axios from "axios";

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "/api",
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("eger_token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const url = error.config?.url || "";
    const isAuthAttempt = url.includes("/auth/login") || url.includes("/auth/verify-2fa");
    if (error.response?.status === 401 && !isAuthAttempt) {
      localStorage.removeItem("eger_token");
      localStorage.removeItem("eger_user");
      if (!window.location.pathname.startsWith("/login")) {
        window.location.assign("/login");
      }
    }
    return Promise.reject(error);
  }
);

export function errorText(error, fallback = "Сталася помилка") {
  if (!error?.response) {
    return "Немає з'єднання з сервером";
  }
  return error.response.data?.message || fallback;
}

export const CURRENT_GRADE_TYPES = [
  "Відвідування",
  "Домашнє завдання",
  "Практична",
  "Модульний контроль",
];

export const FINAL_GRADE_TYPES = ["Залік", "Екзамен"];

export const GRADE_TYPES = [...CURRENT_GRADE_TYPES, ...FINAL_GRADE_TYPES];

export function formatDate(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("uk-UA").format(date);
}

export function todayInput() {
  return new Date().toISOString().slice(0, 10);
}
