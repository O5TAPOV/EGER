from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path

from flat_bot.recipes import GEAR, Recipe, by_id, choose_recipe


NAMES = {"anton": "Антон", "nazar": "Назар", "maksym": "Максим"}

PEOPLE = {
    "антон": "anton",
    "anton": "anton",
    "тоха": "anton",
    "назар": "nazar",
    "назарчик": "nazar",
    "nazar": "nazar",
    "максим": "maksym",
    "максимка": "maksym",
    "макс": "maksym",
    "maksym": "maksym",
    "maxim": "maksym",
}

DUTY_NAMES = {
    "cook": "Кухня",
    "cats": "Коти",
    "clean": "Прибирання",
    "shop": "Закупки",
}

DUTY_ALIASES = {
    "cook": "cook",
    "кухня": "cook",
    "готування": "cook",
    "їжа": "cook",
    "cats": "cats",
    "коти": "cats",
    "кіт": "cats",
    "кот": "cats",
    "clean": "clean",
    "прибирання": "clean",
    "уборка": "clean",
    "прибрати": "clean",
    "shop": "shop",
    "закупки": "shop",
    "покупки": "shop",
    "магазин": "shop",
}

GEAR_ALIASES = {
    "air": "airfryer",
    "airfryer": "airfryer",
    "аерогриль": "airfryer",
    "аеро": "airfryer",
    "pan": "pan",
    "сковорідка": "pan",
    "сковорода": "pan",
    "pot": "pot",
    "каструля": "pot",
    "micro": "microwave",
    "microwave": "microwave",
    "мікрохвильовка": "microwave",
    "мікра": "microwave",
    "multi": "multi",
    "мультиварка": "multi",
    "мульти": "multi",
}


def _people() -> dict:
    return {"anton": None, "nazar": None, "maksym": None}


def _owners() -> dict:
    return {"cook": "anton", "cats": "nazar", "clean": "nazar", "shop": "maksym"}


@dataclass
class FlatState:
    chat_id: int | None = None
    people: dict = field(default_factory=_people)
    duty_owner: dict = field(default_factory=_owners)
    done: dict = field(default_factory=dict)
    shopping: list = field(default_factory=list)
    weights: list = field(default_factory=list)
    menu_date: str = ""
    menu_recipe: str = ""
    recent: list = field(default_factory=list)
    has_multi: bool = False

    def to_json(self) -> dict:
        return {
            "chat_id": self.chat_id,
            "people": self.people,
            "duty_owner": self.duty_owner,
            "done": self.done,
            "shopping": self.shopping,
            "weights": self.weights,
            "menu_date": self.menu_date,
            "menu_recipe": self.menu_recipe,
            "recent": self.recent,
            "has_multi": self.has_multi,
        }

    @classmethod
    def from_json(cls, raw: dict) -> "FlatState":
        people = _people()
        people.update(raw.get("people") or {})
        owners = _owners()
        owners.update(raw.get("duty_owner") or {})
        return cls(
            chat_id=raw.get("chat_id"),
            people=people,
            duty_owner=owners,
            done=dict(raw.get("done") or {}),
            shopping=list(raw.get("shopping") or []),
            weights=list(raw.get("weights") or []),
            menu_date=raw.get("menu_date") or "",
            menu_recipe=raw.get("menu_recipe") or "",
            recent=list(raw.get("recent") or []),
            has_multi=bool(raw.get("has_multi")),
        )


def load_state(path: Path) -> FlatState:
    return FlatState.from_json(json.loads(path.read_text(encoding="utf-8")))


def save_state(path: Path, state: FlatState) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(state.to_json(), ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def person_key(token: str) -> str | None:
    return PEOPLE.get(token.casefold().strip())


def duty_key(token: str) -> str | None:
    return DUTY_ALIASES.get(token.casefold().strip())


def gear_key(token: str) -> str | None:
    return GEAR_ALIASES.get(token.casefold().strip())


def owner_name(state: FlatState, duty_id: str) -> str:
    return NAMES.get(state.duty_owner.get(duty_id, ""), "хтось")


def who_is(state: FlatState, user_id: int | None) -> str | None:
    if user_id is None:
        return None
    for key, bound in state.people.items():
        if bound == user_id:
            return key
    return None


def bind_person(state: FlatState, key: str, user_id: int) -> None:
    for person, bound in list(state.people.items()):
        if bound == user_id:
            state.people[person] = None
    state.people[key] = user_id


def due_ids(day: date, shopping: list[str]) -> list[str]:
    found = ["cook", "cats"]
    if day.weekday() in {2, 6}:
        found.append("clean")
    if day.weekday() == 5 or shopping:
        found.append("shop")
    return found


def done_ids(state: FlatState, day: date) -> list[str]:
    return list(state.done.get(day.isoformat(), []))


def open_ids(state: FlatState, day: date) -> list[str]:
    closed = set(done_ids(state, day))
    return [duty_id for duty_id in due_ids(day, state.shopping) if duty_id not in closed]


def mark_done(state: FlatState, day: date, duty_id: str) -> None:
    key = day.isoformat()
    current = list(state.done.get(key, []))
    if duty_id not in current:
        current.append(duty_id)
    state.done[key] = current
    prune_done(state, day)


def prune_done(state: FlatState, day: date) -> None:
    kept = {}
    for key, duties in state.done.items():
        try:
            stamp = date.fromisoformat(key)
        except ValueError:
            continue
        if (day - stamp).days <= 21:
            kept[key] = duties
    state.done = kept


def add_shopping(state: FlatState, raw: str) -> list[str]:
    parts = []
    for chunk in raw.replace("\n", ",").split(","):
        item = " ".join(chunk.split())
        if item:
            parts.append(item)
    existing = {item.casefold() for item in state.shopping}
    for item in parts:
        if item.casefold() not in existing:
            state.shopping.append(item)
            existing.add(item.casefold())
    return parts


def clear_shopping(state: FlatState) -> None:
    state.shopping = []


def add_weight(state: FlatState, day: date, kg: float, user_id: int | None, name: str) -> dict:
    previous = [row for row in state.weights if row.get("name") == name]
    row = {"date": day.isoformat(), "kg": round(kg, 1), "user_id": user_id, "name": name}
    state.weights.append(row)
    state.weights = state.weights[-60:]
    delta = None if not previous else round(row["kg"] - previous[-1]["kg"], 1)
    first = None if not previous else round(row["kg"] - previous[0]["kg"], 1)
    return {"row": row, "delta": delta, "from_first": first, "count": len(previous) + 1}


def weights_for(state: FlatState, name: str) -> list[dict]:
    return [row for row in state.weights if row.get("name") == name]


def menu_for(state: FlatState, day: date, *, prefer: str | None = None, another: bool = False) -> Recipe:
    if (
        not another
        and prefer is None
        and state.menu_date == day.isoformat()
        and state.menu_recipe
        and by_id(state.menu_recipe) is not None
    ):
        return by_id(state.menu_recipe)
    avoid = list(state.recent)
    shift = 1 if another else 0
    if another and state.menu_recipe:
        avoid.append(state.menu_recipe)
    recipe = choose_recipe(day, has_multi=state.has_multi, avoid=avoid, prefer=prefer, shift=shift)
    if prefer is None:
        state.menu_date = day.isoformat()
        state.menu_recipe = recipe.id
        recent = [item for item in state.recent if item != recipe.id]
        recent.append(recipe.id)
        state.recent = recent[-6:]
    return recipe


def render_recipe(recipe: Recipe) -> str:
    gear = ", ".join(GEAR[item] for item in recipe.gear)
    wait = f", далі саме йде {recipe.wait} хв" if recipe.wait else ""
    steps = "\n".join(f"{index}. {step}" for index, step in enumerate(recipe.steps, start=1))
    return "\n".join(
        [
            f"🍽 {recipe.title}",
            f"Руками {recipe.minutes} хв{wait}. На трьох. З цього: {gear}.",
            f"Твоя тарілка: ~{recipe.kcal} ккал, ~{recipe.protein} г білка.",
            "",
            steps,
            "",
            recipe.note,
        ]
    )


def render_duties(state: FlatState, day: date) -> str:
    lines = ["🏠 Хто за що:"]
    for duty_id, title in DUTY_NAMES.items():
        lines.append(f"• {title}: {owner_name(state, duty_id)}")
    lines.append("Коти зараз на Назарі. Закупки можна міняти: /give shop nazar")
    lines.append("")
    opened = open_ids(state, day)
    if not opened:
        lines.append("✅ На сьогодні все закрито.")
    else:
        lines.append("Сьогодні висить:")
        for duty_id in opened:
            lines.append(f"• {DUTY_NAMES[duty_id]} — {owner_name(state, duty_id)}")
    if state.shopping:
        lines.append("🛒 Список: " + ", ".join(state.shopping))
    else:
        lines.append("🛒 Список порожній. Кидати сюди: /buy молоко, філе")
    multi = "увімкнена" if state.has_multi else "ще не беремо в рецепти"
    lines.append(f"Мультиварка: {multi}. /multi on коли купиш.")
    return "\n".join(lines)


def render_open(state: FlatState, day: date, *, with_recipe: bool) -> str | None:
    opened = open_ids(state, day)
    if not opened:
        return None
    lines = []
    if "cook" in opened:
        lines.append(f"{owner_name(state, 'cook')}, кухня. Руками до 15 хвилин, далі можна сісти.")
        if with_recipe:
            lines.append("")
            lines.append(render_recipe(menu_for(state, day)))
        else:
            title = by_id(state.menu_recipe).title if by_id(state.menu_recipe) else "рецепт"
            lines.append(f"Страва вже обрана: {title}. Повна інструкція: /cook")
    if "cats" in opened:
        lines.append(f"{owner_name(state, 'cats')}, коти: корм і лоток.")
    if "clean" in opened:
        lines.append(f"{owner_name(state, 'clean')}, сьогодні прибирання. Поверхні, підлога, кухня після їжі.")
    if "shop" in opened:
        items = ", ".join(state.shopping) if state.shopping else "глянь, що закінчилось, і допиши /buy"
        lines.append(f"{owner_name(state, 'shop')}, закупки. {items}")
    lines.append("")
    lines.append("Закрив свою справу: /done cook, /done cats, /done clean або /done shop")
    return "\n".join(lines)


def render_weight(state: FlatState, name: str) -> str:
    rows = weights_for(state, name)
    if not rows:
        return f"⚖️ {name}, ще немає жодної цифри. Вранці: /weight 110"
    last = rows[-1]
    lines = [f"⚖️ {name}: {last['kg']} кг ({last['date'][8:10]}.{last['date'][5:7]})"]
    if len(rows) >= 2:
        delta = round(last["kg"] - rows[-2]["kg"], 1)
        sign = "+" if delta > 0 else ""
        lines.append(f"Від минулого разу: {sign}{delta} кг")
        total = round(last["kg"] - rows[0]["kg"], 1)
        sign = "+" if total > 0 else ""
        lines.append(f"Від першого запису: {sign}{total} кг")
    lines.append("Раз на тиждень, вранці, після туалету, до води.")
    if name == "Антон":
        lines.append("Вечеря в /cook уже з нормальним шматком білка. Це одна тарілка, не весь день: вдень сир або яйця теж мають бути.")
    return "\n".join(lines)


def preview(day: date | None = None) -> str:
    state = FlatState()
    day = day or date(2026, 10, 5)
    text = render_duties(state, day)
    recipe = menu_for(state, day)
    return text + "\n\n" + render_recipe(recipe)
