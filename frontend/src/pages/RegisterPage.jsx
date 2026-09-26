import { useEffect, useState } from "react";
import { api, errorText } from "../api/client";
import Gradebook from "../components/Gradebook";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import { useToast } from "../context/ToastContext";

export default function RegisterPage({ subjectsPath, linkBase }) {
  const { push } = useToast();
  const [subjects, setSubjects] = useState([]);
  const [groups, setGroups] = useState([]);
  const [subjectId, setSubjectId] = useState("");
  const [group, setGroup] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([api.get(subjectsPath), api.get("/students/groups")])
      .then(([subjectsRes, groupsRes]) => {
        setSubjects(subjectsRes.data);
        setGroups(groupsRes.data);
        if (subjectsRes.data[0]) setSubjectId(subjectsRes.data[0].id);
        if (groupsRes.data[0]) setGroup(groupsRes.data[0]);
      })
      .catch((error) => push(errorText(error, "Не вдалося завантажити дисципліни"), "error"))
      .finally(() => setLoading(false));
  }, [subjectsPath, push]);

  return (
    <Layout title="Відомість">
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : subjects.length === 0 ? (
        <section className="card">
          <p>Дисциплін ще немає.</p>
        </section>
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap gap-3">
            <Field label="Дисципліна">
              <select className="field min-w-64" value={subjectId} onChange={(event) => setSubjectId(event.target.value)}>
                {subjects.map((subject) => (
                  <option key={subject.id} value={subject.id}>
                    {subject.title}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Група">
              <select className="field min-w-40" value={group} onChange={(event) => setGroup(event.target.value)}>
                {groups.length === 0 && <option value="">Груп ще немає</option>}
                {groups.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </Field>
          </div>
          {subjectId && group && <Gradebook subjectId={subjectId} group={group} linkBase={linkBase} allowColumn />}
        </div>
      )}
    </Layout>
  );
}
