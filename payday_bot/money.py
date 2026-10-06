from decimal import Decimal, ROUND_HALF_UP
from datetime import date, timedelta


TWOPLACES = Decimal("0.01")


def money(value) -> Decimal:
    return Decimal(str(value)).quantize(TWOPLACES, rounding=ROUND_HALF_UP)


def fmt(value) -> str:
    amount = money(value)
    sign = "-" if amount < 0 else ""
    amount = abs(amount)
    whole, frac = f"{amount:.2f}".split(".")
    groups = []
    while whole:
        groups.append(whole[-3:])
        whole = whole[:-3]
    text = sign + " ".join(reversed(groups))
    if frac != "00":
        text += "," + frac
    return text


def next_friday(day: date) -> date:
    ahead = (4 - day.weekday()) % 7
    return day + timedelta(days=ahead)


def is_first_friday(day: date) -> bool:
    return day.weekday() == 4 and day.day <= 7


def is_second_friday(day: date) -> bool:
    return day.weekday() == 4 and 8 <= day.day <= 14
