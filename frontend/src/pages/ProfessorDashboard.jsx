import { useEffect, useMemo, useState } from "react";
import { api, errorText, formatDate, GRADE_TYPES, todayInput } from "../api/client";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import { useToast } from "../context/ToastContext";

export default function ProfessorDashboard() {
  const { push } = useToast();
  const [subjects, setSubjects] = useState([]);
  const [groups, setGroups] = useState([]);
  const [subjectId, setSubjectId] = useState("");
  const [group, setGroup] = useState("");
  const [grid, setGrid] = useState(null);
  const [drafts, setDrafts] = useState({});
  const [analytics, setAnalytics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    Promise.all([api.get("/subjects/mine"), api.get("/students/groups")])
      .then(([subjectsRes, groupsRes]) => {
        setSubjects(subjectsRes.data);
        setGroups(groupsRes.data);
        if (subjectsRes.data[0]) setSubjectId(subjectsRes.data[0].id);
        if (groupsRes.data[0]) setGroup(groupsRes.data[0]);
      })
      .catch((error) => push(errorText(error, "Не вдалося завантажити дисципліни"), "error"))
      .finally(() => setLoading(false));
  }, [push]);

  const loadJournal = async (event) => {
    event?.preventDefault();
    if (!subjectId || !group) {
      push("Оберіть дисципліну та групу", "error");
      return;
    }
    setBusy(true);
    try {
      const [gridRes, analyticsRes] = await Promise.all([
        api.get("/grades/grid", { params: { subjectId, group } }),
        api.get(`/analytics/subject/${subjectId}`, { params: { group } }),
      ]);
      setGrid(gridRes.data);
      setAnalytics(analyticsRes.data);
      const next = {};
      for (const row of gridRes.data.rows) {
        next[row.studentId] = {
          id: "",
          gradeValue: "",
          gradeType: "Екзамен",
          date: todayInput(),
        };
      }
      setDrafts(next);
    } catch (error) {
      push(errorText(error, "Не вдалося відкрити журнал"), "error");
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    if (!grid) return;
    const items = grid.rows
      .map((row) => {
        const draft = drafts[row.studentId];
        if (!draft || draft.gradeValue === "") return null;
        return {
          id: draft.id || null,
          studentId: row.studentId,
          gradeValue: Number(draft.gradeValue),
          gradeType: draft.gradeType,
          date: draft.date ? new Date(`${draft.date}T00:00:00Z`).toISOString() : null,
        };
      })
      .filter(Boolean);

    if (items.length === 0) {
      push("Вкажіть хоча б одну оцінку", "error");
      return;
    }

    setBusy(true);
    try {
      await api.post("/grades/bulk", { subjectId: grid.subjectId, items });
      push("Журнал збережено");
      await loadJournal();
    } catch (error) {
      push(errorText(error, "Не вдалося зберегти оцінки"), "error");
    } finally {
      setBusy(false);
    }
  };

  const maxBucket = useMemo(() => {
    if (!analytics?.distribution?.length) return 1;
    return Math.max(...analytics.distribution.map((item) => item.count), 1);
  }, [analytics]);

  return (
    <Layout title="Кабінет викладача">
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : subjects.length === 0 ? (
        <section className="card">
          <p>Вам ще не призначено дисциплін. Зверніться до адміністратора.</p>
        </section>
      ) : (
        <div className="space-y-6">
          <form className="card grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end" onSubmit={loadJournal}>
            <Field label="Дисципліна">
              <select className="field" value={subjectId} onChange={(event) => setSubjectId(event.target.value)}>
                {subjects.map((subject) => (
                  <option key={subject.id} value={subject.id}>
                    {subject.title} · {subject.credits} кред.
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Група">
              <select className="field" value={group} onChange={(event) => setGroup(event.target.value)}>
                {groups.length === 0 && <option value="">Груп ще немає</option>}
                {groups.map((item) => (
                  <option key={item} value={item}>{item}</option>
                ))}
              </select>
            </Field>
            <button type="submit" className="btn-primary" disabled={busy || !group}>
              Відкрити журнал
            </button>
          </form>

          {grid && (
            <section className="card">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 className="text-lg font-semibold">{grid.subjectTitle}</h2>
                  <p className="text-sm text-stone-400">Група {grid.group} · {grid.credits} кредитів</p>
                </div>
                <button type="button" className="btn-primary" onClick={save} disabled={busy}>
                  Зберегти журнал
                </button>
              </div>
              {grid.rows.length === 0 ? (
                <p className="text-sm text-stone-400">У цій групі немає студентів</p>
              ) : (
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Студент</th>
                        <th>Залікова книжка</th>
                        <th>Вже виставлено</th>
                        <th>Оцінка</th>
                        <th>Тип</th>
                        <th>Дата</th>
                      </tr>
                    </thead>
                    <tbody>
                      {grid.rows.map((row) => {
                        const draft = drafts[row.studentId] || {};
                        return (
                          <tr key={row.studentId}>
                            <td className="font-medium">{row.fullName}</td>
                            <td>{row.studentCardNumber}</td>
                            <td>
                              <div className="flex flex-wrap gap-1">
                                {row.grades.length === 0 && <span className="text-stone-500">Немає</span>}
                                {row.grades.map((grade) => (
                                  <button
                                    key={grade.id}
                                    type="button"
                                    className="rounded-full border border-eger-line px-2 py-1 text-xs hover:border-eger-gold"
                                    onClick={() =>
                                      setDrafts((current) => ({
                                        ...current,
                                        [row.studentId]: {
                                          id: grade.id,
                                          gradeValue: grade.gradeValue,
                                          gradeType: grade.gradeType,
                                          date: grade.date?.slice(0, 10) || todayInput(),
                                        },
                                      }))
                                    }
                                  >
                                    {grade.gradeType}: {grade.gradeValue} · {formatDate(grade.date)}
                                  </button>
                                ))}
                              </div>
                            </td>
                            <td>
                              <input
                                className="field w-24"
                                type="number"
                                min="0"
                                max="100"
                                value={draft.gradeValue ?? ""}
                                onChange={(event) =>
                                  setDrafts((current) => ({
                                    ...current,
                                    [row.studentId]: { ...draft, gradeValue: event.target.value },
                                  }))
                                }
                                aria-label={`Оцінка для ${row.fullName}`}
                              />
                            </td>
                            <td>
                              <select
                                className="field"
                                value={draft.gradeType || "Екзамен"}
                                onChange={(event) =>
                                  setDrafts((current) => ({
                                    ...current,
                                    [row.studentId]: { ...draft, gradeType: event.target.value },
                                  }))
                                }
                              >
                                {GRADE_TYPES.map((type) => (
                                  <option key={type}>{type}</option>
                                ))}
                              </select>
                            </td>
                            <td>
                              <input
                                className="field"
                                type="date"
                                value={draft.date || todayInput()}
                                onChange={(event) =>
                                  setDrafts((current) => ({
                                    ...current,
                                    [row.studentId]: { ...draft, date: event.target.value },
                                  }))
                                }
                              />
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
              <p className="mt-3 text-xs text-stone-500">
                Натисніть наявну оцінку, щоб змінити її. Порожнє поле не зберігається. Нове значення без вибору оцінки створює запис.
              </p>
            </section>
          )}

          {analytics && (
            <section className="grid gap-4 lg:grid-cols-[280px_1fr]">
              <article className="card">
                <h2 className="mb-3 text-lg font-semibold">Успішність групи</h2>
                <dl className="space-y-2 text-sm">
                  <Metric label="Студентів" value={analytics.studentCount} />
                  <Metric label="Оцінок" value={analytics.gradeCount} />
                  <Metric label="Середній бал" value={analytics.averageScore} />
                  <Metric label="Середній бал 4.0" value={analytics.averageGpa} />
                  <Metric label="Успішність" value={`${analytics.passRate}%`} />
                </dl>
              </article>
              <article className="card">
                <h2 className="mb-4 text-lg font-semibold">Розподіл оцінок</h2>
                <div className="space-y-3">
                  {analytics.distribution.map((bucket) => (
                    <div key={bucket.label}>
                      <div className="mb-1 flex justify-between text-xs text-stone-300">
                        <span>{bucket.label}</span>
                        <span>{bucket.count}</span>
                      </div>
                      <div className="h-2 rounded-full bg-black/40">
                        <div
                          className="h-2 rounded-full bg-gradient-to-r from-eger-green to-eger-gold"
                          style={{ width: `${(bucket.count / maxBucket) * 100}%` }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </article>
            </section>
          )}
        </div>
      )}
    </Layout>
  );
}

function Metric({ label, value }) {
  return (
    <div className="flex items-center justify-between border-b border-eger-line/60 py-1">
      <dt className="text-stone-400">{label}</dt>
      <dd className="font-semibold text-eger-gold">{value}</dd>
    </div>
  );
}
