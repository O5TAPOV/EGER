from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path

from ration_bot.meals import Dish, day_menu, shopping, stem
from ration_bot.train import DEFAULT_DAYS, advance


@dataclass
class State:
    owner_id: int | None = None
    chat_id: int | None = None
    hates: list[str] = field(default_factory=list)
    level: int = 0
    done_at_level: int = 0
    trained: list[str] = field(default_factory=list)
    train_days: list[int] = field(default_factory=lambda: list(DEFAULT_DAYS))
    weights: list[dict] = field(default_factory=list)
    kg: float = 111.6
    owned: list[str] = field(default_factory=list)
    menu_date: str = ""
    shifts: dict = field(default_factory=dict)

    def to_json(self) -> dict:
        return {
            "owner_id": self.owner_id,
            "chat_id": self.chat_id,
            "hates": self.hates,
            "level": self.level,
            "done_at_level": self.done_at_level,
            "trained": self.trained,
            "train_days": self.train_days,
            "weights": self.weights,
            "kg": self.kg,
            "owned": self.owned,
            "menu_date": self.menu_date,
            "shifts": self.shifts,
        }

    @classmethod
    def from_json(cls, raw: dict) -> "State":
        return cls(
            owner_id=raw.get("owner_id"),
            chat_id=raw.get("chat_id"),
            hates=list(raw.get("hates") or []),
            level=int(raw.get("level") or 0),
            done_at_level=int(raw.get("done_at_level") or 0),
            trained=list(raw.get("trained") or []),
            train_days=list(raw.get("train_days") or list(DEFAULT_DAYS)),
            weights=list(raw.get("weights") or []),
            kg=float(raw.get("kg") or 111.6),
            owned=list(raw.get("owned") or []),
            menu_date=raw.get("menu_date") or "",
            shifts=dict(raw.get("shifts") or {}),
        )


def load_state(path: Path) -> State:
    return State.from_json(json.loads(path.read_text(encoding="utf-8")))


def save_state(path: Path, state: State) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(state.to_json(), ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def add_hate(state: State, words: list[str]) -> None:
    existing = {stem(item) for item in state.hates}
    for word in words:
        cleaned = " ".join(word.split())
        if cleaned and stem(cleaned) not in existing:
            state.hates.append(cleaned)
            existing.add(stem(cleaned))
    state.menu_date = ""


def remove_hate(state: State, word: str) -> bool:
    target = stem(word)
    kept = [item for item in state.hates if stem(item) != target]
    changed = len(kept) != len(state.hates)
    state.hates = kept
    if changed:
        state.menu_date = ""
    return changed


def menu_for(state: State, day: date) -> dict[str, Dish]:
    shifts = state.shifts if state.menu_date == day.isoformat() else {}
    if state.menu_date != day.isoformat():
        state.shifts = {}
        state.menu_date = day.isoformat()
        shifts = {}
    return day_menu(day, state.hates, shifts=shifts)


def reroll(state: State, day: date, slot: str) -> dict[str, Dish]:
    if state.menu_date != day.isoformat():
        state.shifts = {}
        state.menu_date = day.isoformat()
    state.shifts[slot] = int(state.shifts.get(slot, 0)) + 1
    return day_menu(day, state.hates, shifts=state.shifts)


def shop_lines(state: State, day: date) -> list[str]:
    return shopping(menu_for(state, day))


def mark_trained(state: State, day: date) -> str:
    key = day.isoformat()
    if key in state.trained:
        return "already"
    state.trained.append(key)
    state.trained = state.trained[-40:]
    state.done_at_level += 1
    state.level, state.done_at_level, moved = advance(state.level, state.done_at_level)
    if moved:
        return "up"
    return "ok"


def mark_owned(state: State, key: str) -> None:
    if key not in state.owned:
        state.owned.append(key)


def add_weight(state: State, day: date, kg: float) -> dict:
    previous = state.weights[-1]["kg"] if state.weights else None
    row = {"date": day.isoformat(), "kg": round(kg, 1)}
    state.kg = row["kg"]
    state.weights.append(row)
    state.weights = state.weights[-60:]
    delta = None if previous is None else round(row["kg"] - previous, 1)
    return {"row": row, "delta": delta}
