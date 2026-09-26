import { useEffect, useState } from "react";
import { ShieldCheck } from "lucide-react";
import { api, errorText } from "../api/client";
import Gradebook from "../components/Gradebook";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import StudentCardView from "../components/StudentCard";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";

export default function StudentDashboard() {
  const { user, refresh } = useAuth();
  const { push } = useToast();
  const [card, setCard] = useState(null);
  const [activeSubjectId, setActiveSubjectId] = useState("");
  const [enabled, setEnabled] = useState(false);
  const [chatId, setChatId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api
      .get("/academic/card/me")
      .then((response) => setCard(response.data))
      .catch((error) => push(errorText(error, "Не вдалося завантажити картку"), "error"))
      .finally(() => setLoading(false));
  }, [push]);

  useEffect(() => {
    setEnabled(Boolean(user?.is2FAEnabled));
    setChatId(user?.telegramChatId || "");
  }, [user]);

  const openSubject = (subject) => {
    setActiveSubjectId((current) => (current === subject.subjectId ? "" : subject.subjectId));
  };

  const saveTwoFactor = async (event) => {
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
    <Layout title="Картка студента">
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : !card ? (
        <p className="text-stone-400">Не вдалося завантажити картку</p>
      ) : (
        <div className="space-y-6">
          <StudentCardView card={card} activeSubjectId={activeSubjectId} onSubject={openSubject} />
          {activeSubjectId && (
            <Gradebook subjectId={activeSubjectId} group={card.group} studentId={card.studentId} />
          )}

          <section className="card">
            <div className="mb-3 flex items-center gap-2 text-eger-gold">
              <ShieldCheck size={18} />
              <h2 className="text-lg font-semibold text-stone-50">Двофакторна перевірка</h2>
            </div>
            <ol className="mb-4 list-decimal space-y-1 pl-5 text-sm text-stone-300">
              <li>Відкрийте Telegram і знайдіть бота EGER, якого налаштував адміністратор.</li>
              <li>Надішліть команду /start. Бот відповість числовим ідентифікатором чату.</li>
              <li>Вставте цей ідентифікатор у поле нижче.</li>
              <li>Увімкніть перевірку та збережіть. Наступний вхід попросить 6-значний код із Telegram.</li>
            </ol>
            <form className="grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end" onSubmit={saveTwoFactor}>
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
                Увімкнути код із Telegram
              </label>
              <button type="submit" className="btn-primary" disabled={busy}>
                Зберегти
              </button>
            </form>
          </section>
        </div>
      )}
    </Layout>
  );
}
