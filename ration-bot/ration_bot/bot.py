from __future__ import annotations

import os
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

from telegram import BotCommand, Update
from telegram.ext import Application, CommandHandler, ContextTypes, MessageHandler, filters

from ration_bot.meals import SLOTS, budget_line, render_dish, render_menu
from ration_bot.talk import intent, meal_call, next_buy, roast_cheat, roast_scam, roast_weight, slot_for_hour
from ration_bot.store import (
    State,
    add_hate,
    add_weight,
    load_state,
    mark_owned,
    mark_trained,
    menu_for,
    remove_hate,
    reroll,
    save_state,
    shop_lines,
)
from ration_bot.train import DAY_ALIASES, DAY_NAMES, is_train_day, render_rest, render_train

KYIV = ZoneInfo("Europe/Kyiv")
COMMANDS = (
    BotCommand("start", "Що вміє цей бот"),
    BotCommand("menu", "Тарілки на день і норма ккал"),
    BotCommand("shop", "Що купити під сьогодні"),
    BotCommand("hate", "Прибрати продукт з тарілок"),
    BotCommand("unhate", "Повернути продукт"),
    BotCommand("next", "Інша страва на цей прийом"),
    BotCommand("train", "Сьогоднішнє коло"),
    BotCommand("done", "Коло закрив"),
    BotCommand("weight", "Записати ранкову вагу"),
    BotCommand("days", "Дні трені"),
    BotCommand("got", "Креатин, протеїн або доріжка вже є"),
)
SLOT_WORDS = {
    "breakfast": "breakfast",
    "сніданок": "breakfast",
    "lunch": "lunch",
    "обід": "lunch",
    "dinner": "dinner",
    "вечеря": "dinner",
}


def state_path() -> Path:
    return Path(os.environ.get("RATION_STATE", "data/ration.json"))


def ensure_state() -> State:
    path = state_path()
    if not path.exists():
        save_state(path, State())
    return load_state(path)


def _today():
    return datetime.now(KYIV).date()


def _allowed(update: Update, state: State) -> bool:
    user = update.effective_user
    chat = update.effective_chat
    if user is None or chat is None or chat.type != "private":
        return False
    if state.owner_id is None:
        state.owner_id = user.id
        state.chat_id = chat.id
        return True
    return user.id == state.owner_id


async def _reply(update: Update, text: str) -> None:
    if update.message:
        await update.message.reply_text(text)


def _split_words(args: list[str]) -> list[str]:
    raw = " ".join(args).replace(";", ",")
    return [" ".join(part.split()) for part in raw.split(",") if part.strip()]


async def start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    save_state(path, state)
    await _reply(
        update,
        "\n".join(
            [
                "Це твій бот. Хата і зарплата його не чують.",
                "Не вивалюю весь день зранку. Пиши як є:",
                "«проснувся, треба поснідати», «жрать хочу», «я в макові, ізвінітісь».",
                "",
                "/hate гриби — цей продукт більше не чіпаю",
                "/next dinner — інша вечеря",
                "/shop — що купити під сьогодні",
                "/train — сет, /done — коло закрив",
                "/weight 111.6 — вага раз на тиждень",
                "/got creatine — коли креатин уже в хаті",
            ]
        ),
    )


async def menu_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    text = render_menu(menu_for(state, _today()), state.hates, state.kg)
    save_state(path, state)
    await _reply(update, text)


async def shop_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    items = shop_lines(state, _today())
    save_state(path, state)
    await _reply(update, "🛒 Під сьогодні:\n" + "\n".join(f"• {item}" for item in items))


async def hate_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    words = _split_words(context.args)
    if not words:
        listed = ", ".join(state.hates) if state.hates else "поки нічого"
        await _reply(update, f"Не їси: {listed}.\nДодати: /hate гриби, цибуля")
        return
    add_hate(state, words)
    save_state(path, state)
    await _reply(update, "Записав. Цього в тарілці більше не буде: " + ", ".join(words) + ".")


async def unhate_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        await _reply(update, "Приклад: /unhate гриби")
        return
    remove_hate(state, " ".join(context.args))
    save_state(path, state)
    await _reply(update, render_menu(menu_for(state, _today()), state.hates, state.kg))


async def next_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    token = context.args[0].casefold() if context.args else "dinner"
    slot = SLOT_WORDS.get(token)
    if slot not in SLOTS:
        await _reply(update, "/next breakfast, /next lunch або /next dinner")
        return
    menu = reroll(state, _today(), slot)
    save_state(path, state)
    await _reply(update, _one(state, menu[slot], slot))


async def train_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    today = _today()
    if is_train_day(today, state.train_days):
        text = render_train(state.level, done_at_level=state.done_at_level)
    else:
        text = render_rest()
    save_state(path, state)
    await _reply(update, text)


async def done_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    result = mark_trained(state, _today())
    save_state(path, state)
    if result == "already":
        await _reply(update, "Сьогоднішнє коло вже закрите.")
        return
    if result == "up":
        await _reply(update, "Чотири трені на цьому кроці є. Наступного разу буде важче.\n\n" + render_train(state.level, done_at_level=state.done_at_level))
        return
    await _reply(update, "Записав коло.\n\n" + render_train(state.level, done_at_level=state.done_at_level))


async def weight_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        if not state.weights:
            await _reply(update, "Ще немає цифри. Вранці: /weight 110")
            return
        last = state.weights[-1]
        await _reply(update, f"Остання вага: {last['kg']} кг ({last['date'][8:10]}.{last['date'][5:7]}).")
        return
    try:
        kg = float(context.args[0].replace(",", "."))
    except ValueError:
        await _reply(update, "Приклад: /weight 109.4")
        return
    if kg < 40 or kg > 250:
        await _reply(update, "Це не схоже на вагу. Приклад: /weight 109.4")
        return
    result = add_weight(state, _today(), kg)
    save_state(path, state)
    extra = ""
    if result["delta"] is not None:
        sign = "+" if result["delta"] > 0 else ""
        extra = f"\nВід минулого разу: {sign}{result['delta']} кг."
    await _reply(
        update,
        roast_weight(result["row"]["kg"]) + extra + "\n\n" + budget_line(result["row"]["kg"]) + "\n\n" + next_buy(state.owned),
    )


async def days_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        names = ", ".join(DAY_NAMES[day] for day in state.train_days)
        await _reply(update, f"Треня: {names}. Змінити: /days mon wed fri")
        return
    picked = []
    for token in context.args:
        day = DAY_ALIASES.get(token.casefold().strip(".,"))
        if day is None or day in picked:
            continue
        picked.append(day)
    if not picked:
        await _reply(update, "Не бачу днів. Приклад: /days mon wed fri")
        return
    state.train_days = picked
    save_state(path, state)
    names = ", ".join(DAY_NAMES[day] for day in picked)
    await _reply(update, f"Треня тепер: {names}.")


def _one(state: State, dish, slot: str) -> str:
    return meal_call(slot, state.kg) + "\n\n" + render_dish(slot, dish, kg=state.kg) + f"\n\nІнша: /next {slot}"


async def got_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    token = (context.args[0].casefold() if context.args else "").strip()
    aliases = {"креатин": "creatine", "протеїн": "protein", "протеин": "protein", "доріжка": "walkpad", "пад": "walkpad"}
    key = aliases.get(token, token)
    if key not in {"creatine", "protein", "walkpad"}:
        await _reply(update, "Приймаю /got creatine, /got protein або /got walkpad.")
        return
    mark_owned(state, key)
    save_state(path, state)
    await _reply(update, "Ок, це вже є.\n\n" + next_buy(state.owned))


async def talk(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    text = update.message.text or ""
    kind = intent(text)
    today = _today()
    hour = datetime.now(KYIV).hour
    if kind == "cheat":
        save_state(path, state)
        await _reply(update, roast_cheat())
        return
    if kind == "scam":
        save_state(path, state)
        await _reply(update, roast_scam())
        return
    if kind == "money":
        save_state(path, state)
        await _reply(update, next_buy(state.owned))
        return
    if kind == "breakfast":
        slot = "breakfast"
    elif kind == "hungry":
        slot = slot_for_hour(hour)
    else:
        await _reply(update, "Не в'їхав. Напиши, що проснувся, що жрать хочеш, або що згрішив у макові.")
        return
    dish = menu_for(state, today)[slot]
    save_state(path, state)
    await _reply(update, _one(state, dish, slot))


async def _publish_commands(app: Application) -> None:
    await app.bot.set_my_commands(list(COMMANDS))


def build_app(token: str) -> Application:
    app = Application.builder().token(token).post_init(_publish_commands).build()
    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("menu", menu_cmd))
    app.add_handler(CommandHandler("shop", shop_cmd))
    app.add_handler(CommandHandler("hate", hate_cmd))
    app.add_handler(CommandHandler("unhate", unhate_cmd))
    app.add_handler(CommandHandler("next", next_cmd))
    app.add_handler(CommandHandler("train", train_cmd))
    app.add_handler(CommandHandler("done", done_cmd))
    app.add_handler(CommandHandler("weight", weight_cmd))
    app.add_handler(CommandHandler("days", days_cmd))
    app.add_handler(CommandHandler("got", got_cmd))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, talk))
    return app
