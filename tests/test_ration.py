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


def test_every_dish_is_short_and_step_by_step():
    from ration_bot.meals import DISHES

    for dish in DISHES:
        assert dish.minutes <= 15
        assert len(dish.steps) >= 4


def test_he_gets_one_meal_and_a_roast_when_he_sins():
    from ration_bot.talk import intent, roast_cheat, roast_weight, slot_for_hour

    assert intent("бля, я тута в макові щас хаваю, ізвінітісь") == "cheat"
    assert intent("проснувся, нада поснідати") == "breakfast"
    assert intent("чота жрать хочу, чо здєлать?") == "hungry"
    assert intent("якщо є вільні бабки, шо за імба") == "money"
    assert slot_for_hour(9) == "breakfast"
    assert slot_for_hour(13) == "lunch"
    assert slot_for_hour(19) == "dinner"
    text = roast_weight(111.6)
    assert "111.6" in text
    assert "100" in text
    assert "Мак" in roast_cheat()


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
