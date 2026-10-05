from __future__ import annotations

import logging
import os
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

from telegram import Update
from telegram.ext import Application, CommandHandler, ContextTypes

from ration_bot.meals import SLOTS, render_menu
from ration_bot.store import (
    State,
    add_hate,
    add_weight,
    load_state,
    mark_trained,
    menu_for,
    remove_hate,
    reroll,
    save_state,
    shop_lines,
)
from ration_bot.train import DAY_ALIASES, DAY_NAMES, is_train_day, render_rest, render_train

KYIV = ZoneInfo("Europe/Kyiv")
log = logging.getLogger("ration_bot")
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
                "🍽 Це твій бот по їжі і трені. Хата і зарплата його не бачать.",
                "Вранці о 10:00 кине меню на день і трену, якщо сьогодні коло.",
                "",
                "/menu — що їсти",
                "/shop — що купити під це меню",
                "/hate гриби — більше не пропонувати",
                "/unhate гриби — повернути",
                "/next dinner — інша вечеря",
                "/train — сет на сьогодні",
                "/done — коло закрив",
                "/weight 110 — вага раз на тиждень",
            ]
        ),
    )


async def menu_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    text = render_menu(menu_for(state, _today()), state.hates)
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
    await _reply(update, "Прибрав з меню: " + ", ".join(words) + ".\n\n" + render_menu(menu_for(state, _today()), state.hates))


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
    await _reply(update, render_menu(menu_for(state, _today()), state.hates))


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
    await _reply(update, render_menu(menu, state.hates))


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
        extra = f"\nВід минулого разу: {sign}{result['delta']} кг"
    await _reply(update, f"Записав {result['row']['kg']} кг.{extra}\nРаз на тиждень, вранці, до води.")


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


def morning_text(state: State, day) -> str:
    parts = [render_menu(menu_for(state, day), state.hates), "", "🛒 " + ", ".join(shop_lines(state, day))]
    parts.append("")
    if is_train_day(day, state.train_days):
        parts.append(render_train(state.level, done_at_level=state.done_at_level))
    else:
        parts.append(render_rest())
    return "\n".join(parts)


async def job_morning(context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not state.chat_id:
        log.info("раціон ще ніхто не відкривав")
        return
    text = morning_text(state, _today())
    save_state(path, state)
    await context.bot.send_message(chat_id=state.chat_id, text=text)


def build_app(token: str) -> Application:
    app = Application.builder().token(token).build()
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
    if app.job_queue is not None:
        from datetime import time

        app.job_queue.run_daily(job_morning, time=time(10, 0, tzinfo=KYIV))
    return app
