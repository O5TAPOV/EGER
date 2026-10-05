from datetime import date

from ration_bot.meals import blocked, day_menu, shopping
from ration_bot.store import State, add_hate, mark_trained, menu_for, reroll
from ration_bot.train import is_train_day, level_row, render_train


def test_mushrooms_drop_out_when_banned():
    menu = day_menu(date(2026, 10, 5), ["гриби"])
    for dish in menu.values():
        assert not blocked(dish, ["гриби"])
        assert "гриб" not in " ".join(dish.ingredients)


def test_onion_and_chicken_stems_are_enough():
    from ration_bot.meals import DISHES

    mushroom = next(dish for dish in DISHES if dish.id == "mushroom-buckwheat")
    assert blocked(mushroom, ["цибулю"])
    assert blocked(mushroom, ["печериці"])
    chicken = next(dish for dish in DISHES if dish.id == "chicken-cabbage")
    assert blocked(chicken, ["курку"])


def test_menu_sticks_and_next_changes_dinner():
    state = State()
    day = date(2026, 10, 5)
    first = menu_for(state, day)
    again = menu_for(state, day)
    assert first["dinner"].id == again["dinner"].id
    rolled = reroll(state, day, "dinner")
    assert rolled["dinner"].id != first["dinner"].id
    assert rolled["breakfast"].id == first["breakfast"].id


def test_shop_list_comes_from_the_plates():
    state = State()
    items = shopping(menu_for(state, date(2026, 10, 5)))
    assert items
    assert all(item.strip() for item in items)


def test_hate_resets_today_menu():
    state = State()
    day = date(2026, 10, 5)
    menu_for(state, day)
    add_hate(state, ["куряче філе"])
    menu = menu_for(state, day)
    for dish in menu.values():
        assert "куря" not in " ".join(dish.ingredients)


def test_every_dish_is_short():
    menu = day_menu(date(2026, 10, 6), [])
    for dish in menu.values():
        assert dish.minutes <= 15
        assert dish.steps


def test_training_starts_at_his_numbers_and_grows_after_four_sessions():
    rounds, jacks, burpees, pushups, squats, plank = level_row(0)
    assert (rounds, jacks, burpees, pushups, squats, plank) == (2, 30, 30, 7, 7, 30)
    text = render_train(0, done_at_level=0)
    assert "7" in text
    assert "30" in text
    state = State()
    marks = []
    for offset in range(4):
        marks.append(mark_trained(state, date(2026, 10, 5 + offset)))
    assert marks[-1] == "up"
    assert state.level == 1
    assert level_row(state.level)[0] == 3


def test_train_days_are_monday_wednesday_friday():
    assert is_train_day(date(2026, 10, 5), [0, 2, 4])
    assert not is_train_day(date(2026, 10, 6), [0, 2, 4])
