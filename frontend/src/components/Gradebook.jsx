import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { api, errorText, todayInput } from "../api/client";
import DarkSelect from "./DarkSelect";
import { Field } from "./Modal";
import { useToast } from "../context/ToastContext";

const COLUMN_KINDS = [
  { value: "Лекція", label: "Лекція" },
  { value: "Практична", label: "Практична" },
  { value: "Лабораторна", label: "Лабораторна" },
  { value: "Контроль", label: "Контроль" },
];

function parsedMark(mark) {
  if (mark == null || String(mark).trim() === "") return { empty: true, absent: false, points: null };
  const text = String(mark).trim();
  if (text.toLowerCase() === "н") return { empty: false, absent: true, points: null };
  if (!/^\d+$/.test(text)) return { invalid: true };
  return { empty: false, absent: false, points: Number(text) };
}

function columnMessage(fullName, points, maxPoints) {
  return `Увага. Бал студента ${fullName} становить ${points} і перевищує максимум колонки ${maxPoints}.`;
}

function limitMessages({ fullName, row, lectures, works, controls, sessionId, finalColumn, mark, currentMax, finalMax }) {
  const messages = [];
  let current = 0;
  const groups = [
    [lectures, row.lectures],
    [works, row.works],
    [controls, row.controls],
  ];
  for (const [columns, cells] of groups) {
    columns.forEach((column, index) => {
      const cell = cells?.[index];
      const editing = !finalColumn && column.id === sessionId;
      const value = editing ? parsedMark(mark) : cell?.absent ? { points: null, absent: true } : { points: cell?.points ?? null };
      if (value.invalid) return;
      if (editing && value.points != null && value.points > column.maxPoints) {
        messages.push(columnMessage(fullName, value.points, column.maxPoints));
      }
      if (value.points != null) current += value.points;
    });
  }

  let finalPoints = null;
  if (finalColumn) {
    const parsed = parsedMark(mark);
    if (!parsed.invalid && !parsed.absent) finalPoints = parsed.points;
  } else if (row.hasFinal) {
    finalPoints = row.finalPoints;
  }
  const total = current + (finalPoints ?? 0);
  if (current > currentMax) {
    messages.push(`Увага. Поточні бали студента ${fullName} становлять ${current} і перевищують максимум ${currentMax}. Перевірте поточні роботи.`);
  }
  if (finalPoints != null && finalPoints > finalMax) {
    messages.push(`Увага. Підсумок студента ${fullName} становить ${finalPoints} і перевищує максимум ${finalMax}. Перевірте підсумковий контроль.`);
  }
  if (total > 100) {
    messages.push(`Увага. Сума балів студента ${fullName} становить ${total} і перевищує 100. Перевірте поточні роботи та підсумковий контроль.`);
  }
  return messages;
}

function SheetCell({ display, allowsAbsence, readOnly, onCommit }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");
  const inputRef = useRef(null);
  const skipBlur = useRef(false);

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  if (readOnly) return <span className="sheet-cell-value">{display || ""}</span>;

  const close = () => setEditing(false);
  const commit = () => {
    const text = draft.trim();
    close();
    if (text === "") {
      if (display) onCommit("");
      return;
    }
    if (allowsAbsence && text.toLowerCase() === "н") {
      if (display !== "н") onCommit("н");
      return;
    }
    if (!/^\d+$/.test(text)) {
      onCommit(undefined);
      return;
    }
    if (text !== String(display ?? "")) onCommit(String(Number(text)));
  };

  if (!editing) {
    return (
      <button
        type="button"
        className="sheet-cell-button"
        onClick={() => {
          setDraft(display || "");
          setEditing(true);
        }}
      >
        {display || ""}
      </button>
    );
  }

  return (
    <input
      ref={inputRef}
      className="sheet-cell-input"
      value={draft}
      aria-label={allowsAbsence ? "Бал або відсутність" : "Бал"}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={() => {
        if (skipBlur.current) {
          skipBlur.current = false;
          return;
        }
        commit();
      }}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          skipBlur.current = true;
          commit();
        } else if (event.key === "Escape") {
          event.preventDefault();
          skipBlur.current = true;
          close();
        }
      }}
    />
  );
}

function findCell(cells, column) {
  return cells?.find((cell) => cell.sessionId === column.id) ?? { display: "", points: null, absent: false };
}

export default function Gradebook({ subjectId, group, studentId, linkBase, allowColumn = false }) {
  const { push } = useToast();
  const [register, setRegister] = useState(null);
  const [loading, setLoading] = useState(false);
  const [blocked, setBlocked] = useState("");
  const [kind, setKind] = useState("Лекція");
  const [columnDate, setColumnDate] = useState(todayInput());
  const [columnMax, setColumnMax] = useState("2");
  const [legend, setLegend] = useState("");
  const [legendDraft, setLegendDraft] = useState([]);
  const [busy, setBusy] = useState(false);
  const requestRef = useRef(0);

  const load = async () => {
    if (!subjectId) return;
    const requestId = ++requestRef.current;
    setLoading(true);
    try {
      const { data } = await api.get("/academic/register", {
        params: { subjectId, group: group || undefined, studentId: studentId || undefined },
      });
      if (requestRef.current !== requestId) return;
      setRegister(data);
    } catch (error) {
      if (requestRef.current !== requestId) return;
      setRegister(null);
      push(errorText(error, "Не вдалося відкрити відомість"), "error");
    } finally {
      if (requestRef.current === requestId) setLoading(false);
    }
  };

  useEffect(() => {
    if (!register) return;
    setLegendDraft((register.legend || []).map((item) => ({
      previousCode: item.code,
      code: item.code,
      text: item.text,
    })));
  }, [register]);

  useEffect(() => {
    setBlocked("");
    setRegister(null);
    if (subjectId) load();
    // load reads the latest subject, group, and student.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [subjectId, group, studentId]);

  const saveCell = async (row, { sessionId, finalColumn, mark, allowsAbsence }) => {
    if (!register) return;
    if (mark === undefined) {
      const text = allowsAbsence
        ? "Вкажіть ціле число або «н» для лекції."
        : "Вкажіть ціле невід'ємне число.";
      setBlocked(text);
      push(text, "error");
      return;
    }
    const messages = limitMessages({
      fullName: row.fullName,
      row,
      lectures: register.lectures,
      works: register.works,
      controls: register.controls,
      sessionId,
      finalColumn,
      mark,
      currentMax: register.currentMax,
      finalMax: register.finalMax,
    });
    if (messages.length > 0) {
      const text = messages.join("\n");
      setBlocked(text);
      push(text, "error");
      return;
    }

    setBusy(true);
    try {
      await api.put("/academic/register/cells", {
        sessionId: sessionId || null,
        subjectId: register.subjectId,
        finalColumn: Boolean(finalColumn),
        studentId: row.studentId,
        mark: mark === "" ? null : mark,
      });
      setBlocked("");
      await load();
    } catch (error) {
      const text = errorText(error, "Не вдалося зберегти бал");
      setBlocked(text);
      push(text, "error");
    } finally {
      setBusy(false);
    }
  };

  const setFinalized = async (finalized) => {
    if (!register) return;
    setBusy(true);
    try {
      await api.put("/academic/register/sheet", {
        subjectId: register.subjectId,
        group: register.group,
        finalized,
      });
      await load();
    } catch (error) {
      push(errorText(error, "Не вдалося оновити позначку журналу"), "error");
    } finally {
      setBusy(false);
    }
  };

  const addColumn = async (event) => {
    event.preventDefault();
    if (!register) return;
    setBusy(true);
    try {
      await api.post("/academic/register/columns", {
        subjectId: register.subjectId,
        group: register.group,
        kind,
        maxPoints: Number(columnMax),
        date: kind === "Контроль" || !columnDate ? null : new Date(`${columnDate}T00:00:00Z`).toISOString(),
        legend: kind === "Контроль" ? legend : null,
      });
      setBlocked("");
      setLegend("");
      await load();
    } catch (error) {
      const text = errorText(error, "Не вдалося додати колонку");
      setBlocked(text);
      push(text, "error");
    } finally {
      setBusy(false);
    }
  };

  const changeDate = async (sessionId, value) => {
    if (!value) return;
    setBusy(true);
    try {
      await api.put(`/academic/register/columns/${sessionId}/date`, {
        date: new Date(`${value}T00:00:00Z`).toISOString(),
      });
      await load();
    } catch (error) {
      push(errorText(error, "Не вдалося змінити дату"), "error");
    } finally {
      setBusy(false);
    }
  };

  const deleteColumn = async (column) => {
    const label = column.dateLabel ? `${column.number} (${column.dateLabel})` : (column.code || column.number);
    if (!window.confirm(`Видалити колонку ${label} з відомості? Бали в ній буде знято.`)) return;
    setBusy(true);
    try {
      await api.delete(`/academic/register/columns/${column.id}`);
      await load();
    } catch (error) {
      push(errorText(error, "Не вдалося видалити колонку"), "error");
    } finally {
      setBusy(false);
    }
  };

  const hideStudent = async (row) => {
    if (!register) return;
    if (!window.confirm(`Прибрати ${row.fullName} з цієї відомості? Обліковий запис студента не видаляється.`)) return;
    setBusy(true);
    try {
      await api.delete("/academic/register/rows", {
        params: { subjectId: register.subjectId, group: register.group, studentId: row.studentId },
      });
      await load();
    } catch (error) {
      push(errorText(error, "Не вдалося прибрати рядок"), "error");
    } finally {
      setBusy(false);
    }
  };

  const saveLegend = async () => {
    if (!register) return;
    if (legendDraft.some((item) => !item.code.trim())) {
      const text = "Вкажіть код позначення.";
      setBlocked(text);
      push(text, "error");
      return;
    }
    setBusy(true);
    try {
      await api.put("/academic/register/legend", {
        subjectId: register.subjectId,
        group: register.group,
        entries: legendDraft.map((item) => ({
          previousCode: item.previousCode || null,
          code: item.code.trim(),
          text: item.text.trim(),
        })),
      });
      setBlocked("");
      await load();
      push("Умовні позначення збережено");
    } catch (error) {
      const text = errorText(error, "Не вдалося зберегти позначення");
      setBlocked(text);
      push(text, "error");
    } finally {
      setBusy(false);
    }
  };

  if (!subjectId || (allowColumn && !group)) return null;
  if (loading && !register) return <p className="text-stone-400">Завантаження відомості...</p>;
  if (!register) return null;

  const lectures = register.lectures || [];
  const works = register.works || [];
  const labs = works.filter((column) => column.kind === "Лабораторна");
  const practicals = works.filter((column) => column.kind !== "Лабораторна");
  const controls = register.controls || [];
  const storedWarnings = [...new Set(register.rows.flatMap((row) => row.warnings || []))];
  const editable = register.canEdit && !busy;
  const columnCount = 2 + lectures.length + works.length + controls.length + 5;

  const lessonHead = (column) => (
    <th key={column.id} className="lesson-col" title={column.legend || undefined}>
      <span className="lesson-no">{column.code || column.number}</span>
      {column.dateLabel ? (
        editable ? (
          <button type="button" className="sheet-date-button" onClick={(event) => event.currentTarget.nextElementSibling?.showPicker?.() || event.currentTarget.nextElementSibling?.focus()}>
            {column.dateLabel}
          </button>
        ) : (
          <span className="lesson-date">{column.dateLabel}</span>
        )
      ) : null}
      {editable && column.dateValue ? (
        <input
          className="sheet-date"
          type="date"
          aria-label={`Дата колонки ${column.number}`}
          value={column.dateValue}
          onChange={(event) => changeDate(column.id, event.target.value)}
        />
      ) : null}
      {editable && (
        <button type="button" className="sheet-col-delete" aria-label="Видалити колонку" onClick={() => deleteColumn(column)}>
          ×
        </button>
      )}
    </th>
  );

  return (
    <div className="space-y-3">
      <p className="text-sm text-stone-300">
        Середнє разом: <span className="font-semibold text-eger-gold">{register.classAverage ?? "—"}</span>
        {" · "}
        Борг: <span className="font-semibold text-eger-gold">{register.debtCount}</span>
        {" · "}
        Поріг зарахування {register.passThreshold}
      </p>

      {storedWarnings.length > 0 && (
        <div className="limit-alert" role="alert">
          {storedWarnings.join("\n")}
        </div>
      )}
      {blocked && (
        <div className="limit-alert" role="alert">
          {blocked}
        </div>
      )}

      <div>
        <h2 className="text-lg font-semibold">{register.subjectTitle}</h2>
        <p className="text-sm text-stone-400">
          {register.hours} год · {register.controlForm} · {register.specialty}, {register.degree} · група {register.group} · семестр {register.semester}
        </p>
        <p className="text-sm text-stone-400">
          Поточний контроль: {register.currentProfessor} · Підсумок: {register.finalProfessor}
        </p>
      </div>

      <div className="sheet-scroll">
          <table className="sheet-grid">
            <thead>
              <tr>
                <th className="col-num" rowSpan={2}>№ з/п</th>
                <th className="col-name" rowSpan={2}>Прізвище, ім&apos;я, по батькові студента</th>
                {lectures.length > 0 && <th className="group-col" colSpan={lectures.length}>Лекції</th>}
                {labs.length > 0 && <th className="group-col" colSpan={labs.length}>Лабораторні</th>}
                {practicals.length > 0 && <th className="group-col" colSpan={practicals.length}>Практичні</th>}
                {controls.length > 0 && <th className="group-col" colSpan={controls.length}>Контроль</th>}
                <th className="group-col" colSpan={5}>Підсумок</th>
              </tr>
              <tr>
                {lectures.map(lessonHead)}
                {labs.map(lessonHead)}
                {practicals.map(lessonHead)}
                {controls.map(lessonHead)}
                <th className="sum-col">Результати поточного контролю</th>
                <th className="sum-col">Залік / Екзамен</th>
                <th className="sum-col">Кількість балів</th>
                <th className="sum-col">ECTS</th>
                <th className="sum-col">За розширеною шкалою</th>
              </tr>
              <tr className="max-row">
                <th className="max-label" colSpan={2}>Максимальна кількість балів</th>
                {lectures.map((column) => <th key={column.id} className="lesson-col">{column.maxPoints}</th>)}
                {labs.map((column) => <th key={column.id} className="lesson-col">{column.maxPoints}</th>)}
                {practicals.map((column) => <th key={column.id} className="lesson-col">{column.maxPoints}</th>)}
                {controls.map((column) => <th key={column.id} className="lesson-col">{column.maxPoints}</th>)}
                <th>{register.plannedCurrentMax}</th>
                <th>{register.finalMax}</th>
                <th>100</th>
                <th />
                <th />
              </tr>
            </thead>
            <tbody>
              {register.rows.length === 0 ? (
                <tr>
                  <td className="col-name" colSpan={columnCount}>У групі немає студентів</td>
                </tr>
              ) : (
                register.rows.map((row) => (
                  <tr key={row.studentId}>
                    <td className="col-num">{row.number}</td>
                    <td className="col-name">
                      {linkBase ? (
                        <Link className="sheet-name" to={`${linkBase}/${row.studentId}`}>{row.fullName}</Link>
                      ) : (
                        row.fullName
                      )}
                      {editable && (
                        <button type="button" className="sheet-row-delete" onClick={() => hideStudent(row)}>
                          Прибрати з відомості
                        </button>
                      )}
                    </td>
                    {lectures.map((column) => {
                      const cell = findCell(row.lectures, column);
                      return (
                        <td key={column.id}>
                          <SheetCell
                            display={cell.display}
                            allowsAbsence
                            readOnly={!editable}
                            onCommit={(mark) => saveCell(row, { sessionId: column.id, mark, allowsAbsence: true })}
                          />
                        </td>
                      );
                    })}
                    {labs.map((column) => {
                      const cell = findCell(row.works, column);
                      return (
                        <td key={column.id}>
                          <SheetCell
                            display={cell.display}
                            readOnly={!editable}
                            onCommit={(mark) => saveCell(row, { sessionId: column.id, mark, allowsAbsence: false })}
                          />
                        </td>
                      );
                    })}
                    {practicals.map((column) => {
                      const cell = findCell(row.works, column);
                      return (
                        <td key={column.id}>
                          <SheetCell
                            display={cell.display}
                            readOnly={!editable}
                            onCommit={(mark) => saveCell(row, { sessionId: column.id, mark, allowsAbsence: false })}
                          />
                        </td>
                      );
                    })}
                    {controls.map((column) => {
                      const cell = findCell(row.controls, column);
                      return (
                        <td key={column.id}>
                          <SheetCell
                            display={cell.display}
                            readOnly={!editable}
                            onCommit={(mark) => saveCell(row, { sessionId: column.id, mark, allowsAbsence: false })}
                          />
                        </td>
                      );
                    })}
                    <td>{row.showScores ? row.currentPoints : ""}</td>
                    <td>
                      {row.showScores ? (
                        <SheetCell
                          display={row.hasFinal ? String(row.finalPoints) : ""}
                          readOnly={!editable}
                          onCommit={(mark) => saveCell(row, { finalColumn: true, mark, allowsAbsence: false })}
                        />
                      ) : editable ? (
                        <SheetCell
                          display=""
                          readOnly={false}
                          onCommit={(mark) => saveCell(row, { finalColumn: true, mark, allowsAbsence: false })}
                        />
                      ) : ""}
                    </td>
                    <td className={row.showScores && !row.withinLimits ? "is-over" : row.showScores && row.debt ? "is-debt" : ""}>
                      {row.showScores ? row.total : ""}
                    </td>
                    <td>{row.showScores && row.withinLimits ? row.ects || "" : ""}</td>
                    <td>{row.showScores && row.withinLimits ? row.nationalLabel || "" : ""}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
      </div>

      <section className="sheet-legend">
        <h3>Умовні позначення</h3>
        {editable ? (
          <div className="space-y-2">
            {legendDraft.map((item, index) => (
              <div key={`${item.previousCode}-${index}`} className="flex flex-wrap items-center gap-2">
                <input
                  className="field w-28"
                  aria-label="Код позначення"
                  value={item.code}
                  onChange={(event) => {
                    const next = event.target.value;
                    setLegendDraft((current) => current.map((entry, entryIndex) => (
                      entryIndex === index ? { ...entry, code: next } : entry
                    )));
                  }}
                />
                <input
                  className="field min-w-64 flex-1"
                  aria-label="Пояснення позначення"
                  value={item.text}
                  onChange={(event) => {
                    const next = event.target.value;
                    setLegendDraft((current) => current.map((entry, entryIndex) => (
                      entryIndex === index ? { ...entry, text: next } : entry
                    )));
                  }}
                />
              </div>
            ))}
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className="btn-ghost"
                onClick={() => setLegendDraft((current) => [...current, { previousCode: "", code: "", text: "" }])}
              >
                Додати позначення
              </button>
              <button type="button" className="btn-primary" disabled={busy} onClick={saveLegend}>
                Зберегти позначення
              </button>
            </div>
          </div>
        ) : legendDraft.length === 0 ? (
          <p>Позначень ще немає.</p>
        ) : (
          <ul>
            {legendDraft.map((item) => (
              <li key={`${item.code}-${item.text}`}><b>{item.code}</b> — {item.text}</li>
            ))}
          </ul>
        )}
      </section>

      <label className="sheet-finalized">
        <input
          type="checkbox"
          checked={Boolean(register.finalized)}
          disabled={!register.canEdit || busy}
          onChange={(event) => setFinalized(event.target.checked)}
        />
        Журнал заповнений остаточно
      </label>

      {allowColumn && register.canEdit && (
        <form className="flex flex-wrap items-end gap-3" onSubmit={addColumn}>
          <Field label="Колонка">
            <DarkSelect
              className="w-48"
              value={kind}
              onChange={(next) => {
                setKind(next);
                if (next === "Лекція") setColumnMax("2");
                else if (next === "Контроль") setColumnMax("10");
                else setColumnMax("4");
              }}
              options={COLUMN_KINDS}
            />
          </Field>
          {kind !== "Контроль" && (
            <Field label="Дата">
              <input className="field w-40" type="date" required value={columnDate} onChange={(event) => setColumnDate(event.target.value)} />
            </Field>
          )}
          {kind === "Контроль" && (
            <Field label="Пояснення">
              <input className="field w-64" value={legend} onChange={(event) => setLegend(event.target.value)} placeholder="Наступний вільний код C1–C6" />
            </Field>
          )}
          <Field label="Максимум">
            <input className="field w-24" type="number" min="1" max="100" required value={columnMax} onChange={(event) => setColumnMax(event.target.value)} />
          </Field>
          <button type="submit" className="btn-primary" disabled={busy}>Додати колонку</button>
        </form>
      )}
    </div>
  );
}
