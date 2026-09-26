import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, CURRENT_GRADE_TYPES, errorText, FINAL_GRADE_TYPES, todayInput } from "../api/client";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import { JournalTable, statusBadge } from "../components/StudentCard";
import { useToast } from "../context/ToastContext";

const emptyDraft = () => ({
  id: "",
  gradeValue: "",
  gradeType: "Відвідування",
  date: todayInput(),
});

export default function ProfessorDashboard() {
  const { push } = useToast();
  const [subjects, setSubjects] = useState([]);
  const [groups, setGroups] = useState([]);
  const [subjectId, setSubjectId] = useState("");
  const [group, setGroup] = useState("");
  const [statement, setStatement] = useState(null);
  const [openId, setOpenId] = useState("");
  const [drafts, setDrafts] = useState({});
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

  const loadStatement = async (event) => {
    event?.preventDefault();
    if (!subjectId || !group) {
      push("Оберіть дисципліну та групу", "error");
      return;
    }
    setBusy(true);
    try {
      const { data } = await api.get("/academic/statement", { params: { subjectId, group } });
      setStatement(data);
      setOpenId("");
      const next = {};
      for (const row of data.students) next[row.studentId] = emptyDraft();
      setDrafts(next);
    } catch (error) {
      push(errorText(error, "Не вдалося відкрити відомість"), "error");
    } finally {
      setBusy(false);
    }
  };

  const saveRow = async (studentId) => {
    const draft = drafts[studentId];
    if (!statement || !draft || draft.gradeValue === "") {
      push("Вкажіть бали", "error");
      return;
    }
    setBusy(true);
    try {
      await api.post("/grades/bulk", {
        subjectId: statement.subjectId,
        items: [
          {
            id: draft.id || null,
            studentId,
            gradeValue: Number(draft.gradeValue),
            gradeType: draft.gradeType,
            date: draft.date ? new Date(`${draft.date}T00:00:00Z`).toISOString() : null,
          },
        ],
      });
      push(draft.id ? "Рядок журналу оновлено" : "Рядок додано до журналу");
      await loadStatement();
      setOpenId(studentId);
    } catch (error) {
      push(errorText(error, "Не вдалося зберегти бали"), "error");
    } finally {
      setBusy(false);
    }
  };

  const patchDraft = (studentId, patch) => {
    setDrafts((current) => ({
      ...current,
      [studentId]: { ...(current[studentId] || emptyDraft()), ...patch },
    }));
  };

  return (
    <Layout title="Відомість групи">
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : subjects.length === 0 ? (
        <section className="card">
          <p>Вам ще не призначено дисциплін. Зверніться до адміністратора.</p>
        </section>
      ) : (
        <div className="space-y-6">
          <form className="card grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end" onSubmit={loadStatement}>
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
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </Field>
            <button type="submit" className="btn-primary" disabled={busy || !group}>
              Відкрити відомість
            </button>
          </form>

          {statement && (
            <>
              <section className="grid gap-4 md:grid-cols-3">
                <article className="card md:col-span-2">
                  <h2 className="text-lg font-semibold">{statement.subjectTitle}</h2>
                  <p className="mt-1 text-sm text-stone-400">
                    Група {statement.group} · {statement.credits} кред. · викладає {statement.professorNames.join(", ") || "—"}
                  </p>
                  <p className="mt-2 text-xs text-stone-500">
                    Поточні до {statement.currentMax}, підсумок до {statement.finalMax}, поріг зарахування {statement.passThreshold}.
                  </p>
                </article>
                <article className="card">
                  <p className="text-xs uppercase tracking-wide text-stone-400">Середній бал групи</p>
                  <p className="mt-2 text-4xl font-semibold text-eger-gold">{statement.classAverage}</p>
                  <p className="text-xs text-stone-500">за 100-бальною шкалою</p>
                </article>
              </section>

              <section className="card">
                <h2 className="mb-3 text-lg font-semibold">Боржники</h2>
                {statement.debtors.length === 0 ? (
                  <p className="text-sm text-stone-400">У цій відомості боржників немає</p>
                ) : (
                  <ul className="space-y-2 text-sm">
                    {statement.debtors.map((item) => (
                      <li key={item.studentId} className="flex flex-wrap items-center justify-between gap-2 border-b border-eger-line/60 py-2">
                        <Link className="font-medium text-eger-gold hover:underline" to={`/professor/students/${item.studentId}`}>
                          {item.fullName}
                        </Link>
                        <span className="text-stone-300">
                          {item.total} · {item.reason}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </section>

              <section className="card">
                <h2 className="mb-4 text-lg font-semibold">Журнал групи</h2>
                {statement.students.length === 0 ? (
                  <p className="text-sm text-stone-400">У цій групі немає студентів</p>
                ) : (
                  <div className="space-y-3">
                    {statement.students.map((row) => {
                      const open = openId === row.studentId;
                      const draft = drafts[row.studentId] || emptyDraft();
                      return (
                        <article key={row.studentId} className="rounded-xl border border-eger-line/80 p-3">
                          <div className="flex flex-wrap items-center gap-3">
                            <Link className="font-medium text-eger-gold hover:underline" to={`/professor/students/${row.studentId}`}>
                              {row.fullName}
                            </Link>
                            <span className="text-xs text-stone-500">{row.studentCardNumber}</span>
                            <span className="text-sm text-stone-300">
                              {row.currentPoints}/{statement.currentMax}
                              {" · "}
                              {row.hasFinal ? `${row.finalPoints}/${statement.finalMax}` : "підсумок —"}
                              {" · "}
                              <span className="font-semibold text-eger-gold">{row.total}/100</span>
                            </span>
                            {row.ects && <span className="text-xs text-stone-400">ECTS {row.ects}</span>}
                            {statusBadge(row)}
                            <button
                              type="button"
                              className="btn-ghost ml-auto px-3 py-1"
                              onClick={() => setOpenId(open ? "" : row.studentId)}
                            >
                              {open ? "Згорнути" : "Журнал"}
                            </button>
                          </div>
                          {open && (
                            <div className="mt-4 space-y-4">
                              <JournalTable rows={row.journal} />
                              <form
                                className="grid gap-3 md:grid-cols-[140px_1fr_120px_auto] md:items-end"
                                onSubmit={(event) => {
                                  event.preventDefault();
                                  saveRow(row.studentId);
                                }}
                              >
                                <Field label="Дата">
                                  <input
                                    className="field"
                                    type="date"
                                    value={draft.date}
                                    onChange={(event) => patchDraft(row.studentId, { date: event.target.value })}
                                  />
                                </Field>
                                <Field label="Тип">
                                  <select
                                    className="field"
                                    value={draft.gradeType}
                                    onChange={(event) => patchDraft(row.studentId, { gradeType: event.target.value })}
                                  >
                                    <optgroup label="Поточні">
                                      {CURRENT_GRADE_TYPES.map((type) => (
                                        <option key={type}>{type}</option>
                                      ))}
                                    </optgroup>
                                    <optgroup label="Підсумок">
                                      {FINAL_GRADE_TYPES.map((type) => (
                                        <option key={type}>{type}</option>
                                      ))}
                                    </optgroup>
                                  </select>
                                </Field>
                                <Field label="Бали">
                                  <input
                                    className="field"
                                    type="number"
                                    min="0"
                                    max="100"
                                    required
                                    value={draft.gradeValue}
                                    onChange={(event) => patchDraft(row.studentId, { gradeValue: event.target.value })}
                                  />
                                </Field>
                                <button type="submit" className="btn-primary" disabled={busy}>
                                  {draft.id ? "Оновити рядок" : "Додати рядок"}
                                </button>
                              </form>
                              {row.journal.length > 0 && (
                                <div className="flex flex-wrap gap-2">
                                  {row.journal.map((line) => (
                                    <button
                                      key={line.id}
                                      type="button"
                                      className="rounded-full border border-eger-line px-2 py-1 text-xs hover:border-eger-gold"
                                      onClick={() =>
                                        patchDraft(row.studentId, {
                                          id: line.id,
                                          gradeValue: String(line.points),
                                          gradeType: line.gradeType,
                                          date: line.date?.slice(0, 10) || todayInput(),
                                        })
                                      }
                                    >
                                      Змінити: {line.gradeType} {line.points}
                                    </button>
                                  ))}
                                  {draft.id && (
                                    <button
                                      type="button"
                                      className="text-xs text-stone-400 underline"
                                      onClick={() => patchDraft(row.studentId, emptyDraft())}
                                    >
                                      Новий рядок
                                    </button>
                                  )}
                                </div>
                              )}
                            </div>
                          )}
                        </article>
                      );
                    })}
                  </div>
                )}
              </section>
            </>
          )}
        </div>
      )}
    </Layout>
  );
}
