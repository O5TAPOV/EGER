from __future__ import annotations

from datetime import date

# rounds, jacks, burpees, push-ups, jump squats, plank
LEVELS = (
    (2, 30, 30, 7, 7, 30),
    (3, 30, 30, 7, 7, 30),
    (3, 35, 30, 8, 8, 35),
    (3, 40, 35, 9, 9, 40),
    (3, 45, 40, 10, 10, 45),
)

DEFAULT_DAYS = (0, 2, 4)
DAY_NAMES = {
    0: "пн",
    1: "вт",
    2: "ср",
    3: "чт",
    4: "пт",
    5: "сб",
    6: "нд",
}
DAY_ALIASES = {
    "mon": 0,
    "пн": 0,
    "понеділок": 0,
    "tue": 1,
    "вт": 1,
    "вівторок": 1,
    "wed": 2,
    "ср": 2,
    "середа": 2,
    "thu": 3,
    "чт": 3,
    "четвер": 3,
    "fri": 4,
    "пт": 4,
    "пʼятниця": 4,
    "пятниця": 4,
    "sat": 5,
    "сб": 5,
    "субота": 5,
    "sun": 6,
    "нд": 6,
    "неділя": 6,
}


def level_row(level: int) -> tuple[int, int, int, int, int, int]:
    index = min(max(level, 0), len(LEVELS) - 1)
    return LEVELS[index]


def is_train_day(day: date, days: list[int]) -> bool:
    use = days or list(DEFAULT_DAYS)
    return day.weekday() in use


def render_train(level: int, *, done_at_level: int) -> str:
    rounds, jacks, burpees, pushups, squats, plank = level_row(level)
    left = max(0, 4 - done_at_level)
    lines = [
        f"💪 Сьогодні треня. {rounds} кола, між колами 90 секунд.",
        "",
        f"1. Зірочки {jacks} сек",
        f"2. Берпі {burpees} сек",
        f"3. Віджимання {pushups}",
        f"4. Присідання з вистрибуванням {squats}",
        f"5. Планка {plank} сек",
        "",
        "Між вправами тільки віддихайся. Коло має лишатись колом.",
        "Якщо коліна неприємно нагадують про себе, присідай до стільця, а в берпі крокуй назад замість стрибка.",
        f"До наступного кроку лишилось закритих трен: {left}. Закрив: /done",
    ]
    if level == 0:
        lines.append("Зараз твої менші цифри і два кола. Третій круг з'явиться сам, коли ці два стануть звичайними.")
    return "\n".join(lines)


def render_rest() -> str:
    return "Сьогодні без берпі. Треня в понеділок, середу і п'ятницю. Якщо є настрій, 20–40 хвилин пішки."


def advance(level: int, done_at_level: int) -> tuple[int, int, bool]:
    if done_at_level >= 4 and level < len(LEVELS) - 1:
        return level + 1, 0, True
    return level, done_at_level, False
