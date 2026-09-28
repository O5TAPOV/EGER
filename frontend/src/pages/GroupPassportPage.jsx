import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api, errorText } from "../api/client";
import DarkSelect from "../components/DarkSelect";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import { useToast } from "../context/ToastContext";

export default function GroupPassportPage({ studentBase }) {
  const { push } = useToast();
  const [params, setParams] = useSearchParams();
  const [groups, setGroups] = useState([]);
  const [passport, setPassport] = useState(null);
  const [loading, setLoading] = useState(true);
  const selected = params.get("code") || "";

  useEffect(() => {
    api.get("/academic/teaching-groups")
      .then(({ data }) => {
        setGroups(data);
        if (!selected && data[0]) {
          setParams({ code: data[0].code }, { replace: true });
        }
      })
      .catch((error) => push(errorText(error, "Не вдалося завантажити групи"), "error"));
  }, [push, selected, setParams]);

  useEffect(() => {
    if (!selected) {
      setPassport(null);
      setLoading(false);
      return undefined;
    }
    let cancelled = false;
    setLoading(true);
    api.get("/academic/group-passport", { params: { group: selected } })
      .then(({ data }) => {
        if (!cancelled) setPassport(data);
      })
      .catch((error) => {
        if (!cancelled) {
          setPassport(null);
          push(errorText(error, "Не вдалося відкрити паспорт групи"), "error");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [selected, push]);

  return (
    <Layout title="Паспорт групи">
      <div className="mb-4 max-w-sm">
        <Field label="Група">
          <DarkSelect
            value={selected}
            onChange={(code) => setParams({ code })}
            placeholder={groups.length === 0 ? "Груп ще немає" : "Оберіть групу"}
            disabled={groups.length === 0}
            options={groups.map((item) => ({ value: item.code, label: item.code }))}
          />
        </Field>
      </div>
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : !passport ? (
        <section className="card">
          <p>Оберіть групу, щоб побачити її паспорт.</p>
        </section>
      ) : (
        <div className="space-y-4">
          <section className="card">
            <p className="text-sm text-stone-400">Група</p>
            <h2 className="mt-1 text-2xl font-semibold">{passport.code}</h2>
            <p className="mt-2 text-stone-200">{passport.specialty}</p>
          </section>

          <section className="card">
            <h2 className="mb-3 text-lg font-semibold">Студенти</h2>
            {passport.students.length === 0 ? (
              <p className="text-sm text-stone-400">У групі ще немає студентів</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>ПІБ</th>
                      <th>Залікова книжка</th>
                      <th>Борг</th>
                    </tr>
                  </thead>
                  <tbody>
                    {passport.students.map((student) => (
                      <tr key={student.studentId}>
                        <td>
                          <Link className="font-medium text-eger-gold hover:underline" to={`${studentBase}/${student.studentId}`}>
                            {student.fullName}
                          </Link>
                        </td>
                        <td>{student.studentCardNumber}</td>
                        <td>{student.inDebt ? <span className="badge-debt">борг</span> : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="card">
            <h2 className="mb-3 text-lg font-semibold">Дисципліни групи</h2>
            {passport.subjects.length === 0 ? (
              <p className="text-sm text-stone-400">Для цієї групи дисциплін ще не призначено</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Дисципліна</th>
                      <th>Кредити</th>
                      <th>Контроль</th>
                      <th>Викладачі</th>
                    </tr>
                  </thead>
                  <tbody>
                    {passport.subjects.map((subject) => (
                      <tr key={subject.subjectId}>
                        <td>{subject.title}</td>
                        <td>{subject.credits}</td>
                        <td>{subject.controlForm}</td>
                        <td>{subject.professorNames.join(", ") || "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="card">
            <h2 className="mb-1 text-lg font-semibold">Борг</h2>
            <p className="mb-3 text-sm text-stone-400">Поріг зарахування — {passport.passThreshold}</p>
            {passport.debtors.length === 0 ? (
              <p className="text-sm text-stone-400">Боржників немає</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Студент</th>
                      <th>Дисципліна</th>
                      <th>Разом</th>
                    </tr>
                  </thead>
                  <tbody>
                    {passport.debtors.map((item) => (
                      <tr key={`${item.studentId}-${item.subjectTitle}`}>
                        <td>
                          <Link className="font-medium text-eger-gold hover:underline" to={`${studentBase}/${item.studentId}`}>
                            {item.fullName}
                          </Link>
                        </td>
                        <td>{item.subjectTitle}</td>
                        <td>{item.total}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}
    </Layout>
  );
}
