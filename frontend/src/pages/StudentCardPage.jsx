import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, errorText } from "../api/client";
import Layout from "../components/Layout";
import StudentCardView, { JournalPanel } from "../components/StudentCard";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";

export default function StudentCardPage() {
  const { studentId } = useParams();
  const { user } = useAuth();
  const { push } = useToast();
  const [card, setCard] = useState(null);
  const [journal, setJournal] = useState(null);
  const [activeSubjectId, setActiveSubjectId] = useState("");
  const [loading, setLoading] = useState(true);
  const [journalLoading, setJournalLoading] = useState(false);

  const home = user?.role === "Professor" ? "/professor" : "/admin";

  useEffect(() => {
    setLoading(true);
    setJournal(null);
    setActiveSubjectId("");
    api
      .get(`/academic/card/${studentId}`)
      .then((response) => setCard(response.data))
      .catch((error) => push(errorText(error, "Не вдалося відкрити картку студента"), "error"))
      .finally(() => setLoading(false));
  }, [studentId, push]);

  const openSubject = async (subject) => {
    if (activeSubjectId === subject.subjectId) {
      setActiveSubjectId("");
      setJournal(null);
      return;
    }
    setActiveSubjectId(subject.subjectId);
    setJournalLoading(true);
    try {
      const { data } = await api.get("/academic/journal", {
        params: { studentId, subjectId: subject.subjectId },
      });
      setJournal(data);
    } catch (error) {
      setJournal(null);
      push(errorText(error, "Не вдалося відкрити журнал"), "error");
    } finally {
      setJournalLoading(false);
    }
  };

  return (
    <Layout title="Картка студента">
      <Link to={home} className="mb-4 inline-block text-sm text-eger-gold hover:underline">
        Назад
      </Link>
      {loading && <p className="text-stone-400">Завантаження...</p>}
      {!loading && !card && <p className="text-stone-400">Картку не знайдено</p>}
      {card && (
        <>
          <StudentCardView card={card} activeSubjectId={activeSubjectId} onSubject={openSubject} />
          <JournalPanel journal={activeSubjectId ? journal : null} loading={journalLoading} />
        </>
      )}
    </Layout>
  );
}
