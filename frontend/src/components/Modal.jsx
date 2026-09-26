import { X } from "lucide-react";

export default function Modal({ title, onClose, children }) {
  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-black/70 p-4" onMouseDown={onClose}>
      <div
        className="card max-h-[90vh] w-full max-w-lg overflow-auto"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="mb-4 flex items-start justify-between gap-4">
          <h2 className="text-lg font-semibold text-eger-gold">{title}</h2>
          <button type="button" className="text-stone-400 hover:text-white" onClick={onClose} aria-label="Закрити">
            <X size={18} />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function Field({ label, children }) {
  return (
    <label className="block">
      <span className="label">{label}</span>
      {children}
    </label>
  );
}
