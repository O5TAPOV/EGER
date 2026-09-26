import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import Protected from "./components/Protected";
import { AuthProvider, pathForRole, useAuth } from "./context/AuthContext";
import { ToastProvider } from "./context/ToastContext";
import AdminDashboard from "./pages/AdminDashboard";
import AtRiskPage from "./pages/AtRiskPage";
import LoginPage from "./pages/LoginPage";
import ProfessorDashboard from "./pages/ProfessorDashboard";
import RegisterPage from "./pages/RegisterPage";
import StudentCardPage from "./pages/StudentCardPage";
import StudentDashboard from "./pages/StudentDashboard";

function HomeRedirect() {
  const { user, ready } = useAuth();
  if (!ready) {
    return <div className="grid min-h-screen place-items-center text-stone-400">Завантаження...</div>;
  }
  if (!user) return <Navigate to="/login" replace />;
  return <Navigate to={pathForRole(user.role)} replace />;
}

export default function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route
              path="/admin"
              element={
                <Protected role="Admin">
                  <AdminDashboard />
                </Protected>
              }
            />
            <Route
              path="/admin/students/:studentId"
              element={
                <Protected role="Admin">
                  <StudentCardPage />
                </Protected>
              }
            />
            <Route
              path="/admin/register"
              element={
                <Protected role="Admin">
                  <RegisterPage subjectsPath="/subjects" linkBase="/admin/students" />
                </Protected>
              }
            />
            <Route
              path="/admin/at-risk"
              element={
                <Protected role="Admin">
                  <AtRiskPage />
                </Protected>
              }
            />
            <Route
              path="/professor"
              element={
                <Protected role="Professor">
                  <ProfessorDashboard />
                </Protected>
              }
            />
            <Route
              path="/professor/students/:studentId"
              element={
                <Protected role="Professor">
                  <StudentCardPage />
                </Protected>
              }
            />
            <Route
              path="/professor/at-risk"
              element={
                <Protected role="Professor">
                  <AtRiskPage />
                </Protected>
              }
            />
            <Route
              path="/student"
              element={
                <Protected role="Student">
                  <StudentDashboard />
                </Protected>
              }
            />
            <Route path="*" element={<HomeRedirect />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ToastProvider>
  );
}
