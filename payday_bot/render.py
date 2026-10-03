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


def sell_rate(state: State):
    return state.usd_uah if state.usd_uah > 0 else money(1)


def pile_totals(state: State):
    rate = sell_rate(state)
    saved_uah = money(state.lexus_saved_usd * rate)
    parents_usd = money(state.parents_held_uah / rate) if state.parents_held_uah else money(0)
    total_usd = money(state.lexus_saved_usd + parents_usd)
    total_uah = money(saved_uah + state.parents_held_uah)
    left_usd = money(state.lexus_target_usd - total_usd)
    return {
        "rate": rate,
        "saved_uah": saved_uah,
        "parents_usd": parents_usd,
        "total_usd": total_usd,
        "total_uah": total_uah,
        "left_usd": left_usd,
    }


def render_pile(state: State) -> str:
    totals = pile_totals(state)
    buy = state.xchange_buy
    rate_line = f"💱 X-Change, продаж {fmt(totals['rate'])}"
    if buy > 0:
        rate_line += f", купівля {fmt(buy)}"
    lines = [
        rate_line,
        f"💵 Твої долари: {fmt(state.lexus_saved_usd)} $ = {fmt(totals['saved_uah'])} грн",
        f"👪 У батьків: {fmt(state.parents_held_uah)} грн = {fmt(totals['parents_usd'])} $",
        f"🚗 Разом: {fmt(totals['total_usd'])} $ = {fmt(totals['total_uah'])} грн",
        f"🎯 До {fmt(state.lexus_target_usd)} $ лишилось {fmt(totals['left_usd'])} $",
    ]
    return "\n".join(lines)


def render_balances(state: State) -> str:
    lines = ["💳 Залишки:"]
    if not state.open_debts() and state.parked_uah <= 0:
        lines.append("✅ Боргів немає.")
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
        lines.append(f"🅿️ Окремо на розстрочку: {fmt(state.parked_uah)} грн. Це не життя.")
    owed = [item for item in state.receivables if item.amount > 0]
    if owed:
        bits = [f"{item.who} {fmt(item.amount)}" for item in owed]
        lines.append("🤝 Тобі винні: " + ", ".join(bits) + ". У план не ставлю, поки гроші не в руках.")
    lines.append("")
    lines.append(render_pile(state))
    return "\n".join(lines)


def render_plan(state: State, today: date) -> str:
    payday = next_friday(today)
    parts = []
    if today < payday:
        dues = [debt for debt in immediate_dues(state, today, payday) if debt.due < payday]
        if dues:
            parts.append("⏰ До п'ятниці, з грошей які вже є, не з нової зарплати:")
            for debt in dues:
                parts.append(f"• {debt.title}: {fmt(debt.balance)} грн до {debt.due.strftime('%d.%m')}")
            parts.append("")
        friday = build_plan(state, payday, drop_dues_before=payday)
    else:
        friday = build_plan(state, payday)
    parts.append(render_friday(friday, state.usd_uah))
    return "\n".join(parts).strip()


def render_friday(plan: Plan, usd_uah) -> str:
    lines = [f"📅 П'ятниця {plan.payday.strftime('%d.%m')}", ""]
    lines.append(f"💵 Зарплата ~{fmt(plan.income)} грн")
    if plan.rent > 0:
        lines.append(f"🏠 Хата: {fmt(plan.rent)} грн")
        lines.append(f"🛒 На їжу і дрібне цього тижня: {fmt(plan.living)} грн")
    else:
        lines.append(f"🛒 На життя лишаєш {fmt(plan.living)} грн")
    if plan.parents > 0:
        lines.append(f"👪 Батькам цього тижня: {fmt(plan.parents)} грн")

    if plan.mode == "lexus":
        usd = money(plan.lexus_uah / money(usd_uah)) if plan.lexus_uah > 0 else money(0)
        lines.append(f"🚗 У долари: {fmt(plan.lexus_uah)} грн (~{fmt(usd)} $)")
        lines.append("✅ Боргів немає. Десятку на кредит більше не відкладаєш.")
        if usd > 0:
            lines.append("Як купиш долари, напиши /saved +сума.")
        return "\n".join(lines)

    lines.append(f"🔥 У борг іде {fmt(plan.debt_budget)} грн")
    lines.append("")
    lines.append("Гасиш:")
    if not plan.payments:
        lines.append("• Цього тижня в борг з зарплати не виходить.")
    for payment in plan.payments:
        lines.append(f"• {payment.title}: {fmt(payment.amount)} грн ({REASONS[payment.reason]})")
    if plan.parked_after > 0:
        lines.append("")
        lines.append(
            f"🅿️ Після оплати окремо лежить {fmt(plan.parked_after)} грн. "
            "Це на повне закриття розстрочки. Не витрачай."
        )
    if plan.lump_title and plan.lump_gap > 0:
        lines.append(
            f"⚠️ {plan.lump_title} банк візьме тільки цілком. Не вистачає {fmt(plan.lump_gap)} грн. "
            "Частково кидати не можна. Докинь з життя або зачекай наступної п'ятниці."
        )
    if plan.extra_pocket > 0:
        lines.append(
            f"✅ Борги закриваються, і ще лишається {fmt(plan.extra_pocket)} грн. "
            "Купи на них долари і напиши /saved +сума."
        )
    if plan.budget_short:
        lines.append("⚠️ Зарплата не покриває хату, батьків і життя. Місячні платіжі по розстрочці не пропускай.")
    lines.append("")
    lines.append("🏦 Телефон і екофлоу не приймають «кину 5 тисяч». Тільки платіж за місяць або вся сума.")
    lines.append("✅ Як оплатиш у банку, напиши /done. До оплати не тисни.")
    return "\n".join(lines)


def render_hold(state: State, today: date) -> str:
    payday = next_friday(today)
    if payday == today:
        payday = next_friday(today + timedelta(days=1))
    days = (payday - today).days
    lines = [
        f"⏳ До зарплати {days} дн. (п'ятниця {payday.strftime('%d.%m')}).",
        "",
        f"🛒 {fmt(state.living_uah)} грн на життя цього тижня можна тратити.",
        "🔥 Гроші, які вже відклав на борг, і те, що лежить на закриття розстрочки, не чіпаєш.",
    ]
    for debt in immediate_dues(state, today, payday):
        lines.append(
            f"⏰ До {debt.due.strftime('%d.%m')} віддай {debt.title}: {fmt(debt.balance)} грн. "
            "Після оплати напиши /set завтра 0."
        )
    if state.parked_uah > 0:
        lines.append(f"🅿️ Зараз окремо лежить {fmt(state.parked_uah)} грн.")
    else:
        lines.append("🅿️ Окремої купи на розстрочку поки немає.")
    return "\n".join(lines)


def render_lexus(state: State, rate_note: str = "") -> str:
    totals = pile_totals(state)
    lines = []
    if rate_note:
        lines.append(rate_note)
        lines.append("")
    lines.append(render_pile(state))
    if state.open_debts() or state.parked_uah > 0:
        lines.append("Поки є борг, нові п'ятниці ще гасять кредит, а не машину.")
    elif totals["left_usd"] > 0:
        free = money(state.weekly_income_usd * sell_rate(state) - state.living_uah)
        per_week = money(free / sell_rate(state)) if free > 0 else money(0)
        if per_week > 0:
            weeks = int((totals["left_usd"] / per_week).to_integral_value(rounding="ROUND_CEILING"))
            lines.append(f"📆 У вільний тиждень виходить ~{fmt(per_week)} $. Якщо так щоп'ятниці, лишилось близько {weeks}.")
    lines.append("")
    lines.append("💵 Скільки доларів є зараз: /saved 150")
    lines.append("💵 Докинув ще: /saved +40")
    lines.append("👪 Закинув батькам гривні: /parents 10000")
    return "\n".join(lines)


def render_done(plan: Plan) -> str:
    lines = [
        f"✅ Записав п'ятницю {plan.payday.strftime('%d.%m')}.",
        "Банк я не чіпав. Це лише позначка, що ти вже оплатив ось це:",
        "",
    ]
    if plan.payments:
        for payment in plan.payments:
            lines.append(f"• {payment.title}: {fmt(payment.amount)} грн")
    elif plan.mode == "lexus":
        lines.append(f"• У долари: {fmt(plan.lexus_uah)} грн")
    else:
        lines.append("• Цього тижня оплат у плані не було.")
    if plan.parked_after > 0:
        lines.append(f"🅿️ Окремо лежить {fmt(plan.parked_after)} грн. Їх не тратиш.")
    lines.append("")
    lines.append("Наступний розклад сам не відкривається. Кнопка План покаже вже наступну п'ятницю.")
    lines.append("Якщо натиснув рано, /undo поверне залишки як було.")
    return "\n".join(lines)
