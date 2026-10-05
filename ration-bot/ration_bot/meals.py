from __future__ import annotations

from dataclasses import dataclass
from datetime import date


@dataclass(frozen=True)
class Dish:
    id: str
    slot: str
    title: str
    ingredients: tuple[str, ...]
    shop: tuple[str, ...]
    minutes: int
    kcal: int
    protein: int
    steps: tuple[str, ...]


DISHES: tuple[Dish, ...] = (
    Dish("oats", "breakfast", "Вівсянка і яйце", ("вівсянка", "яйце"), ("вівсянка 50 г", "яйце 1 шт"), 8, 380, 18, ("50 г вівсянки залий окропом або водою на 3 хвилини.", "Поруч звари або підсмаж одне яйце.", "Солі дрібку. Цукор не сип.")),
    Dish("tvorog", "breakfast", "Творог", ("творог",), ("творог 200 г",), 3, 280, 32, ("200 г творогу в миску.", "Можна кинути яблуко або дрібку кориці.", "Це весь сніданок, хліб не додавай.")),
    Dish("omelette", "breakfast", "Омлет з помідором", ("яйце", "помідор"), ("яйця 3 шт", "помідор 1 шт"), 10, 340, 24, ("3 яйця збий, сіль.", "Помідор кружечками на сковороду, зверху яйця, кришка на 5 хвилин.",)),
    Dish("eggs-buckwheat", "breakfast", "Гречка і два яйця", ("гречка", "яйце"), ("гречка 50 г", "яйця 2 шт"), 12, 420, 22, ("50 г гречки і 100 мл води, 10 хвилин під кришкою.", "Два яйця на сковороді.",)),
    Dish("chicken-cabbage", "lunch", "Курка з аерогриля і капуста", ("куряче філе", "курк", "куря", "капуста"), ("куряче філе 250 г", "капуста 200 г"), 10, 560, 55, ("250 г філе, сіль, перець, паприка.", "Аерогриль 180 °C, 16–18 хвилин.", "Капусту поріж, поки курка сама смажиться.",)),
    Dish("chicken-buckwheat", "lunch", "Курка і гречка", ("куряче філе", "курк", "куря", "гречка"), ("куряче філе 250 г", "гречка 70 г"), 12, 680, 58, ("70 г гречки залий водою і постав варитися.", "Філе на сковороду, 7 хвилин з одного боку і 5 з другого.",)),
    Dish("chicken-salad", "dinner", "Курка і салат", ("куряче філе", "курк", "куря", "помідор", "огірок"), ("куряче філе 250 г", "помідор 1 шт", "огірок 1 шт"), 12, 540, 54, ("250 г філе на сковороді, сіль, перець.", "Помідор і огірок великими шматками поруч.",)),
    Dish("tuna-buckwheat", "lunch", "Гречка і тунець", ("тунець", "тунц", "риб", "гречка"), ("тунець 2 банки", "гречка 70 г"), 12, 560, 48, ("70 г гречки вари 12 хвилин.", "Дві банки тунця зціди і поклади зверху. Олії ложка.",)),
    Dish("tuna-salad", "dinner", "Тунець і овочі", ("тунець", "тунц", "риб", "огірок", "помідор"), ("тунець 2 банки", "огірок 1 шт", "помідор 1 шт"), 5, 420, 44, ("Зціди дві банки тунця.", "Огірок і помідор поріж. Змішай. Хліб не клади.",)),
    Dish("turkey-cabbage", "dinner", "Індичка з капустою", ("фарш індички", "індич", "капуста"), ("фарш індички 220 г", "капуста 200 г"), 15, 520, 42, ("220 г фаршу на сковороду, сіль, перець.", "Коли посвітлішає, додай капусту на 7 хвилин під кришкою.",)),
    Dish("turkey-potato", "lunch", "Індичка і картопля з аерогриля", ("фарш індички", "індич", "картопля"), ("фарш індички 220 г", "картопля 250 г"), 12, 680, 40, ("250 г картоплі часточками, ложка олії, сіль. Аерогриль 200 °C, 20 хвилин.", "220 г фаршу на сковороді, поки картопля сама доходить.",)),
    Dish("hake-buckwheat", "dinner", "Хек і гречка", ("хек", "риб", "гречка"), ("хек 250 г", "гречка 70 г"), 12, 520, 46, ("70 г гречки постав варитися.", "250 г хека в аерогриль на 180 °C, 14 хвилин. Сіль і перець.",)),
    Dish("hake-salad", "lunch", "Хек і огірок", ("хек", "риб", "огірок"), ("хек 250 г", "огірок 1 шт"), 8, 380, 44, ("250 г хека в аерогриль, 180 °C, 14 хвилин.", "Огірок поруч, без майонезу.",)),
    Dish("eggs-salad", "dinner", "Яйця і салат", ("яйце", "огірок", "помідор"), ("яйця 3 шт", "огірок 1 шт", "помідор 1 шт"), 10, 360, 24, ("3 яйця на сковороді.", "Огірок і помідор велико.",)),
    Dish("mushroom-buckwheat", "dinner", "Гречка з грибами", ("гриби", "гриб", "печериц", "цибуля", "цибул", "гречка"), ("печериці 150 г", "цибуля 1 шт", "гречка 60 г"), 15, 380, 14, ("Цибулю і гриби на сковороду.", "Гречку звари окремо і змішай.",)),
)

SLOTS = ("breakfast", "lunch", "dinner")
SLOT_TITLES = {"breakfast": "Сніданок", "lunch": "Обід", "dinner": "Вечеря"}


def stem(word: str) -> str:
    text = word.casefold().replace("ї", "і").strip()
    for suffix in ("ами", "ями", "ів", "ей", "ою", "ям", "ях", "и", "і", "а", "я", "у", "ю"):
        if len(text) > 4 and text.endswith(suffix):
            return text[: -len(suffix)]
    return text


def blocked(dish: Dish, hates: list[str]) -> bool:
    stems = [stem(item) for item in hates if stem(item)]
    for ingredient in dish.ingredients:
        ingredient_stem = stem(ingredient)
        for hate in stems:
            if hate in ingredient_stem or ingredient_stem in hate:
                return True
    return False


def options(slot: str, hates: list[str]) -> list[Dish]:
    found = [dish for dish in DISHES if dish.slot == slot and not blocked(dish, hates)]
    if found:
        return sorted(found, key=lambda dish: dish.id)
    return sorted((dish for dish in DISHES if dish.slot == slot), key=lambda dish: dish.id)[:1]


def pick(slot: str, day: date, hates: list[str], *, shift: int = 0, avoid: set[str] | None = None) -> Dish:
    pool = options(slot, hates)
    fresh = [dish for dish in pool if dish.id not in (avoid or set())]
    use = fresh or pool
    index = (day.toordinal() + shift) % len(use)
    return use[index]


def day_menu(day: date, hates: list[str], *, shifts: dict[str, int] | None = None) -> dict[str, Dish]:
    shifts = shifts or {}
    menu: dict[str, Dish] = {}
    used: set[str] = set()
    for slot in SLOTS:
        dish = pick(slot, day, hates, shift=shifts.get(slot, 0), avoid=used)
        menu[slot] = dish
        used.add(dish.id)
    return menu


def render_dish(slot: str, dish: Dish) -> str:
    steps = "\n".join(f"{index}. {step}" for index, step in enumerate(dish.steps, start=1))
    return "\n".join(
        [
            f"{SLOT_TITLES[slot]}: {dish.title}",
            f"Руками {dish.minutes} хв. Твоя тарілка ~{dish.kcal} ккал, ~{dish.protein} г білка.",
            steps,
        ]
    )


def render_menu(menu: dict[str, Dish], hates: list[str]) -> str:
    lines = ["🍽 Твій день, одна порція."]
    if hates:
        lines.append("Без: " + ", ".join(hates) + ".")
    kcal = sum(dish.kcal for dish in menu.values())
    protein = sum(dish.protein for dish in menu.values())
    lines.append(f"Разом близько {kcal} ккал і {protein} г білка.")
    if kcal < 1700:
        lines.append("На 110 кг цього мало. Між обідом і вечерею додай 200 г творогу, це ще близько 300 ккал і 35 г білка.")
    lines.append("")
    for slot in SLOTS:
        lines.append(render_dish(slot, menu[slot]))
        lines.append("")
    lines.append("Інша страва: /next lunch. Прибрати продукт: /hate гриби")
    return "\n".join(lines).strip()


def shopping(menu: dict[str, Dish]) -> list[str]:
    items: list[str] = []
    seen: set[str] = set()
    for slot in SLOTS:
        for item in menu[slot].shop:
            if item.casefold() not in seen:
                seen.add(item.casefold())
                items.append(item)
    return items
