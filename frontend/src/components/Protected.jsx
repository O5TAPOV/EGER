import { Navigate } from "react-router-dom";
import { pathForRole, useAuth } from "../context/AuthContext";

export default function Protected({ role, children }) {
  const { user, ready } = useAuth();
  if (!ready) {
    return <div className="grid min-h-screen place-items-center text-stone-400">Завантаження...</div>;
  }
  if (!user) {
    return <Navigate to="/login" replace />;
  }
  if (role && user.role !== role) {
    return <Navigate to={pathForRole(user.role)} replace />;
  }
  return children;
}
