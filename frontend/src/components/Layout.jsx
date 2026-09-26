import { GraduationCap, LogOut } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { roleLabel, useAuth } from "../context/AuthContext";

export default function Layout({ title, children }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const onLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  return (
    <div className="min-h-screen md:grid md:grid-cols-[240px_1fr]">
      <aside className="border-b border-eger-line bg-eger-panel md:border-b-0 md:border-r">
        <div className="flex items-center justify-between gap-3 px-5 py-4 md:block">
          <div className="flex items-center gap-3">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-eger-green text-eger-gold">
              <GraduationCap size={20} />
            </span>
            <div>
              <p className="text-lg font-bold tracking-wide text-eger-gold">EGER</p>
              <p className="text-xs text-stone-400">Оцінювання та звітність</p>
            </div>
          </div>
          <button type="button" className="btn-ghost md:hidden" onClick={onLogout}>
            <LogOut size={16} />
            Вийти
          </button>
        </div>
        <div className="hidden px-5 pb-6 md:block">
          <p className="text-sm font-medium text-stone-100">{user?.fullName}</p>
          <p className="text-xs text-eger-mint">{roleLabel(user?.role)}</p>
          <p className="mt-1 truncate text-xs text-stone-500">{user?.email}</p>
          <button type="button" className="btn-ghost mt-6 w-full" onClick={onLogout}>
            <LogOut size={16} />
            Вийти
          </button>
        </div>
      </aside>
      <main className="px-4 py-6 md:px-8">
        <h1 className="mb-6 text-2xl font-semibold text-stone-50">{title}</h1>
        {children}
      </main>
    </div>
  );
}
