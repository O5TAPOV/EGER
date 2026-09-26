export function statusBadge(item) {
  if (item.hasMarks === false) return <span className="badge-wait">ще немає оцінок</span>;
  if (item.withinLimits === false) return <span className="badge-debt">перевищення</span>;
  if (item.debt) return <span className="badge-debt">борг</span>;
  if (!item.hasFinal) return <span className="badge-wait">набрано на зараз</span>;
  if (item.finalType === "Залік" || item.outcome === "зараховано") {
    return <span className="badge-ok">зараховано</span>;
  }
  return <span className="badge-ok">{item.nationalLabel || item.outcome}</span>;
}

export default function StudentCardView({ card, activeSubjectId, onSubject }) {
  const hasScoredSubject = card.subjects.some((item) => item.hasMarks !== false);
  const average = !hasScoredSubject || card.subjects.length === 0 || (card.hasInvalidSubjects && card.subjects.every((item) => item.withinLimits === false))
    ? "—"
    : card.averageTotal;

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
          {card.hasInvalidSubjects && (
            <p className="mt-2 text-xs text-red-200">Дисципліни з перевищенням ліміту не входять у середній бал.</p>
          )}
        </article>
      </section>

      <section className="card">
        <h2 className="mb-4 text-lg font-semibold">Дисципліни</h2>
        {card.subjects.length === 0 ? (
          <p className="text-sm text-stone-400">У групі ще немає дисциплін</p>
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
                      {subject.warnings?.length > 0 && (
                        <div className="mt-2 max-w-md space-y-1 text-xs text-red-200">
                          {subject.warnings.map((warning) => (
                            <p key={warning}>{warning}</p>
                          ))}
                        </div>
                      )}
                    </td>
                    <td>{subject.credits}</td>
                    <td>
                      {subject.hasMarks === false ? "—" : `${subject.currentPoints}/${subject.currentMax}`}
                      {subject.absenceCount > 0 && (
                        <span className="mt-1 block text-xs text-eger-gold">Пропущено: {subject.absenceCount}</span>
                      )}
                    </td>
                    <td>
                      {subject.hasMarks === false || !subject.hasFinal ? "—" : `${subject.finalPoints}/${subject.finalMax}`}
                      {subject.finalType ? <span className="mt-1 block text-xs text-stone-500">{subject.finalType}</span> : null}
                    </td>
                    <td className={`text-lg font-semibold ${subject.withinLimits === false && subject.hasMarks !== false ? "text-red-300" : "text-eger-gold"}`}>
                      {subject.hasMarks === false ? "—" : subject.total}
                      {subject.hasMarks !== false && <span className="text-xs font-normal text-stone-400"> / 100</span>}
                    </td>
                    <td>{subject.hasMarks === false ? "—" : subject.ects || "—"}</td>
                    <td>
                      {subject.withinLimits === false ? "—" : subject.nationalLabel || "—"}
                      {subject.withinLimits !== false && subject.nationalScore ? (
                        <span className="ml-1 text-xs text-stone-500">{subject.nationalScore}</span>
                      ) : null}
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
