import { useEffect, useState } from "react";
import { api, errorText } from "../api/client";
import Gradebook from "../components/Gradebook";
import Layout from "../components/Layout";
import StudentCardView from "../components/StudentCard";
import TwoFactorSettings from "../components/TwoFactorSettings";
import { useToast } from "../context/ToastContext";

export default function StudentDashboard() {
  const { push } = useToast();
  const [card, setCard] = useState(null);
  const [activeSubjectId, setActiveSubjectId] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get("/academic/card/me")
      .then((response) => setCard(response.data))
      .catch((error) => push(errorText(error, "Не вдалося завантажити картку"), "error"))
      .finally(() => setLoading(false));
  }, [push]);

  const openSubject = (subject) => {
    setActiveSubjectId((current) => (current === subject.subjectId ? "" : subject.subjectId));
  };

  return (
    <Layout title="Картка студента">
      {loading ? (
        <p className="text-stone-400">Завантаження...</p>
      ) : !card ? (
        <p className="text-stone-400">Не вдалося завантажити картку</p>
      ) : (
        <div className="space-y-6">
          <StudentCardView card={card} activeSubjectId={activeSubjectId} onSubject={openSubject} />
          {activeSubjectId && (
            <Gradebook subjectId={activeSubjectId} group={card.group} studentId={card.studentId} />
          )}

          <TwoFactorSettings />
        </div>
      )}
    </Layout>
  );
}
