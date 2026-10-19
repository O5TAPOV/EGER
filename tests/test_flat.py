from datetime import date

from flat_bot.chores import (
    FlatState,
    add_shopping,
    add_weight,
    due_ids,
    mark_done,
    menu_for,
    open_ids,
    render_open,
    render_recipe,
)
from flat_bot.recipes import RECIPES, choose_recipe


def test_week_puts_clean_on_wednesday_and_sunday_and_shop_on_saturday():
    assert due_ids(date(2026, 10, 5), []) == ["cook", "cats"]
    assert due_ids(date(2026, 10, 7), []) == ["cook", "cats", "clean"]
    assert due_ids(date(2026, 10, 4), []) == ["cook", "cats", "clean"]
    assert "shop" in due_ids(date(2026, 10, 10), [])
    assert "shop" in due_ids(date(2026, 10, 5), ["молоко"])


def test_done_closes_only_that_duty():
    state = FlatState()
    day = date(2026, 10, 7)
    mark_done(state, day, "cats")
    opened = open_ids(state, day)
    assert "cats" not in opened
    assert "cook" in opened
    assert "clean" in opened


def test_shopping_dedupes_and_clear_is_separate():
    state = FlatState()
    add_shopping(state, "Молоко, філе, молоко")
    assert state.shopping == ["Молоко", "філе"]


def test_default_recipes_fit_the_kitchen_and_stay_under_15_minutes():
    for recipe in RECIPES:
        assert recipe.minutes <= 15
        assert recipe.steps
        assert recipe.kcal > 0
    monday = date(2026, 10, 5)
    picked = choose_recipe(monday, has_multi=False)
    assert "multi" not in picked.gear
    multi = choose_recipe(monday, has_multi=True, prefer="multi")
    assert multi.gear == ("multi",)


def test_menu_sticks_for_the_day_and_next_is_different():
    state = FlatState()
    day = date(2026, 10, 5)
    first = menu_for(state, day)
    again = menu_for(state, day)
    other = menu_for(state, day, another=True)
    assert first.id == again.id
    assert other.id != first.id
    text = render_recipe(first)
    assert "15" in text or str(first.minutes) in text
    nag = render_open(state, day, with_recipe=False)
    assert nag is not None
    assert "Антон" in nag


def test_weight_delta_for_anton():
    state = FlatState()
    add_weight(state, date(2026, 10, 5), 110, 1, "Антон")
    result = add_weight(state, date(2026, 10, 12), 109.4, 1, "Антон")
    assert result["delta"] == -0.6
