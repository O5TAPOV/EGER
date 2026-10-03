from datetime import date, timedelta

from payday_bot.model import State
from payday_bot.money import fmt, money, next_friday
from payday_bot.plan import Plan, build_plan, immediate_dues


REASONS = {
    "due": "цілком, строк уже настав",
    "full": "цілком",
    "monthly": "платіж за цей місяць",
    "flexible": "частково, скільки влізе",
}


def render_balances(state: State) -> str:
    lines = ["Залишки:"]
    if not state.open_debts() and state.parked_uah <= 0:
        lines.append("Боргів немає.")
    for debt in state.debts:
        if debt.balance <= 0:
            continue
        extra = ""
        if debt.monthly is not None and debt.settle == "monthly_or_full":
            extra = f", місяць {fmt(debt.monthly)} або вся сума"
        if debt.due is not None and debt.settle == "asap":
            extra = f", до {debt.due.strftime('%d.%m')}"
        lines.append(f"• {debt.title}: {fmt(debt.balance)} грн{extra}")
    if state.parked_uah > 0:
        lines.append(f"Лежить окремо на закриття розстрочки: {fmt(state.parked_uah)} грн. Це не життя.")
    owed = [item for item in state.receivables if item.amount > 0]
    if owed:
        bits = [f"{item.who} {fmt(item.amount)}" for item in owed]
        lines.append("Тобі винні: " + ", ".join(bits) + ". У план не ставлю, поки гроші не в руках.")
    left = state.lexus_target_usd - state.lexus_saved_usd
    lines.append(
        f"Лексус: зібрано {fmt(state.lexus_saved_usd)} $ з {fmt(state.lexus_target_usd)} $. "
        f"Лишилось {fmt(left)} $."
    )
    return "\n".join(lines)


def render_plan(state: State, today: date) -> str:
    payday = next_friday(today)
    parts = []
    if today < payday:
        dues = [debt for debt in immediate_dues(state, today, payday) if debt.due < payday]
        if dues:
            parts.append("До п'ятниці, з грошей які вже є, не з нової зарплати:")
            for debt in dues:
                parts.append(f"• {debt.title}: {fmt(debt.balance)} грн до {debt.due.strftime('%d.%m')}")
            parts.append("")
        friday = build_plan(state, payday, drop_dues_before=payday)
    else:
        friday = build_plan(state, payday)
    parts.append(render_friday(friday, state.usd_uah))
    return "\n".join(parts).strip()


def render_friday(plan: Plan, usd_uah) -> str:
    lines = [f"П'ятниця {plan.payday.strftime('%d.%m')}", ""]
    lines.append(f"Зарплата ~{fmt(plan.income)} грн")
    if plan.rent > 0:
        lines.append(f"Хата: {fmt(plan.rent)} грн")
        lines.append(f"На їжу і дрібне цього тижня: {fmt(plan.living)} грн")
    else:
        lines.append(f"На життя лишаєш {fmt(plan.living)} грн")
    if plan.parents > 0:
        lines.append(f"Батькам: {fmt(plan.parents)} грн")

    if plan.mode == "lexus":
        usd = money(plan.lexus_uah / money(usd_uah)) if plan.lexus_uah > 0 else money(0)
        lines.append(f"У долари: {fmt(plan.lexus_uah)} грн (~{fmt(usd)} $)")
        lines.append("Боргів немає. Десятку на кредит більше не відкладаєш.")
        if usd > 0:
            lines.append("Як купиш долари, напиши /saved і суму в доларах.")
        return "\n".join(lines)

    lines.append(f"У борг іде {fmt(plan.debt_budget)} грн")
    lines.append("")
    lines.append("Гасиш:")
    if not plan.payments:
        lines.append("• Цього тижня в борг з зарплати не виходить.")
    for payment in plan.payments:
        lines.append(f"• {payment.title}: {fmt(payment.amount)} грн ({REASONS[payment.reason]})")
    if plan.parked_after > 0:
        lines.append("")
        lines.append(
            f"Після оплати окремо лежить {fmt(plan.parked_after)} грн. "
            "Це на повне закриття розстрочки. Не витрачай."
        )
    if plan.lump_title and plan.lump_gap > 0:
        lines.append(
            f"{plan.lump_title} банк візьме тільки цілком. Не вистачає {fmt(plan.lump_gap)} грн. "
            "Частково кидати не можна. Докинь з життя або зачекай наступної п'ятниці."
        )
    if plan.extra_pocket > 0:
        lines.append(
            f"Борги закриваються, і ще лишається {fmt(plan.extra_pocket)} грн. "
            "Купи на них долари і напиши /saved сума."
        )
    if plan.budget_short:
        lines.append("Зарплата не покриває хату, батьків і життя. Місячні платіжі по розстрочці не пропускай.")
    lines.append("")
    lines.append("Телефон і екофлоу не приймають «кину 5 тисяч». Тільки платіж за місяць або вся сума.")
    lines.append("Як оплатиш у банку: /done")
    return "\n".join(lines)


def render_hold(state: State, today: date) -> str:
    payday = next_friday(today)
    if payday == today:
        payday = next_friday(today + timedelta(days=1))
    days = (payday - today).days
    lines = [
        f"До зарплати {days} дн. (п'ятниця {payday.strftime('%d.%m')}).",
        "",
        f"{fmt(state.living_uah)} грн на життя цього тижня можна тратити.",
        "Гроші, які вже відклав на борг, і те, що лежить на закриття розстрочки, не чіпаєш.",
    ]
    for debt in immediate_dues(state, today, payday):
        lines.append(
            f"До {debt.due.strftime('%d.%m')} віддай {debt.title}: {fmt(debt.balance)} грн. "
            "Після оплати напиши /set завтра 0."
        )
    if state.parked_uah > 0:
        lines.append(f"Зараз окремо лежить {fmt(state.parked_uah)} грн.")
    else:
        lines.append("Окремої купи на розстрочку поки немає.")
    return "\n".join(lines)


def render_lexus(state: State) -> str:
    left = state.lexus_target_usd - state.lexus_saved_usd
    per_week = money(0)
    if not state.open_debts():
        per_week = money((income_of(state)) / state.usd_uah)
    lines = [
        f"Лексус: {fmt(state.lexus_saved_usd)} $ з {fmt(state.lexus_target_usd)} $.",
        f"Лишилось {fmt(left)} $.",
    ]
    if state.open_debts() or state.parked_uah > 0:
        lines.append("Поки є борг або купа на розстрочку, у долари на машину нічого не йде.")
    elif per_week > 0:
        weeks = int((left / per_week).to_integral_value(rounding="ROUND_CEILING")) if per_week > 0 else 0
        lines.append(f"Після хати і батьків темп плаває. У вільний тиждень це ~{fmt(per_week)} $.")
        if left > 0 and weeks:
            lines.append(f"Якщо кожен тиждень вільний, лишилось близько {weeks} п'ятниць.")
    lines.append("Купив долари: /saved 100")
    return "\n".join(lines)


def income_of(state: State):
    return money(state.weekly_income_usd * state.usd_uah - state.living_uah)
