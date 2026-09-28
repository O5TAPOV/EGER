import { useEffect, useId, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useAnchoredBox } from "./anchoredMenu";

function sameText(left, right) {
  return left.localeCompare(right, "uk", { sensitivity: "accent" }) === 0;
}

export default function SearchMultiSelect({
  options,
  selected,
  onChange,
  placeholder,
  emptyText,
  noMatchText = "Нічого не знайдено",
  allowCreate = false,
  createLabel,
  inputLabel,
}) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const inputRef = useRef(null);
  const menuRef = useRef(null);
  const listId = useId();
  const box = useAnchoredBox(open, inputRef);
  const chosen = selected || [];

  const filtered = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase("uk");
    const rows = needle
      ? options.filter((option) => option.label.toLocaleLowerCase("uk").includes(needle))
      : options;
    return [...rows].sort((a, b) => a.label.localeCompare(b.label, "uk"));
  }, [options, query]);

  const draft = query.trim();
  const canCreate = allowCreate
    && draft.length > 0
    && filtered.length === 0
    && !chosen.some((value) => sameText(value, draft))
    && !options.some((option) => sameText(option.label, draft) || sameText(option.value, draft));

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

  const toggle = (value) => {
    if (chosen.includes(value)) onChange(chosen.filter((item) => item !== value));
    else onChange([...chosen, value]);
  };

  const addDraft = () => {
    if (!canCreate) return;
    onChange([...chosen, draft]);
    setQuery("");
  };

  const chips = chosen.map((value) => ({
    value,
    label: options.find((option) => option.value === value)?.label || value,
  }));

  const menu = open && box && (
    <div
      ref={menuRef}
      id={listId}
      role="listbox"
      aria-multiselectable="true"
      className="dark-menu"
      style={box}
      onMouseDown={(event) => event.preventDefault()}
    >
      {filtered.length === 0 ? (
        canCreate ? (
          <button type="button" className="dark-menu-option" onClick={addDraft}>
            {createLabel ? createLabel(draft) : `Додати «${draft}»`}
          </button>
        ) : (
          <p className="px-3 py-2 text-sm text-stone-400">{draft ? noMatchText : emptyText}</p>
        )
      ) : (
        filtered.map((option) => {
          const active = chosen.includes(option.value);
          return (
            <button
              key={option.value}
              type="button"
              role="option"
              aria-selected={active}
              className={`dark-menu-option ${active ? "dark-menu-option-active" : ""}`}
              onClick={() => toggle(option.value)}
            >
              {option.label}
            </button>
          );
        })
      )}
    </div>
  );

  return (
    <div ref={rootRef} className="search-picker">
      {chips.length > 0 && (
        <ul className="picker-chips">
          {chips.map((chip) => (
            <li key={chip.value}>
              <span className="picker-chip">
                <span>{chip.label}</span>
                <button
                  type="button"
                  aria-label={`Прибрати ${chip.label}`}
                  onClick={() => toggle(chip.value)}
                >
                  ×
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}
      <input
        ref={inputRef}
        className="field"
        value={query}
        placeholder={placeholder}
        aria-label={inputLabel || placeholder}
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        role="combobox"
        autoComplete="off"
        onFocus={() => setOpen(true)}
        onBlur={(event) => {
          if (menuRef.current?.contains(event.relatedTarget)) return;
          window.setTimeout(() => {
            const active = document.activeElement;
            if (rootRef.current?.contains(active) || menuRef.current?.contains(active)) return;
            setOpen(false);
          }, 0);
        }}
        onChange={(event) => {
          setQuery(event.target.value);
          setOpen(true);
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            if (canCreate) addDraft();
          }
        }}
      />
      {menu && createPortal(menu, document.body)}
    </div>
  );
}
