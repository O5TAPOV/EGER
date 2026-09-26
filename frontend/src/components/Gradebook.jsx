import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { api, CURRENT_GRADE_TYPES, errorText, FINAL_GRADE_TYPES, formatDate, todayInput } from "../api/client";
import { Field } from "./Modal";
import { useToast } from "../context/ToastContext";

function limitMessages({ fullName, columns, cells, sessionId, points, currentMax, finalMax }) {
  if (points == null) return [];
  const messages = [];
  const column = columns.find((item) => item.id === sessionId);
  if (column && points > column.maxPoints) {
    messages.push(`Увага. Бал студента ${fullName} становить ${points} і перевищує максимум колонки ${column.maxPoints}.`);
  }

  let current = 0;
  let finalPoints = null;
  for (const col of columns) {
    const value = col.id === sessionId ? points : cells.find((cell) => cell.sessionId === col.id)?.points;
    if (value == null) continue;
    if (col.isFinal) finalPoints = value;
    else current += value;
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

function GradeCell({ points, readOnly, onCommit }) {
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

  if (readOnly) {
    return <span className="register-cell-value">{points ?? ""}</span>;
  }

  const close = () => setEditing(false);

  const commit = () => {
    const text = draft.trim();
    close();
    if (text === "") {
      if (points != null) onCommit(null);
      return;
    }
    if (!/^\d+$/.test(text)) {
      onCommit(undefined);
      return;
    }
    const next = Number(text);
    if (next !== points) onCommit(next);
  };

  if (!editing) {
    return (
      <button type="button" className="register-cell-button" onClick={() => {
        setDraft(points == null ? "" : String(points));
        setEditing(true);
      }}>
        {points ?? ""}
      </button>
    );
  }

  return (
    <input
      ref={inputRef}
      className="register-cell-input"
      inputMode="numeric"
      value={draft}
      aria-label="Бал"
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

export default function Gradebook({ subjectId, group, studentId, linkBase, allowColumn = false }) {
  const { push } = useToast();
  const [register, setRegister] = useState(null);
  const [loading, setLoading] = useState(false);
  const [blocked, setBlocked] = useState("");
  const [columnDate, setColumnDate] = useState(todayInput());
  const [columnType, setColumnType] = useState(CURRENT_GRADE_TYPES[0]);
  const [columnMax, setColumnMax] = useState("10");
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
    setBlocked("");
    setRegister(null);
    if (subjectId) load();
    // load is recreated each render and reads the latest subject, group, and student.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [subjectId, group, studentId]);

  const saveCell = async (row, sessionId, points) => {
    if (!register) return;
    if (points === undefined) {
      const text = "Вкажіть ціле число від 0. Порожня клітинка означає, що бал ще не виставлено.";
      setBlocked(text);
      push(text, "error");
      return;
    }
    const messages = limitMessages({
      fullName: row.fullName,
      columns: register.columns,
      cells: row.cells,
      sessionId,
      points,
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
        sessionId,
        studentId: row.studentId,
        points,
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

  const addColumn = async (event) => {
    event.preventDefault();
    if (!register) return;
    setBusy(true);
    try {
      await api.post("/academic/register/columns", {
        subjectId: register.subjectId,
        group: register.group,
        gradeType: columnType,
        maxPoints: Number(columnMax),
        date: columnDate ? new Date(`${columnDate}T00:00:00Z`).toISOString() : null,
      });
      setBlocked("");
      await load();
    } catch (error) {
      const text = errorText(error, "Не вдалося додати колонку");
      setBlocked(text);
      push(text, "error");
    } finally {
      setBusy(false);
    }
  };

  if (!subjectId || (allowColumn && !group)) return null;
  if (loading && !register) return <p className="text-stone-400">Завантаження відомості...</p>;
  if (!register) return null;

  const storedWarnings = [...new Set(register.rows.flatMap((row) => row.warnings || []))];
  const typeOptions = register.hasFinalColumn ? CURRENT_GRADE_TYPES : [...CURRENT_GRADE_TYPES, ...FINAL_GRADE_TYPES];
  const editable = register.canEdit && !busy;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{register.subjectTitle}</h2>
          <p className="text-sm text-stone-400">
            Група {register.group} · {register.credits} кред. · поріг зарахування {register.passThreshold}
          </p>
        </div>
        <p className="text-sm text-stone-300">
          Середнє разом: <span className="font-semibold text-eger-gold">{register.classAverage ?? "—"}</span>
          {" · "}
          Борг: <span className="font-semibold text-eger-gold">{register.debtCount}</span>
        </p>
      </div>

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

      {allowColumn && register.canEdit && (
        <form className="flex flex-wrap items-end gap-3" onSubmit={addColumn}>
          <Field label="Дата">
            <input className="field w-40" type="date" required value={columnDate} onChange={(event) => setColumnDate(event.target.value)} />
          </Field>
          <Field label="Тип">
            <select
              className="field w-56"
              value={typeOptions.includes(columnType) ? columnType : typeOptions[0]}
              onChange={(event) => {
                const next = event.target.value;
                setColumnType(next);
                if (FINAL_GRADE_TYPES.includes(next)) setColumnMax(String(register.finalMax));
              }}
            >
              <optgroup label="Поточні">
                {CURRENT_GRADE_TYPES.map((type) => (
                  <option key={type}>{type}</option>
                ))}
              </optgroup>
              {!register.hasFinalColumn && (
                <optgroup label="Підсумок">
                  {FINAL_GRADE_TYPES.map((type) => (
                    <option key={type}>{type}</option>
                  ))}
                </optgroup>
              )}
            </select>
          </Field>
          <Field label="Максимум колонки">
            <input
              className="field w-28"
              type="number"
              min="1"
              max="100"
              required
              value={columnMax}
              onChange={(event) => setColumnMax(event.target.value)}
            />
          </Field>
          <button type="submit" className="btn-primary" disabled={busy}>
            Додати колонку
          </button>
          {register.hasFinalColumn && <p className="pb-2 text-xs text-stone-500">Колонку підсумку вже додано.</p>}
        </form>
      )}

      <div className="register-scroll">
        <table className="register-grid">
          <thead>
            <tr>
              <th className="col-num">№</th>
              <th className="col-name">ПІБ</th>
              {register.columns.map((column) => (
                <th key={column.id} className="lesson-col">
                  <span className="block">{formatDate(column.date)}</span>
                  <span className="block">{column.gradeType}</span>
                  <span className="block font-normal text-stone-400">макс. {column.maxPoints}</span>
                </th>
              ))}
              <th className="col-current">
                Поточні
                <span className="block font-normal text-stone-400">/ {register.currentMax}</span>
              </th>
              <th className="col-final">
                Підсумок
                <span className="block font-normal text-stone-400">/ {register.finalMax}</span>
              </th>
              <th className="col-total">
                Разом
                <span className="block font-normal text-stone-400">/ 100</span>
              </th>
              <th className="col-ects">ECTS</th>
              <th className="col-status">Статус</th>
            </tr>
          </thead>
          <tbody>
            {register.rows.length === 0 ? (
              <tr>
                <td className="col-name" colSpan={2}>
                  У групі немає студентів
                </td>
              </tr>
            ) : (
              register.rows.map((row) => (
                <tr key={row.studentId}>
                  <td className="col-num">{row.number}</td>
                  <td className="col-name">
                    {linkBase ? (
                      <Link className="font-medium text-eger-gold hover:underline" to={`${linkBase}/${row.studentId}`}>
                        {row.fullName}
                      </Link>
                    ) : (
                      <span className="font-medium">{row.fullName}</span>
                    )}
                  </td>
                  {register.columns.map((column) => {
                    const cell = row.cells.find((item) => item.sessionId === column.id);
                    return (
                      <td key={column.id}>
                        <GradeCell
                          points={cell?.points ?? null}
                          readOnly={!editable}
                          onCommit={(points) => saveCell(row, column.id, points)}
                        />
                      </td>
                    );
                  })}
                  <td className="col-current">{row.hasMarks ? row.currentPoints : "—"}</td>
                  <td className="col-final">{row.hasFinal ? row.finalPoints : "—"}</td>
                  <td className={`col-total font-semibold ${row.hasMarks && !row.withinLimits ? "text-red-300" : "text-eger-gold"}`}>
                    {row.hasMarks ? row.total : "—"}
                  </td>
                  <td className="col-ects">{row.ects || "—"}</td>
                  <td className="col-status">{row.status || "—"}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
