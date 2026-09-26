import { GraduationCap, LogOut } from "lucide-react";
import { NavLink, useNavigate } from "react-router-dom";
import { roleLabel, useAuth } from "../context/AuthContext";

const NAV = {
  Admin: [
    { to: "/admin", label: "Кабінет", end: true },
    { to: "/admin/register", label: "Відомість" },
    { to: "/admin/at-risk", label: "Група ризику" },
  ],
  Professor: [
    { to: "/professor", label: "Відомість", end: true },
    { to: "/professor/at-risk", label: "Група ризику" },
  ],
  Student: [{ to: "/student", label: "Картка", end: true }],
};

export default function Layout({ title, children }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const links = NAV[user?.role] || [];

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
        <nav className="flex gap-2 overflow-x-auto px-4 pb-4 md:block md:space-y-1 md:px-3">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.end}
              className={({ isActive }) => (isActive ? "nav-link-active whitespace-nowrap" : "nav-link whitespace-nowrap")}
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
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
