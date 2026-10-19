from __future__ import annotations

CHEAT_WORDS = (
    "макдак",
    "макдон",
    "в макові",
    "в маке",
    "макові",
    "kfc",
    "кфс",
    "шаур",
    "піц",
    "пиц",
    "бургер",
    "гріш",
    "греш",
    "ізвін",
    "извин",
    "солод",
    "торт",
    "пиво",
    "шоколад",
    "донер",
    "суші",
    "суши",
    "снекер",
)
WAKE_WORDS = ("просну", "просну", "встав", "снідан", "завтрак", "посніда")
HUNGRY_WORDS = (
    "жрат",
    "жрать",
    "голод",
    "хават",
    "хавать",
    "хаваю",
    "хочу їсти",
    "хочу есть",
    "чо здєлать",
    "чо сделать",
    "шо з",
    "поїсти",
    "пожрати",
    "їсти",
)
MONEY_WORDS = (
    "бабк",
    "бабл",
    "грош",
    "креатин",
    "протеїн",
    "протеин",
    "добав",
    "волкінг",
    "волкинг",
    "walking",
    "доріж",
    "імб",
    "имб",
)
SCAM_WORDS = ("жироспа", "карнітин", "карнитин", "бца", "bcaa")

LADDER = (
    (
        "creatine",
        "Є вільні бабки? Візьми креатин моногідрат. 5 грамів щодня кидай у воду, банка на місяць коштує копійки. Жир він не палить. Віджимання і присідання з ним легше тягнути.",
    ),
    (
        "protein",
        "Наступна імба, якщо курка вже в печінках: сироватковий протеїн. Один шейк, коли ліньки стояти біля сковороди. Не замість тарілки, а замість дірки, де мав бути білок.",
    ),
    (
        "walkpad",
        "Ось що реально зніме вагу: walking pad. Два коротких кола тричі на тиждень живіт не спалять. 30–40 хвилин кроку щодня, поки щось дивишся, спалять.",
    ),
)


def intent(text: str) -> str:
    folded = " " + text.casefold().replace("ё", "е") + " "
    if any(word in folded for word in SCAM_WORDS):
        return "scam"
    if any(word in folded for word in CHEAT_WORDS):
        return "cheat"
    if any(word in folded for word in MONEY_WORDS):
        return "money"
    if any(word in folded for word in WAKE_WORDS):
        return "breakfast"
    if any(word in folded for word in HUNGRY_WORDS):
        return "hungry"
    return "none"


def slot_for_hour(hour: int) -> str:
    if hour < 11:
        return "breakfast"
    if hour < 16:
        return "lunch"
    return "dinner"


def goal_kg(kg: float) -> float:
    if kg > 100:
        return 100
    if kg > 90:
        return 90
    return 90


def roast_weight(kg: float) -> str:
    goal = goal_kg(kg)
    left = round(kg - goal, 1)
    if kg <= 90:
        return f"О, {kg:.1f} кг при 185 см. Дотиснув до дев'яноста. Далі тримай тарілку, а не святкуй маком."
    if left <= 0:
        return f"{kg:.1f} кг. Сотка є. Наступна цифра 90, і це ще не привід лізти в мак."
    return (
        f"Піздєц ти жиробас, блядь. Ти важиш {kg:.1f} кг при 185 см, це ж пізда. "
        f"До {goal:.0f} кг ще {left:.1f}. А ну давай худай нахуй."
    )


def roast_cheat() -> str:
    return (
        "Ізвінітісь? Та йди нахуй з вибаченнями. Мак у тебе на животі лишиться, не в чаті. "
        "Доїдай, раз уже купив, і наступна їжа нормальна. Голодом це не компенсуй, бо ввечері знову зірвешся."
    )


def meal_call(slot: str, kg: float) -> str:
    if slot == "breakfast":
        return f"Проснувся, жиробас на {kg:.1f} кг. Поки не поліз у солодке, сніданок по кроках:"
    if slot == "lunch":
        return "Жрати хочеш. Обід, не мак. Роби рівно так:"
    return "Вечір. Одна вечеря, без другої порції. По кроках:"


def next_buy(owned: list[str]) -> str:
    for key, text in LADDER:
        if key not in owned:
            return text + "\nКупив? Напиши /got " + key
    return "Імби на черзі нема. Вагу знімає тарілка і кроки, не нова банка."


def roast_scam() -> str:
    return (
        "Жироспалювач, L-карнітин і BCAA то розвод на твої бабки. "
        "Працює їжа, кроки і креатин по 5 грамів. Решту полиць у магазині спортпіта можна не бачити."
    )
