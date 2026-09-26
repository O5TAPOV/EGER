import { useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { GraduationCap, KeyRound } from "lucide-react";
import { errorText } from "../api/client";
import Modal, { Field } from "../components/Modal";
import { pathForRole, useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";

export default function LoginPage() {
  const { user, ready, login, verify, sendCode } = useAuth();
  const navigate = useNavigate();
  const { push } = useToast();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [pending, setPending] = useState(null);
  const [code, setCode] = useState("");

  if (!ready) {
    return <div className="grid min-h-screen place-items-center text-stone-400">Завантаження...</div>;
  }

  if (user) {
    return <Navigate to={pathForRole(user.role)} replace />;
  }

  const onSubmit = async (event) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const data = await login(email.trim(), password);
      if (data.requires2FA) {
        setPending({ userId: data.userId, phase: "choose", message: data.message || "" });
        setCode("");
        return;
      }
      push("Вхід виконано");
      navigate(pathForRole(data.user.role), { replace: true });
    } catch (err) {
      setError(errorText(err, "Не вдалося увійти"));
    } finally {
      setBusy(false);
    }
  };

  const onSend = async (channel) => {
    setBusy(true);
    setError("");
    try {
      const data = await sendCode(pending.userId, channel);
      setPending({ userId: pending.userId, phase: "code", message: data.message || "" });
      setCode("");
    } catch (err) {
      setError(errorText(err, "Не вдалося надіслати код"));
    } finally {
      setBusy(false);
    }
  };

  const onVerify = async (event) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const data = await verify(pending.userId, code.trim());
      push("Вхід підтверджено");
      navigate(pathForRole(data.user.role), { replace: true });
    } catch (err) {
      setError(errorText(err, "Невірний код"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <section className="hidden flex-col justify-between bg-eger-panel p-12 lg:flex">
        <div className="flex items-center gap-3 text-eger-gold">
          <span className="grid h-12 w-12 place-items-center rounded-2xl bg-eger-green">
            <GraduationCap />
          </span>
          <span className="text-2xl font-bold tracking-wide">EGER</span>
        </div>
        <div>
          <h1 className="max-w-md text-4xl font-semibold leading-tight text-stone-50">
            Навчальна система оцінювання та звітності
          </h1>
          <p className="mt-4 max-w-md text-stone-400">
            Журнали, транскрипти та аналітика успішності для адміністрації, викладачів і студентів.
          </p>
        </div>
        <p className="text-sm text-stone-500">Глибокий зелений. Темний бурштин. Академічний облік.</p>
      </section>

      <section className="grid place-items-center px-4 py-10">
        <form className="card w-full max-w-md" onSubmit={onSubmit}>
          <div className="mb-6 flex items-center gap-3 lg:hidden">
            <GraduationCap className="text-eger-gold" />
            <div>
              <p className="text-lg font-bold text-eger-gold">EGER</p>
              <p className="text-xs text-stone-400">Навчальна система оцінювання та звітності</p>
            </div>
          </div>
          <h2 className="mb-1 text-xl font-semibold">Вхід до системи</h2>
          <p className="mb-6 text-sm text-stone-400">Вкажіть пошту та пароль облікового запису</p>
          <div className="space-y-4">
            <Field label="Електронна пошта">
              <input
                className="field"
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="admin@eger.ua"
              />
            </Field>
            <Field label="Пароль">
              <input
                className="field"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="Введіть пароль"
              />
            </Field>
          </div>
          {error && !pending && <p className="mt-4 text-sm text-red-300">{error}</p>}
          <button className="btn-primary mt-6 w-full" type="submit" disabled={busy}>
            <KeyRound size={16} />
            {busy ? "Вхід..." : "Увійти"}
          </button>
        </form>
      </section>

      {pending && (
        <Modal title="Підтвердження входу" onClose={() => { setPending(null); setError(""); }}>
          {pending.phase === "code" ? (
            <form onSubmit={onVerify} className="space-y-4">
              <p className="text-sm text-stone-300">
                {pending.message || "Введіть 6-значний код. Діє лише останній надісланий код."}
              </p>
              <Field label="Код підтвердження">
                <input
                  className="field tracking-[0.4em]"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  pattern="\d{6}"
                  maxLength={6}
                  required
                  value={code}
                  onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
                  placeholder="000000"
                />
              </Field>
              {error && <p className="text-sm text-red-300">{error}</p>}
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  className="btn-ghost"
                  onClick={() => { setPending({ ...pending, phase: "choose" }); setError(""); setCode(""); }}
                >
                  Інший канал
                </button>
                <button type="submit" className="btn-primary" disabled={busy || code.length !== 6}>
                  Підтвердити
                </button>
              </div>
            </form>
          ) : (
            <div className="space-y-4">
              <p className="text-sm text-stone-300">
                Оберіть, куди надіслати 6-значний код. Діє лише останній надісланий код, 5 хвилин.
              </p>
              {error && <p className="text-sm text-red-300">{error}</p>}
              <div className="flex flex-col gap-2">
                <button type="button" className="btn-primary" disabled={busy} onClick={() => onSend("telegram")}>
                  Надіслати код у Telegram
                </button>
                <button type="button" className="btn-primary" disabled={busy} onClick={() => onSend("email")}>
                  Надіслати код на пошту
                </button>
                <button type="button" className="btn-ghost" onClick={() => { setPending(null); setError(""); }}>
                  Скасувати
                </button>
              </div>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
