import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
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

export default function RegisterPage({ subjectsPath, linkBase, showTwoFactor = false, groupPassportPath = "" }) {
  const { push } = useToast();
  const [subjects, setSubjects] = useState([]);
  const [subjectId, setSubjectId] = useState("");
  const [group, setGroup] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get(subjectsPath)
      .then(({ data }) => {
        setSubjects(data);
        const codes = [...new Set(data.flatMap(groupsOf))].sort((a, b) => a.localeCompare(b, "uk"));
        const firstGroup = codes[0] || "";
        const firstSubject = data.find((subject) => groupsOf(subject).includes(firstGroup));
        setGroup(firstGroup);
        setSubjectId(firstSubject?.id || "");
      })
      .catch((error) => push(errorText(error, "Не вдалося завантажити дисципліни"), "error"))
      .finally(() => setLoading(false));
  }, [subjectsPath, push]);

  const groupOptions = [...new Set(subjects.flatMap(groupsOf))].sort((a, b) => a.localeCompare(b, "uk"));
  const subjectOptions = group
    ? subjects.filter((subject) => groupsOf(subject).includes(group))
    : [];

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
          <div className="flex flex-wrap items-end gap-3">
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
            <Field label="Дисципліна">
              <DarkSelect
                className="min-w-64"
                value={subjectId}
                onChange={setSubjectId}
                placeholder={subjectOptions.length === 0 ? "У групі немає дисциплін" : "Оберіть дисципліну"}
                disabled={subjectOptions.length === 0}
                options={subjectOptions.map((subject) => ({ value: subject.id, label: subject.title }))}
              />
            </Field>
            {groupPassportPath && group && (
              <Link className="pb-2 text-sm text-eger-gold hover:underline" to={`${groupPassportPath}?code=${encodeURIComponent(group)}`}>
                Паспорт групи
              </Link>
            )}
          </div>
          {subjectId && group && <Gradebook subjectId={subjectId} group={group} linkBase={linkBase} allowColumn />}
          {showTwoFactor && <TwoFactorSettings />}
        </div>
      )}
    </Layout>
  );
}
