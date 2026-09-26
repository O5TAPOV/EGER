import { useEffect, useState } from "react";
import { ShieldCheck } from "lucide-react";
import { api, errorText } from "../api/client";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";
import { Field } from "./Modal";

export default function TwoFactorSettings() {
  const { user, refresh } = useAuth();
  const { push } = useToast();
  const [enabled, setEnabled] = useState(false);
  const [chatId, setChatId] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setEnabled(Boolean(user?.is2FAEnabled));
    setChatId(user?.telegramChatId || "");
  }, [user]);

  const save = async (event) => {
    event.preventDefault();
    setBusy(true);
    try {
      await api.put("/auth/2fa", {
        is2FAEnabled: enabled,
        telegramChatId: chatId.trim() || null,
      });
      await refresh();
      push(enabled ? "Двофакторну перевірку увімкнено" : "Двофакторну перевірку вимкнено");
    } catch (error) {
      push(errorText(error, "Не вдалося зберегти налаштування"), "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card">
      <div className="mb-3 flex items-center gap-2 text-eger-gold">
        <ShieldCheck size={18} />
        <h2 className="text-lg font-semibold text-stone-50">Двофакторна перевірка</h2>
      </div>
      <ol className="mb-4 list-decimal space-y-1 pl-5 text-sm text-stone-300">
        <li>Увімкніть перевірку. Наступний вхід запропонує код у Telegram або на пошту цього облікового запису.</li>
        <li>Для Telegram відкрийте бота EGER, надішліть /start і вставте ідентифікатор чату нижче. Без нього код у Telegram не надійде.</li>
        <li>Пошта каналу береться з облікового запису. Ідентифікатор чату для неї не потрібен.</li>
      </ol>
      <form className="grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end" onSubmit={save}>
        <Field label="Ідентифікатор чату Telegram">
          <input
            className="field"
            inputMode="numeric"
            value={chatId}
            onChange={(event) => setChatId(event.target.value.replace(/[^\d]/g, ""))}
            placeholder="Наприклад, 123456789"
          />
        </Field>
        <label className="flex items-center gap-3 rounded-lg border border-eger-line px-3 py-2 text-sm">
          <input type="checkbox" checked={enabled} onChange={(event) => setEnabled(event.target.checked)} />
          Увімкнути двофакторну перевірку
        </label>
        <button type="submit" className="btn-primary" disabled={busy}>
          Зберегти
        </button>
      </form>
    </section>
  );
}
