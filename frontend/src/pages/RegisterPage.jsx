import { useEffect, useState } from "react";
import { api, errorText } from "../api/client";
import DarkSelect from "../components/DarkSelect";
import Gradebook from "../components/Gradebook";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import TwoFactorSettings from "../components/TwoFactorSettings";
import { useToast } from "../context/ToastContext";

export default function RegisterPage({ subjectsPath, linkBase, showTwoFactor = false }) {
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
              <DarkSelect
                className="min-w-64"
                value={subjectId}
                onChange={setSubjectId}
                placeholder="Оберіть дисципліну"
                options={subjects.map((subject) => ({ value: subject.id, label: subject.title }))}
              />
            </Field>
            <Field label="Група">
              <DarkSelect
                className="min-w-40"
                value={group}
                onChange={setGroup}
                placeholder={groups.length === 0 ? "Груп ще немає" : "Оберіть групу"}
                disabled={groups.length === 0}
                options={groups.map((item) => ({ value: item, label: item }))}
              />
            </Field>
          </div>
          {subjectId && group && <Gradebook subjectId={subjectId} group={group} linkBase={linkBase} allowColumn />}
          {showTwoFactor && <TwoFactorSettings />}
        </div>
      )}
    </Layout>
  );
}
