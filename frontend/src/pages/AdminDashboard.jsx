import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { BookOpen, Pencil, Plus, Sprout, Trash2, Users } from "lucide-react";
import { api, errorText } from "../api/client";
import Layout from "../components/Layout";
import Modal, { Field } from "../components/Modal";
import { useToast } from "../context/ToastContext";

const emptyStudent = {
  email: "",
  password: "",
  fullName: "",
  group: "",
  studentCardNumber: "",
  enrollmentYear: new Date().getFullYear(),
};

const emptyProfessor = {
  email: "",
  password: "",
  fullName: "",
  department: "",
  academicDegree: "",
};

const emptySubject = {
  title: "",
  credits: 4,
  professorIds: [],
};

export default function AdminDashboard() {
  const { push } = useToast();
  const [tab, setTab] = useState("overview");
  const [overview, setOverview] = useState(null);
  const [students, setStudents] = useState([]);
  const [professors, setProfessors] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [settings, setSettings] = useState(null);
  const [editor, setEditor] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const [overviewRes, studentsRes, professorsRes, subjectsRes, settingsRes] = await Promise.all([
        api.get("/analytics/overview"),
        api.get("/students"),
        api.get("/professors"),
        api.get("/subjects"),
        api.get("/settings/grading"),
      ]);
      setOverview(overviewRes.data);
      setStudents(studentsRes.data);
      setProfessors(professorsRes.data);
      setSubjects(subjectsRes.data);
      setSettings(settingsRes.data);
    } catch (error) {
      push(errorText(error, "Не вдалося завантажити огляд"), "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const seed = async () => {
    if (!window.confirm("Згенерувати демонстраційні дані? Якщо журнал ще зі старої шкали, оцінки буде перебудовано. Новий набір не дублюється.")) {
      return;
    }
    setBusy(true);
    try {
      const { data } = await api.post("/admin/seed");
      push(data.message || "Дані згенеровано");
      await load();
      setTab("overview");
    } catch (error) {
      push(errorText(error, "Не вдалося згенерувати дані"), "error");
    } finally {
      setBusy(false);
    }
  };

  const save = async (event) => {
    event.preventDefault();
    setBusy(true);
    try {
      if (editor.kind === "student") {
        const payload = { ...editor.form, enrollmentYear: Number(editor.form.enrollmentYear) };
        if (!payload.password) delete payload.password;
        if (editor.id) await api.put(`/students/${editor.id}`, payload);
        else await api.post("/students", payload);
      } else if (editor.kind === "professor") {
        const payload = { ...editor.form };
        if (!payload.password) delete payload.password;
        if (editor.id) await api.put(`/professors/${editor.id}`, payload);
        else await api.post("/professors", payload);
      } else {
        const payload = {
          title: editor.form.title,
          credits: Number(editor.form.credits),
          professorIds: editor.form.professorIds,
        };
        if (editor.id) await api.put(`/subjects/${editor.id}`, payload);
        else await api.post("/subjects", payload);
      }
      push(editor.id ? "Зміни збережено" : "Запис створено");
      setEditor(null);
      await load();
    } catch (error) {
      push(errorText(error, "Не вдалося зберегти"), "error");
    } finally {
      setBusy(false);
    }
  };

  const saveSettings = async (event) => {
    event.preventDefault();
    setBusy(true);
    try {
      const { data } = await api.put("/settings/grading", {
        passThreshold: Number(settings.passThreshold),
        currentMax: Number(settings.currentMax),
        finalMax: Number(settings.finalMax),
      });
      setSettings(data);
      push("Систему оцінювання збережено");
    } catch (error) {
      push(errorText(error, "Не вдалося зберегти систему оцінювання"), "error");
    } finally {
      setBusy(false);
    }
  };

  const remove = async (kind, id, name) => {
    if (!window.confirm(`Видалити «${name}»?`)) return;
    try {
      const path = kind === "student" ? "students" : kind === "professor" ? "professors" : "subjects";
      await api.delete(`/${path}/${id}`);
      push("Запис видалено");
      await load();
    } catch (error) {
      push(errorText(error, "Не вдалося видалити"), "error");
    }
  };

  return (
    <Layout title="Кабінет адміністратора">
      <div className="mb-6 flex flex-wrap gap-2">
        {[
          ["overview", "Огляд"],
          ["students", "Студенти"],
          ["professors", "Викладачі"],
          ["subjects", "Дисципліни"],
          ["grading", "Система оцінювання"],
        ].map(([id, label]) => (
          <button
            key={id}
            type="button"
            className={tab === id ? "btn-primary" : "btn-ghost"}
            onClick={() => setTab(id)}
          >
            {label}
          </button>
        ))}
        <button type="button" className="btn-primary ml-auto" onClick={seed} disabled={busy}>
          <Sprout size={16} />
          Згенерувати тестові дані
        </button>
      </div>

      {loading && <p className="text-stone-400">Завантаження...</p>}

      {!loading && tab === "overview" && overview && (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Stat icon={<Users size={18} />} label="Студенти" value={overview.students} />
          <Stat icon={<Users size={18} />} label="Викладачі" value={overview.professors} />
          <Stat icon={<BookOpen size={18} />} label="Дисципліни" value={overview.subjects} />
          <Stat label="Оцінки" value={overview.grades} />
          <Stat label="Середній бал" value={overview.averageScore} hint="100-бальна шкала, середнє підсумків" />
          <Stat label="Успішність" value={`${overview.passRate}%`} hint={`частка підсумків від ${overview.passThreshold} балів`} />
        </div>
      )}

      {!loading && tab === "grading" && settings && (
        <GradingSettings
          settings={settings}
          busy={busy}
          onChange={(key, value) => setSettings((current) => ({ ...current, [key]: value }))}
          onSave={saveSettings}
        />
      )}

      {!loading && tab === "students" && (
        <Section
          title="Студенти"
          action={() => setEditor({ kind: "student", id: null, form: { ...emptyStudent } })}
        >
          <Rows
            empty="Студентів ще немає"
            headers={["ПІБ", "Група", "Залікова книжка", "Пошта", "Рік", ""]}
            rows={students.map((item) => [
              <Link key={item.id} className="font-medium text-eger-gold hover:underline" to={`/admin/students/${item.id}`}>
                {item.fullName}
              </Link>,
              item.group,
              item.studentCardNumber,
              item.email,
              item.enrollmentYear,
              <RowActions
                key={item.id}
                onEdit={() =>
                  setEditor({
                    kind: "student",
                    id: item.id,
                    form: { ...item, password: "" },
                  })
                }
                onDelete={() => remove("student", item.id, item.fullName)}
              />,
            ])}
          />
        </Section>
      )}

      {!loading && tab === "professors" && (
        <Section
          title="Викладачі"
          action={() => setEditor({ kind: "professor", id: null, form: { ...emptyProfessor } })}
        >
          <Rows
            empty="Викладачів ще немає"
            headers={["ПІБ", "Кафедра", "Ступінь / посада", "Пошта", ""]}
            rows={professors.map((item) => [
              item.fullName,
              item.department,
              item.academicDegree,
              item.email,
              <RowActions
                key={item.id}
                onEdit={() => setEditor({ kind: "professor", id: item.id, form: { ...item, password: "" } })}
                onDelete={() => remove("professor", item.id, item.fullName)}
              />,
            ])}
          />
        </Section>
      )}

      {!loading && tab === "subjects" && (
        <Section
          title="Дисципліни"
          action={() => setEditor({ kind: "subject", id: null, form: { ...emptySubject } })}
        >
          <Rows
            empty="Дисциплін ще немає"
            headers={["Назва", "Кредити", "Викладачі", ""]}
            rows={subjects.map((item) => [
              item.title,
              item.credits,
              item.professorNames.join(", ") || "—",
              <RowActions
                key={item.id}
                onEdit={() =>
                  setEditor({
                    kind: "subject",
                    id: item.id,
                    form: {
                      title: item.title,
                      credits: item.credits,
                      professorIds: [...item.professorIds],
                    },
                  })
                }
                onDelete={() => remove("subject", item.id, item.title)}
              />,
            ])}
          />
        </Section>
      )}

      {editor && (
        <Modal
          title={
            editor.kind === "student"
              ? editor.id ? "Редагувати студента" : "Новий студент"
              : editor.kind === "professor"
                ? editor.id ? "Редагувати викладача" : "Новий викладач"
                : editor.id ? "Редагувати дисципліну" : "Нова дисципліна"
          }
          onClose={() => setEditor(null)}
        >
          <form className="space-y-3" onSubmit={save}>
            {editor.kind === "student" && (
              <>
                <Field label="ПІБ">
                  <input className="field" required value={editor.form.fullName} onChange={(e) => patch(setEditor, "fullName", e.target.value)} />
                </Field>
                <Field label="Електронна пошта">
                  <input className="field" type="email" required value={editor.form.email} onChange={(e) => patch(setEditor, "email", e.target.value)} />
                </Field>
                <Field label={editor.id ? "Новий пароль" : "Пароль"}>
                  <input
                    className="field"
                    type="password"
                    minLength={editor.id ? undefined : 8}
                    required={!editor.id}
                    value={editor.form.password}
                    placeholder={editor.id ? "Залиште порожнім, щоб не змінювати" : "Щонайменше 8 символів"}
                    onChange={(e) => patch(setEditor, "password", e.target.value)}
                  />
                </Field>
                <div className="grid grid-cols-2 gap-3">
                  <Field label="Група">
                    <input className="field" required value={editor.form.group} onChange={(e) => patch(setEditor, "group", e.target.value)} />
                  </Field>
                  <Field label="Рік вступу">
                    <input className="field" type="number" min="1991" max="2100" required value={editor.form.enrollmentYear} onChange={(e) => patch(setEditor, "enrollmentYear", e.target.value)} />
                  </Field>
                </div>
                <Field label="Номер залікової книжки">
                  <input className="field" required value={editor.form.studentCardNumber} onChange={(e) => patch(setEditor, "studentCardNumber", e.target.value)} />
                </Field>
              </>
            )}
            {editor.kind === "professor" && (
              <>
                <Field label="ПІБ">
                  <input className="field" required value={editor.form.fullName} onChange={(e) => patch(setEditor, "fullName", e.target.value)} />
                </Field>
                <Field label="Електронна пошта">
                  <input className="field" type="email" required value={editor.form.email} onChange={(e) => patch(setEditor, "email", e.target.value)} />
                </Field>
                <Field label={editor.id ? "Новий пароль" : "Пароль"}>
                  <input
                    className="field"
                    type="password"
                    required={!editor.id}
                    minLength={editor.id ? undefined : 8}
                    value={editor.form.password}
                    placeholder={editor.id ? "Залиште порожнім, щоб не змінювати" : "Щонайменше 8 символів"}
                    onChange={(e) => patch(setEditor, "password", e.target.value)}
                  />
                </Field>
                <Field label="Кафедра">
                  <input className="field" required value={editor.form.department} onChange={(e) => patch(setEditor, "department", e.target.value)} placeholder="Кафедра комп'ютерних наук" />
                </Field>
                <Field label="Науковий ступінь або посада">
                  <input className="field" required value={editor.form.academicDegree} onChange={(e) => patch(setEditor, "academicDegree", e.target.value)} placeholder="Доцент" />
                </Field>
              </>
            )}
            {editor.kind === "subject" && (
              <>
                <Field label="Назва">
                  <input className="field" required value={editor.form.title} onChange={(e) => patch(setEditor, "title", e.target.value)} placeholder="Теорія баз даних" />
                </Field>
                <Field label="Кредити">
                  <input className="field" type="number" min="1" max="15" required value={editor.form.credits} onChange={(e) => patch(setEditor, "credits", e.target.value)} />
                </Field>
                <fieldset>
                  <legend className="label">Викладачі</legend>
                  <div className="max-h-40 space-y-2 overflow-auto rounded-lg border border-eger-line p-3">
                    {professors.length === 0 && <p className="text-sm text-stone-400">Спочатку додайте викладача</p>}
                    {professors.map((professor) => (
                      <label key={professor.id} className="flex items-center gap-2 text-sm">
                        <input
                          type="checkbox"
                          checked={editor.form.professorIds.includes(professor.id)}
                          onChange={(event) => {
                            const next = new Set(editor.form.professorIds);
                            if (event.target.checked) next.add(professor.id);
                            else next.delete(professor.id);
                            patch(setEditor, "professorIds", [...next]);
                          }}
                        />
                        {professor.fullName}
                      </label>
                    ))}
                  </div>
                </fieldset>
              </>
            )}
            <div className="flex justify-end gap-2 pt-2">
              <button type="button" className="btn-ghost" onClick={() => setEditor(null)}>Скасувати</button>
              <button type="submit" className="btn-primary" disabled={busy}>Зберегти</button>
            </div>
          </form>
        </Modal>
      )}
    </Layout>
  );
}

const ECTS_BANDS = [
  ["90–100", "A", "4", "відмінно"],
  ["80–89", "B", "4", "дуже добре"],
  ["75–79", "C", "3", "добре"],
  ["60–74", "D", "3", "задовільно"],
  ["50–59", "E", "3", "достатньо"],
  ["35–49", "FX", "—", "незадовільно"],
  ["1–34", "F", "—", "неприйнятно"],
];

function GradingSettings({ settings, busy, onChange, onSave }) {
  return (
    <div className="grid gap-4 xl:grid-cols-[420px_1fr]">
      <form className="card space-y-3" onSubmit={onSave}>
        <h2 className="text-lg font-semibold">Пороги</h2>
        <p className="text-sm text-stone-400">
          Разом = поточні + підсумок, максимум 100. Борг — це підсумок нижче мінімального бала зарахування.
        </p>
        <Field label="Мінімальний бал зарахування">
          <input
            className="field"
            type="number"
            min="1"
            max="100"
            required
            value={settings.passThreshold}
            onChange={(event) => onChange("passThreshold", event.target.value)}
          />
        </Field>
        <Field label="Максимум поточних">
          <input
            className="field"
            type="number"
            min="1"
            max="100"
            required
            value={settings.currentMax}
            onChange={(event) => onChange("currentMax", event.target.value)}
          />
        </Field>
        <Field label="Максимум підсумкових">
          <input
            className="field"
            type="number"
            min="1"
            max="100"
            required
            value={settings.finalMax}
            onChange={(event) => onChange("finalMax", event.target.value)}
          />
        </Field>
        <button type="submit" className="btn-primary" disabled={busy}>
          Зберегти
        </button>
      </form>
      <section className="card">
        <h2 className="mb-3 text-lg font-semibold">Шкала ECTS</h2>
        <p className="mb-3 text-sm text-stone-400">
          Для заліку статус — «зараховано», якщо разом не нижче порога, інакше «не зараховано». Для екзамену показується національна оцінка з таблиці.
        </p>
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Бали</th>
                <th>ECTS</th>
                <th>Національна</th>
                <th>Екзамен</th>
              </tr>
            </thead>
            <tbody>
              {ECTS_BANDS.map((band) => (
                <tr key={band[1]}>
                  {band.map((cell, index) => (
                    <td key={`${band[1]}-${index}`}>{cell}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

function patch(setEditor, key, value) {
  setEditor((current) => ({ ...current, form: { ...current.form, [key]: value } }));
}

function Stat({ label, value, hint, icon }) {
  return (
    <article className="card">
      <div className="mb-2 flex items-center gap-2 text-eger-gold">
        {icon}
        <p className="text-xs uppercase tracking-wide text-stone-400">{label}</p>
      </div>
      <p className="text-3xl font-semibold">{value}</p>
      {hint && <p className="mt-1 text-xs text-stone-500">{hint}</p>}
    </article>
  );
}

function Section({ title, action, children }) {
  return (
    <section className="card">
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-lg font-semibold">{title}</h2>
        <button type="button" className="btn-primary" onClick={action}>
          <Plus size={16} />
          Додати
        </button>
      </div>
      {children}
    </section>
  );
}

function Rows({ headers, rows, empty }) {
  if (rows.length === 0) {
    return <p className="text-sm text-stone-400">{empty}</p>;
  }
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            {headers.map((header) => (
              <th key={header || "actions"}>{header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((cells, index) => (
            <tr key={index}>
              {cells.map((cell, cellIndex) => (
                <td key={cellIndex}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RowActions({ onEdit, onDelete }) {
  return (
    <div className="flex justify-end gap-2">
      <button type="button" className="btn-ghost px-2 py-1" onClick={onEdit} aria-label="Редагувати">
        <Pencil size={14} />
      </button>
      <button type="button" className="btn-danger px-2 py-1" onClick={onDelete} aria-label="Видалити">
        <Trash2 size={14} />
      </button>
    </div>
  );
}
