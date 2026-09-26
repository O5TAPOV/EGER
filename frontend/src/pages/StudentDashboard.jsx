import { useEffect, useState } from "react";
import { ShieldCheck } from "lucide-react";
import { api, errorText, formatDate } from "../api/client";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";

export default function StudentDashboard() {
  const { user, refresh } = useAuth();
  const { push } = useToast();
  const [profile, setProfile] = useState(null);
  const [analytics, setAnalytics] = useState(null);
  const [enabled, setEnabled] = useState(false);
  const [chatId, setChatId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    Promise.all([api.get("/students/me"), api.get("/analytics/me")])
      .then(([profileRes, analyticsRes]) => {
        setProfile(profileRes.data);
        setAnalytics(analyticsRes.data);
      })
      .catch((error) => push(errorText(error, "Не вдалося завантажити транскрипт"), "error"))
      .finally(() => setLoading(false));
  }, [push]);

  useEffect(() => {
    setEnabled(Boolean(user?.is2FAEnabled));
    setChatId(user?.telegramChatId || "");
  }, [user]);

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
    <Layout title="Кабінет студента">
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : !profile || !analytics ? (
        <p className="text-stone-400">Не вдалося завантажити дані кабінету</p>
      ) : (
        <div className="space-y-6">
          <section className="grid gap-4 md:grid-cols-[1.4fr_1fr]">
            <article className="card">
              <p className="text-sm text-stone-400">{profile.group} · вступ {profile.enrollmentYear}</p>
              <h2 className="mt-1 text-2xl font-semibold">{profile.fullName}</h2>
              <p className="mt-2 text-sm text-stone-300">Залікова книжка {profile.studentCardNumber}</p>
              <p className="text-sm text-stone-500">{profile.email}</p>
            </article>
            <article className="card">
              <p className="text-xs uppercase tracking-wide text-stone-400">Загальний середній бал</p>
              <p className="mt-2 text-5xl font-semibold text-eger-gold">{analytics.averageGpa}</p>
              <p className="mt-2 text-sm text-stone-300">Середній бал {analytics.averageScore} · успішність {analytics.passRate}%</p>
              <p className="text-xs text-stone-500">Зважено за кредитами дисциплін за шкалою 4.0, прохідний бал — 60</p>
            </article>
          </section>

          <section className="card">
            <h2 className="mb-4 text-lg font-semibold">Транскрипт</h2>
            {analytics.transcript.length === 0 ? (
              <p className="text-sm text-stone-400">Оцінок ще немає</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Дисципліна</th>
                      <th>Кредити</th>
                      <th>Тип</th>
                      <th>Оцінка</th>
                        <th>Бал 4.0</th>
                      <th>Викладач</th>
                      <th>Дата</th>
                    </tr>
                  </thead>
                  <tbody>
                    {analytics.transcript.map((item) => (
                      <tr key={item.gradeId}>
                        <td>{item.subjectTitle}</td>
                        <td>{item.credits}</td>
                        <td>{item.gradeType}</td>
                        <td className={item.gradeValue >= 60 ? "text-eger-mint" : "text-red-300"}>{item.gradeValue}</td>
                        <td>{item.gpaPoints}</td>
                        <td>{item.professorName}</td>
                        <td>{formatDate(item.date)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

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
