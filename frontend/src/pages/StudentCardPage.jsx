import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, errorText } from "../api/client";
import Gradebook from "../components/Gradebook";
import Layout from "../components/Layout";
import StudentCardView from "../components/StudentCard";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../context/ToastContext";

export default function StudentCardPage() {
  const { studentId } = useParams();
  const { user } = useAuth();
  const { push } = useToast();
  const [card, setCard] = useState(null);
  const [activeSubjectId, setActiveSubjectId] = useState("");
  const [loading, setLoading] = useState(true);
  const home = user?.role === "Professor" ? "/professor" : "/admin";

  useEffect(() => {
    setLoading(true);
    setActiveSubjectId("");
    api
      .get(`/academic/card/${studentId}`)
      .then((response) => setCard(response.data))
      .catch((error) => push(errorText(error, "Не вдалося відкрити картку студента"), "error"))
      .finally(() => setLoading(false));
  }, [studentId, push]);

  return (
    <Layout title="Картка студента">
      <Link to={home} className="mb-4 inline-block text-sm text-eger-gold hover:underline">
        Назад
      </Link>
      {loading && <p className="text-stone-400">Завантаження...</p>}
      {!loading && !card && <p className="text-stone-400">Картку не знайдено</p>}
      {card && (
        <>
          <StudentCardView
            card={card}
            activeSubjectId={activeSubjectId}
            onSubject={(subject) => setActiveSubjectId((current) => (current === subject.subjectId ? "" : subject.subjectId))}
          />
          {activeSubjectId && (
            <div className="mt-4">
              <Gradebook subjectId={activeSubjectId} group={card.group} studentId={card.studentId} />
            </div>
          )}
        </>
      )}
    </Layout>
  );
}
