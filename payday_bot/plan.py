from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, timedelta

from payday_bot.model import State
from payday_bot.money import is_first_friday, is_second_friday, money, next_friday


@dataclass
class Payment:
    debt_id: str
    title: str
    amount: object
    reason: str

    def __post_init__(self) -> None:
        self.amount = money(self.amount)


@dataclass
class Plan:
    payday: date
    income: object
    living: object
    rent: object
    parents: object
    debt_budget: object
    payments: list[Payment] = field(default_factory=list)
    parked_after: object = 0
    extra_pocket: object = 0
    lexus_uah: object = 0
    lump_title: str | None = None
    lump_gap: object = 0
    budget_short: bool = False
    mode: str = "debt"

    def __post_init__(self) -> None:
        self.income = money(self.income)
        self.living = money(self.living)
        self.rent = money(self.rent)
        self.parents = money(self.parents)
        self.debt_budget = money(self.debt_budget)
        self.parked_after = money(self.parked_after)
        self.extra_pocket = money(self.extra_pocket)
        self.lexus_uah = money(self.lexus_uah)
        self.lump_gap = money(self.lump_gap)


def income_uah(state: State):
    return money(state.weekly_income_usd * state.usd_uah)


def week_split(state: State, payday: date):
    income = income_uah(state)
    rent = state.rent_uah if is_first_friday(payday) else money(0)
    parents = state.parents_uah if payday >= state.parents_from and is_second_friday(payday) else money(0)
    living = state.rent_week_living_uah if rent > 0 else state.living_uah
    debt_budget = money(income - living - rent - parents + state.windfall_uah)
    return income, living, rent, parents, debt_budget


def immediate_dues(state: State, today: date, payday: date):
    found = []
    for debt in state.debts:
        if debt.balance <= 0 or debt.settle != "asap" or debt.due is None:
            continue
        if debt.due < payday:
            found.append(debt)
    return [debt for debt in found if debt.due <= payday]


def _copy_state(state: State) -> State:
    return State.from_json(state.to_json())


def build_plan(state: State, payday: date, *, drop_dues_before: date | None = None) -> Plan:
    working = _copy_state(state)
    if drop_dues_before is not None:
        for debt in working.debts:
            if debt.settle == "asap" and debt.due is not None and debt.due < drop_dues_before:
                debt.balance = money(0)

    income, living, rent, parents, debt_budget = week_split(working, payday)
    budget_short = False
    if debt_budget < 0 and working.open_debts():
        budget_short = True
        debt_budget = money(0)

    if not working.open_debts() and working.parked_uah <= 0:
        lexus = debt_budget if debt_budget > 0 else money(0)
        return Plan(
            payday=payday,
            income=income,
            living=living,
            rent=rent,
            parents=parents,
            debt_budget=money(0),
            lexus_uah=lexus,
            budget_short=budget_short,
            mode="lexus",
        )

    payments, parked_after, extra = _allocate(working, payday, debt_budget)
    lump_title, lump_gap = _lump_gap(working, parked_after)
    return Plan(
        payday=payday,
        income=income,
        living=living,
        rent=rent,
        parents=parents,
        debt_budget=debt_budget,
        payments=payments,
        parked_after=parked_after,
        extra_pocket=extra,
        lump_title=lump_title,
        lump_gap=lump_gap,
        budget_short=budget_short,
        mode="debt",
    )


def _lump_gap(state: State, parked) -> tuple[str | None, object]:
    parked = money(parked)
    if parked <= 0:
        return None, money(0)
    if any(debt.balance > 0 and debt.settle == "flexible" for debt in state.debts):
        return None, money(0)
    waiting = [
        debt
        for debt in state.debts
        if debt.balance > 0 and debt.settle == "monthly_or_full" and parked < debt.balance
    ]
    if not waiting:
        return None, money(0)
    target = min(waiting, key=lambda debt: debt.balance)
    return target.title, money(target.balance - parked)


def _allocate(state: State, payday: date, debt_budget):
    pool = money(state.parked_uah + debt_budget)
    payments: list[Payment] = []
    month = f"{payday.year:04d}-{payday.month:02d}"

    def take(debt, amount, reason: str) -> None:
        nonlocal pool
        amount = money(min(money(amount), debt.balance, pool))
        if amount <= 0:
            return
        debt.balance = money(debt.balance - amount)
        pool = money(pool - amount)
        payments.append(Payment(debt.id, debt.title, amount, reason))
        if reason in {"monthly", "full"} or debt.balance <= 0:
            state.installment_paid[debt.id] = month

    for debt in state.debts:
        if debt.settle == "asap" and debt.balance > 0 and (debt.due is None or debt.due <= payday):
            take(debt, debt.balance, "due")

    for debt in state.debts:
        if debt.settle == "early_full" and debt.balance > 0 and pool >= debt.balance:
            take(debt, debt.balance, "full")

    flexible_open = any(debt.settle == "flexible" and debt.balance > 0 for debt in state.debts)
    installments = [debt for debt in state.debts if debt.settle == "monthly_or_full"]

    if flexible_open:
        for debt in installments:
            if debt.balance <= 0 or debt.monthly is None:
                continue
            if state.installment_paid.get(debt.id) == month:
                continue
            take(debt, debt.monthly, "monthly")
        for debt in state.debts:
            if debt.settle == "flexible" and debt.balance > 0:
                take(debt, debt.balance, "flexible")
        return payments, pool, money(0)

    for debt in sorted((item for item in installments if item.balance > 0), key=lambda item: item.balance):
        if pool >= debt.balance:
            take(debt, debt.balance, "full")
        elif state.installment_paid.get(debt.id) != month and debt.monthly is not None:
            take(debt, debt.monthly, "monthly")

    if all(debt.balance <= 0 for debt in state.debts):
        return payments, money(0), pool
    return payments, pool, money(0)


def apply_plan(state: State, plan: Plan) -> bool:
    key = plan.payday.isoformat()
    if key in state.applied_paydays:
        return False
    month = f"{plan.payday.year:04d}-{plan.payday.month:02d}"
    for payment in plan.payments:
        debt = state.debt(payment.debt_id)
        debt.balance = money(max(0, debt.balance - payment.amount))
        if payment.reason in {"monthly", "full"} or debt.balance <= 0:
            state.installment_paid[debt.id] = month
    state.parked_uah = plan.parked_after
    state.windfall_uah = money(0)
    state.applied_paydays.append(key)
    return True


def plan_to_json(plan: Plan) -> dict:
    return {
        "payday": plan.payday.isoformat(),
        "income": f"{plan.income:.2f}",
        "living": f"{plan.living:.2f}",
        "rent": f"{plan.rent:.2f}",
        "parents": f"{plan.parents:.2f}",
        "debt_budget": f"{plan.debt_budget:.2f}",
        "payments": [
            {
                "debt_id": payment.debt_id,
                "title": payment.title,
                "amount": f"{payment.amount:.2f}",
                "reason": payment.reason,
            }
            for payment in plan.payments
        ],
        "parked_after": f"{plan.parked_after:.2f}",
        "extra_pocket": f"{plan.extra_pocket:.2f}",
        "lexus_uah": f"{plan.lexus_uah:.2f}",
        "lump_title": plan.lump_title,
        "lump_gap": f"{plan.lump_gap:.2f}",
        "budget_short": plan.budget_short,
        "mode": plan.mode,
    }


def plan_from_json(raw: dict) -> Plan:
    return Plan(
        payday=date.fromisoformat(raw["payday"]),
        income=raw["income"],
        living=raw["living"],
        rent=raw["rent"],
        parents=raw["parents"],
        debt_budget=raw["debt_budget"],
        payments=[
            Payment(item["debt_id"], item["title"], item["amount"], item["reason"])
            for item in raw["payments"]
        ],
        parked_after=raw["parked_after"],
        extra_pocket=raw["extra_pocket"],
        lexus_uah=raw["lexus_uah"],
        lump_title=raw.get("lump_title"),
        lump_gap=raw.get("lump_gap", 0),
        budget_short=bool(raw.get("budget_short")),
        mode=raw.get("mode", "debt"),
    )


SPRINT_START = date(2026, 10, 9)


def target_payday(today: date, state: State) -> date:
    coming = next_friday(today)
    if today < SPRINT_START:
        return SPRINT_START
    latest = today if today.weekday() == 4 else coming - timedelta(days=7)
    if latest >= SPRINT_START and latest.isoformat() not in state.applied_paydays:
        return latest
    if coming.isoformat() in state.applied_paydays:
        return coming + timedelta(days=7)
    return coming


def project_weeks(state: State, start: date, count: int) -> list[tuple[Plan, State]]:
    cursor = _copy_state(state)
    day = start
    result = []
    for _ in range(count):
        payday = next_friday(day)
        plan = build_plan(cursor, payday)
        apply_plan(cursor, plan)
        result.append((plan, _copy_state(cursor)))
        day = payday + timedelta(days=1)
    return result


def money_snapshot(state: State) -> dict:
    return {
        "debts": {debt.id: f"{debt.balance:.2f}" for debt in state.debts},
        "parked_uah": f"{state.parked_uah:.2f}",
        "windfall_uah": f"{state.windfall_uah:.2f}",
        "installment_paid": dict(state.installment_paid),
        "applied_paydays": list(state.applied_paydays),
    }


def restore_snapshot(state: State, snap: dict) -> None:
    for debt in state.debts:
        if debt.id in snap["debts"]:
            debt.balance = money(snap["debts"][debt.id])
    state.parked_uah = money(snap["parked_uah"])
    state.windfall_uah = money(snap["windfall_uah"])
    state.installment_paid = dict(snap["installment_paid"])
    state.applied_paydays = list(snap["applied_paydays"])
