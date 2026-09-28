import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useAnchoredBox } from "./anchoredMenu";

export default function DarkSelect({ value, onChange, options, placeholder = "Оберіть", className = "", disabled = false }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const menuRef = useRef(null);
  const listId = useId();
  const selected = options.find((option) => option.value === value);
  const box = useAnchoredBox(open, rootRef);

  useEffect(() => {
    if (!open) return undefined;
    const onPointer = (event) => {
      const target = event.target;
      if (rootRef.current?.contains(target) || menuRef.current?.contains(target)) return;
      setOpen(false);
    };
    const onKey = (event) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onPointer, true);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer, true);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={rootRef} className={`relative ${className}`}>
      <button
        type="button"
        className="field flex w-full items-center justify-between gap-2 text-left"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        disabled={disabled}
        onClick={() => setOpen((current) => !current)}
      >
        <span className={selected ? "text-stone-100" : "text-stone-500"}>{selected?.label || placeholder}</span>
        <span className="text-xs text-stone-400" aria-hidden="true">▾</span>
      </button>
      {open && box && createPortal(
        <ul id={listId} ref={menuRef} role="listbox" className="dark-menu" style={box}>
          {options.length === 0 ? (
            <li className="px-3 py-2 text-sm text-stone-500">Немає варіантів</li>
          ) : (
            options.map((option) => (
              <li key={option.value}>
                <button
                  type="button"
                  role="option"
                  aria-selected={option.value === value}
                  className={`dark-menu-option ${option.value === value ? "dark-menu-option-active" : ""}`}
                  onClick={() => {
                    onChange(option.value);
                    setOpen(false);
                  }}
                >
                  {option.label}
                </button>
              </li>
            ))
          )}
        </ul>,
        document.body,
      )}
    </div>
  );
}
