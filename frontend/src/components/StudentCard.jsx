import { formatDate } from "../api/client";

export function statusBadge(item) {
  if (item.debt) return <span className="badge-debt">борг</span>;
  if (!item.hasFinal) return <span className="badge-wait">набрано на зараз</span>;
  if (item.finalType === "Залік" || item.outcome === "зараховано") {
    return <span className="badge-ok">зараховано</span>;
  }
  return <span className="badge-ok">{item.nationalLabel || item.outcome}</span>;
}

export function JournalTable({ rows }) {
  if (!rows?.length) {
    return <p className="text-sm text-stone-400">У журналі ще немає рядків</p>;
  }

  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th>Дата</th>
            <th>Тип</th>
            <th>Бали</th>
            <th>Середній / набрано на зараз</th>
            {rows.some((row) => row.professorName) && <th>Викладач</th>}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.id}>
              <td>{formatDate(row.date)}</td>
              <td>{row.gradeType}</td>
              <td className="font-medium text-stone-100">{row.points ?? row.gradeValue}</td>
              <td className="font-semibold text-eger-gold">{row.runningTotal}</td>
              {rows.some((item) => item.professorName) && <td>{row.professorName}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function JournalPanel({ journal, loading }) {
  if (loading) return <p className="mt-4 text-sm text-stone-400">Завантаження журналу...</p>;
  if (!journal) return null;

  return (
    <section className="card mt-4">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs uppercase tracking-wide text-stone-400">Журнал</p>
          <h2 className="text-lg font-semibold">{journal.subjectTitle}</h2>
          <p className="text-sm text-stone-400">
            Викладає: {journal.professorNames?.join(", ") || "—"} · {journal.credits} кред.
          </p>
        </div>
        <div className="text-right">
          <p className="text-xs uppercase tracking-wide text-stone-400">Середній / набрано на зараз</p>
          <p className="text-3xl font-semibold text-eger-gold">
            {journal.total}
            <span className="text-base text-stone-400"> / 100</span>
          </p>
          <p className="text-xs text-stone-400">
            поточні {journal.currentPoints}/{journal.currentMax}
            {journal.hasFinal ? ` · підсумок ${journal.finalPoints}/${journal.finalMax}` : ` · підсумок ще не виставлено`}
          </p>
        </div>
      </div>
      <div className="mb-4 flex flex-wrap items-center gap-2 text-sm">
        {journal.ects && <span className="badge-wait">ECTS {journal.ects}</span>}
        {journal.nationalLabel && <span className="badge-wait">{journal.nationalLabel}</span>}
        {statusBadge(journal)}
      </div>
      <JournalTable rows={journal.rows} />
    </section>
  );
}

export default function StudentCardView({ card, activeSubjectId, onSubject }) {
  const average = card.subjects.length === 0 ? "—" : card.averageTotal;

  return (
    <div className="space-y-4">
      <section className="grid gap-4 md:grid-cols-[1.4fr_1fr]">
        <article className="card">
          <p className="text-sm text-stone-400">
            {card.group} · вступ {card.enrollmentYear}
          </p>
          <h2 className="mt-1 text-2xl font-semibold">{card.fullName}</h2>
          <p className="mt-2 text-sm text-stone-300">Залікова книжка {card.studentCardNumber}</p>
        </article>
        <article className="card">
          <p className="text-xs uppercase tracking-wide text-stone-400">Середній бал</p>
          <p className="mt-2 text-5xl font-semibold text-eger-gold">{average}</p>
          <p className="mt-2 text-sm text-stone-300">Середнє підсумків дисциплін за 100-бальною шкалою</p>
          <p className="text-xs text-stone-500">Поріг зарахування — {card.passThreshold}</p>
        </article>
      </section>

      <section className="card">
        <h2 className="mb-4 text-lg font-semibold">Дисципліни</h2>
        {card.subjects.length === 0 ? (
          <p className="text-sm text-stone-400">Оцінок ще немає</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Дисципліна</th>
                  <th>Кредити</th>
                  <th>Поточні</th>
                  <th>Підсумок</th>
                  <th>Разом</th>
                  <th>ECTS</th>
                  <th>Національна</th>
                  <th>Статус</th>
                </tr>
              </thead>
              <tbody>
                {card.subjects.map((subject) => (
                  <tr key={subject.subjectId} className={activeSubjectId === subject.subjectId ? "bg-white/5" : undefined}>
                    <td>
                      <button
                        type="button"
                        className="text-left font-medium text-eger-gold hover:underline"
                        onClick={() => onSubject(subject)}
                      >
                        {subject.subjectTitle}
                      </button>
                    </td>
                    <td>{subject.credits}</td>
                    <td>
                      {subject.currentPoints}/{subject.currentMax}
                    </td>
                    <td>
                      {subject.hasFinal ? `${subject.finalPoints}/${subject.finalMax}` : "—"}
                      {subject.finalType ? <span className="mt-1 block text-xs text-stone-500">{subject.finalType}</span> : null}
                    </td>
                    <td className="text-lg font-semibold text-eger-gold">
                      {subject.total}
                      <span className="text-xs font-normal text-stone-400"> / 100</span>
                    </td>
                    <td>{subject.ects || "—"}</td>
                    <td>
                      {subject.nationalLabel || "—"}
                      {subject.nationalScore ? <span className="ml-1 text-xs text-stone-500">{subject.nationalScore}</span> : null}
                    </td>
                    <td>{statusBadge(subject)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
