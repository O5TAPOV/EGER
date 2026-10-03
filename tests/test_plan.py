from datetime import date
from decimal import Decimal

from payday_bot.money import fmt, money
from payday_bot.plan import apply_plan, build_plan, project_weeks, target_payday
from payday_bot.seed import initial_state


def cleared_tomorrow():
    state = initial_state()
    state.debt("tomorrow").balance = money(0)
    return state


def pay(plan, debt_id):
    found = [item for item in plan.payments if item.debt_id == debt_id]
    assert len(found) == 1
    return found[0]


def test_four_fridays_clear_everything():
    state = cleared_tomorrow()
    weeks = project_weeks(state, date(2026, 10, 9), 4)

    first, after_first = weeks[0]
    assert first.payday == date(2026, 10, 9)
    assert first.debt_budget == money(11415)
    assert first.rent == 0
    assert first.parents == 0
    assert pay(first, "mouse").amount == money("1189.30")
    assert pay(first, "mouse").reason == "full"
    assert pay(first, "phone").amount == money("1599.90")
    assert pay(first, "phone").reason == "monthly"
    assert pay(first, "ecoflow").amount == money("1285.64")
    assert pay(first, "limit").amount == money("7340.16")
    assert after_first.debt("limit").balance == money("8068.73")
    assert after_first.debt("phone").balance == money("7999.50")
    assert after_first.parked_uah == 0

    second, after_second = weeks[1]
    assert pay(second, "limit").amount == money("8068.73")
    assert after_second.debt("limit").balance == 0
    assert after_second.parked_uah == money("3346.27")
    assert all(item.debt_id != "phone" for item in second.payments)

    third, after_third = weeks[2]
    assert pay(third, "phone").reason == "full"
    assert pay(third, "phone").amount == money("7999.50")
    assert after_third.debt("phone").balance == 0
    assert after_third.parked_uah == money("6761.77")
    assert third.lump_title == "Екофлоу"
    assert third.lump_gap == money("7380.31")
    assert all(item.reason != "flexible" or item.amount != money(5000) for item in third.payments)

    fourth, after_fourth = weeks[3]
    assert pay(fourth, "ecoflow").reason == "full"
    assert pay(fourth, "ecoflow").amount == money("14142.08")
    assert after_fourth.open_debts() == []
    assert after_fourth.parked_uah == 0
    assert fourth.extra_pocket == money("4034.69")


def test_no_partial_installment_while_limit_is_open():
    state = cleared_tomorrow()
    plan = build_plan(state, date(2026, 10, 9))
    phone = pay(plan, "phone")
    assert phone.reason == "monthly"
    assert phone.amount != money(5000)


def test_rent_and_parents_land_on_different_fridays():
    state = cleared_tomorrow()
    _, done = project_weeks(state, date(2026, 10, 9), 4)[-1]
    assert done.open_debts() == []
    rent_week = build_plan(done, date(2026, 11, 6))
    parents_week = build_plan(done, date(2026, 11, 13))
    assert rent_week.mode == "lexus"
    assert rent_week.rent == money(11000)
    assert rent_week.living == money(4000)
    assert rent_week.lexus_uah == money(7415)
    assert parents_week.parents == money(10000)
    assert parents_week.lexus_uah == money(1415)
    assert parents_week.rent == 0


def test_done_does_not_apply_twice():
    state = cleared_tomorrow()
    plan = build_plan(state, date(2026, 10, 9))
    assert apply_plan(state, plan) is True
    limit = state.debt("limit").balance
    assert apply_plan(state, plan) is False
    assert state.debt("limit").balance == limit


def test_before_sprint_targets_october_9():
    state = initial_state()
    assert target_payday(date(2026, 10, 3), state) == date(2026, 10, 9)
    assert target_payday(date(2026, 10, 8), state) == date(2026, 10, 9)


def test_money_format():
    assert fmt(Decimal("1189.30")) == "1 189,30"
    assert fmt(11415) == "11 415"
    assert fmt("8068.73") == "8 068,73"
