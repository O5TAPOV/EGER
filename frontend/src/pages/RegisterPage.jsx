import { useEffect, useState } from "react";
import { api, errorText } from "../api/client";
import DarkSelect from "../components/DarkSelect";
import Gradebook from "../components/Gradebook";
import Layout from "../components/Layout";
import { Field } from "../components/Modal";
import TwoFactorSettings from "../components/TwoFactorSettings";
import { useToast } from "../context/ToastContext";

function groupsOf(subject) {
  return Array.isArray(subject?.groups) ? subject.groups : [];
}

export default function RegisterPage({ subjectsPath, linkBase, showTwoFactor = false }) {
  const { push } = useToast();
  const [subjects, setSubjects] = useState([]);
  const [subjectId, setSubjectId] = useState("");
  const [group, setGroup] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get(subjectsPath)
      .then(({ data }) => {
        setSubjects(data);
        const first = data.find((subject) => groupsOf(subject).length > 0) || data[0];
        if (first) {
          setSubjectId(first.id);
          setGroup(groupsOf(first)[0] || "");
        }
      })
      .catch((error) => push(errorText(error, "Не вдалося завантажити дисципліни"), "error"))
      .finally(() => setLoading(false));
  }, [subjectsPath, push]);

  const selected = subjects.find((subject) => subject.id === subjectId);
  const groupOptions = groupsOf(selected);
  const subjectOptions = group
    ? subjects.filter((subject) => groupsOf(subject).includes(group))
    : subjects;

  const chooseSubject = (nextId) => {
    const next = subjects.find((subject) => subject.id === nextId);
    const allowed = groupsOf(next);
    setSubjectId(nextId);
    setGroup((current) => (allowed.includes(current) ? current : (allowed[0] || "")));
  };

  const chooseGroup = (nextGroup) => {
    setGroup(nextGroup);
    setSubjectId((current) => {
      const currentSubject = subjects.find((subject) => subject.id === current);
      if (groupsOf(currentSubject).includes(nextGroup)) return current;
      return subjects.find((subject) => groupsOf(subject).includes(nextGroup))?.id || "";
    });
  };

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
                onChange={chooseSubject}
                placeholder="Оберіть дисципліну"
                options={subjectOptions.map((subject) => ({ value: subject.id, label: subject.title }))}
              />
            </Field>
            <Field label="Група">
              <DarkSelect
                className="min-w-40"
                value={group}
                onChange={chooseGroup}
                placeholder={groupOptions.length === 0 ? "Груп ще немає" : "Оберіть групу"}
                disabled={groupOptions.length === 0}
                options={groupOptions.map((item) => ({ value: item, label: item }))}
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
