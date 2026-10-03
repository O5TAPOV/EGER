from datetime import date
from decimal import Decimal

from payday_bot.model import Debt, Receivable, State
from payday_bot.money import money


def initial_state() -> State:
    """Стартові залишки на 3 жовтня 2026. Файл стану в git не потрапляє."""
    return State(
        usd_uah=money("44.83"),
        weekly_income_usd=money("500"),
        living_uah=money("11000"),
        rent_uah=money("11000"),
        rent_week_living_uah=money("4000"),
        parents_uah=money("10000"),
        parents_from=date(2026, 11, 1),
        lexus_target_usd=money("10000"),
        debts=[
            Debt("tomorrow", "Кредит до завтра", money("1051"), "asap", due=date(2026, 10, 4)),
            Debt("mouse", "Мишка", money("1189.30"), "early_full"),
            Debt("phone", "Телефон", money("9599.40"), "monthly_or_full", monthly=money("1599.90")),
            Debt("ecoflow", "Екофлоу", money("15427.72"), "monthly_or_full", monthly=money("1285.64")),
            Debt("limit", "Кредитний ліміт", money("15408.89"), "flexible"),
        ],
        receivables=[
            Receivable("один тіп", money("800"), count_on=False),
            Receivable("ще один", money("3000"), count_on=False),
        ],
    )


def find_debt(token: str, state: State) -> Debt | None:
    key = token.casefold().strip()
    aliases = {
        "limit": "limit",
        "ліміт": "limit",
        "кредит": "limit",
        "phone": "phone",
        "телефон": "phone",
        "eco": "ecoflow",
        "ecoflow": "ecoflow",
        "екофлоу": "ecoflow",
        "еко": "ecoflow",
        "mouse": "mouse",
        "мишка": "mouse",
        "мишу": "mouse",
        "tomorrow": "tomorrow",
        "завтра": "tomorrow",
    }
    debt_id = aliases.get(key, key)
    try:
        return state.debt(debt_id)
    except KeyError:
        return None


def parse_amount(raw: str) -> Decimal:
    cleaned = raw.strip().replace(" ", "").replace(",", ".")
    return money(cleaned)
