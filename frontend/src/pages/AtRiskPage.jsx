import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, errorText } from "../api/client";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";

export default function AtRiskPage() {
  const { user } = useAuth();
  const { push } = useToast();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const cardBase = user?.role === "Professor" ? "/professor/students" : "/admin/students";

  useEffect(() => {
    api
      .get("/academic/at-risk")
      .then((response) => setData(response.data))
      .catch((error) => push(errorText(error, "Не вдалося завантажити групу ризику"), "error"))
      .finally(() => setLoading(false));
  }, [push]);

  return (
    <Layout title="Група ризику">
      {loading && <p className="text-stone-400">Завантаження...</p>}
      {data && (
        <div className="space-y-6">
          <p className="text-sm text-stone-300">
            Поріг зарахування — <span className="font-semibold text-eger-gold">{data.passThreshold}</span>.
            Борг з’являється після підсумку, якщо разом менше порога. Якщо поточних балів уже не вистачить навіть з максимальним підсумком, студент теж у цьому списку.
          </p>

          <section className="card">
            <h2 className="mb-4 text-lg font-semibold">Студенти</h2>
            {data.students.length === 0 ? (
              <p className="text-sm text-stone-400">Нікого нижче порога</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Студент</th>
                      <th>Група</th>
                      <th>Дисципліна</th>
                      <th>Разом</th>
                      <th>Причина</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.students.map((item) => (
                      <tr key={`${item.studentId}-${item.subjectId}`}>
                        <td>
                          <Link className="font-medium text-eger-gold hover:underline" to={`${cardBase}/${item.studentId}`}>
                            {item.fullName}
                          </Link>
                        </td>
                        <td>{item.group}</td>
                        <td>{item.subjectTitle}</td>
                        <td className="font-semibold text-eger-gold">{item.total}</td>
                        <td>{item.debt ? <span className="badge-debt">борг</span> : <span className="badge-wait">{item.reason}</span>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="card">
            <h2 className="mb-4 text-lg font-semibold">Дисципліни з низькою успішністю</h2>
            <p className="mb-3 text-xs text-stone-500">Частка зарахованих серед тих, у кого вже є підсумок, нижча за 50%.</p>
            {data.subjects.length === 0 ? (
              <p className="text-sm text-stone-400">Таких дисциплін немає</p>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Дисципліна</th>
                      <th>З підсумком</th>
                      <th>Зараховано</th>
                      <th>Частка</th>
                      <th>У групі ризику</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.subjects.map((item) => (
                      <tr key={item.subjectId}>
                        <td className="font-medium">{item.subjectTitle}</td>
                        <td>{item.withFinal}</td>
                        <td>{item.passed}</td>
                        <td className="font-semibold text-eger-gold">{item.passShare}%</td>
                        <td>{item.atRiskCount}</td>
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
