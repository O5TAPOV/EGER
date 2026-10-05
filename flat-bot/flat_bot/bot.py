from __future__ import annotations

import logging
import os
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

from telegram import ReplyKeyboardMarkup, Update
from telegram.ext import Application, CommandHandler, ContextTypes, MessageHandler, filters

from flat_bot.chores import (
    DUTY_NAMES,
    NAMES,
    FlatState,
    add_shopping,
    add_weight,
    bind_person,
    clear_shopping,
    duty_key,
    gear_key,
    load_state,
    mark_done,
    menu_for,
    open_ids,
    owner_name,
    person_key,
    render_duties,
    render_open,
    render_recipe,
    render_weight,
    save_state,
    who_is,
)

KYIV = ZoneInfo("Europe/Kyiv")
KEYBOARD = ReplyKeyboardMarkup(
    [["🍽 Рецепт", "📋 Справи"], ["✅ Зробив", "🛒 Список"]],
    resize_keyboard=True,
)
log = logging.getLogger("flat_bot")

BUTTONS = {
    "🍽 рецепт": "cook",
    "рецепт": "cook",
    "📋 справи": "duties",
    "справи": "duties",
    "✅ зробив": "done",
    "зробив": "done",
    "🛒 список": "list",
    "список": "list",
}


def state_path() -> Path:
    return Path(os.environ.get("FLAT_STATE", "data/flat.json"))


def ensure_state() -> FlatState:
    path = state_path()
    if not path.exists():
        save_state(path, FlatState())
    return load_state(path)


def _today():
    return datetime.now(KYIV).date()


def _name_for(state: FlatState, update: Update) -> str:
    user = update.effective_user
    user_id = user.id if user else None
    key = who_is(state, user_id)
    if key:
        return NAMES[key]
    if user and user.first_name:
        return user.first_name
    return "Хтось"


async def _reply(update: Update, text: str) -> None:
    if update.message is None:
        return
    chat = update.effective_chat
    markup = KEYBOARD if chat is not None and chat.type == "private" else None
    await update.message.reply_text(text, reply_markup=markup)


def _remember(state: FlatState, update: Update) -> None:
    chat = update.effective_chat
    if chat is not None and chat.type in {"group", "supergroup"}:
        state.chat_id = chat.id


def _args(update: Update, context: ContextTypes.DEFAULT_TYPE) -> list[str]:
    if context.args:
        return list(context.args)
    text = (update.message.text or "").strip()
    parts = text.split()
    if not parts:
        return []
    head = parts[0].casefold()
    if head.startswith("/") or head in BUTTONS or head in {"купити", "вага", "готово", "віддай"}:
        return parts[1:]
    return parts


async def start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    save_state(path, state)
    await _reply(
        update,
        "\n".join(
            [
                "🏠 Хата на трьох.",
                "Антон: кухня. Назар: прибирання і коти. Максим: закупки.",
                "Коти лишаються на Назарі, поки самі не перекинете.",
                "",
                "Кожен пише один раз: /me anton, /me nazar або /me maksym.",
                "/cook — вечеря до 15 хвилин руками.",
                "/done cook — закрив кухню. Так само cats, clean, shop.",
                "/buy молоко, філе — у список. /bought — купив.",
                "/weight 110 — вага вранці, раз на тиждень.",
                "/nag — хто сьогодні висить.",
            ]
        ),
    )


async def me_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    args = _args(update, context)
    if not args:
        await _reply(update, "Хто ти: /me anton, /me nazar або /me maksym")
        return
    key = person_key(args[0])
    user = update.effective_user
    if key is None or user is None:
        await _reply(update, "Пиши /me anton, /me nazar або /me maksym")
        return
    bind_person(state, key, user.id)
    save_state(path, state)
    await _reply(update, f"Запам'ятав: ти {NAMES[key]}.\n\n" + render_duties(state, _today()))


async def duties_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    save_state(path, state)
    await _reply(update, render_duties(state, _today()))


async def done_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    today = _today()
    args = _args(update, context)
    if args:
        duty_id = duty_key(args[0])
        if duty_id is None:
            await _reply(update, "Не бачу справу. /done cook, /done cats, /done clean або /done shop")
            return
        targets = [duty_id]
    else:
        user = update.effective_user
        key = who_is(state, user.id if user else None)
        if key is None:
            await _reply(update, "Напиши /me anton, або закрий конкретно: /done cook")
            return
        targets = [duty_id for duty_id in open_ids(state, today) if state.duty_owner.get(duty_id) == key]
        if not targets:
            await _reply(update, "На тобі сьогодні нічого відкритого.\n\n" + render_duties(state, today))
            return
    for duty_id in targets:
        mark_done(state, today, duty_id)
    save_state(path, state)
    closed = ", ".join(f"{DUTY_NAMES[duty_id]} ({owner_name(state, duty_id)})" for duty_id in targets)
    await _reply(update, f"✅ Закрив: {closed}\n\n" + render_duties(state, today))


async def nag_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    today = _today()
    text = render_open(state, today, with_recipe=True)
    save_state(path, state)
    await _reply(update, text or "✅ Сьогодні все закрито. Можна не вставати.")


async def cook_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    args = _args(update, context)
    prefer = None
    another = False
    if args:
        token = args[0].casefold()
        if token in {"next", "інше", "інший", "ще"}:
            another = True
        else:
            prefer = gear_key(token)
            if prefer is None:
                await _reply(update, "Можна /cook, /cook next, /cook air, /cook pan, /cook pot, /cook micro")
                return
            if prefer == "multi" and not state.has_multi:
                await _reply(update, "Мультиварки в рецептах ще немає. Коли купиш: /multi on")
                return
    recipe = menu_for(state, _today(), prefer=prefer, another=another)
    save_state(path, state)
    extra = ""
    if not state.has_multi:
        extra = "\n\nАерогриль уже замінює духовку на курку, рибу, картоплю й овочі. Мультиварка потрібна на гречку і суп, які вариш і йдеш: /multi on"
    await _reply(update, render_recipe(recipe) + extra)


async def buy_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    raw = " ".join(_args(update, context))
    if not raw:
        await _reply(update, "Приклад: /buy молоко, куряче філе, гречка")
        return
    add_shopping(state, raw)
    save_state(path, state)
    await _reply(update, "🛒 Список: " + ", ".join(state.shopping))


async def list_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    save_state(path, state)
    if not state.shopping:
        await _reply(update, "🛒 Список порожній.")
        return
    await _reply(update, "🛒 Купити: " + ", ".join(state.shopping) + f"\nЦе на {owner_name(state, 'shop')}.")


async def bought_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    today = _today()
    clear_shopping(state)
    mark_done(state, today, "shop")
    save_state(path, state)
    await _reply(update, f"✅ {owner_name(state, 'shop')} зходив. Список чистий.")


async def weight_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    name = _name_for(state, update)
    args = _args(update, context)
    if not args:
        save_state(path, state)
        await _reply(update, render_weight(state, name))
        return
    try:
        kg = float(args[0].replace(",", "."))
    except ValueError:
        await _reply(update, "Приклад: /weight 109.4")
        return
    if kg < 40 or kg > 250:
        await _reply(update, "Ця цифра не схожа на вагу. Приклад: /weight 109.4")
        return
    user = update.effective_user
    add_weight(state, _today(), kg, user.id if user else None, name)
    save_state(path, state)
    await _reply(update, render_weight(state, name))


async def multi_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    args = _args(update, context)
    if not args:
        state_text = "вже в рецептах" if state.has_multi else "ще вимкнена"
        await _reply(update, f"Мультиварка {state_text}. /multi on або /multi off")
        return
    token = args[0].casefold()
    if token in {"on", "так", "є", "yes"}:
        state.has_multi = True
    elif token in {"off", "ні", "нема", "no"}:
        state.has_multi = False
    else:
        await _reply(update, "/multi on коли купиш, /multi off якщо ще немає")
        return
    save_state(path, state)
    if state.has_multi:
        await _reply(update, "Мультиварка ввімкнена. /cook multi дасть те, що можна кинути і піти.")
    else:
        await _reply(update, "Без мультиварки. Лишаються аерогриль, сковорода, каструля і мікрохвильовка.")


async def give_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    _remember(state, update)
    args = _args(update, context)
    if len(args) < 2:
        await _reply(update, "Приклад: /give shop maksym або /give cats nazar")
        return
    duty_id = duty_key(args[0])
    key = person_key(args[1])
    if duty_id is None or key is None:
        await _reply(update, "Справи: cook, cats, clean, shop. Люди: anton, nazar, maksym.")
        return
    state.duty_owner[duty_id] = key
    save_state(path, state)
    await _reply(update, render_duties(state, _today()))


async def buttons(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    text = (update.message.text or "").strip()
    head = text.split()[0].casefold() if text else ""
    action = BUTTONS.get(text.casefold()) or BUTTONS.get(head)
    if action == "cook" or head in {"рецепт", "/cook"}:
        await cook_cmd(update, context)
    elif action == "duties" or head in {"справи", "хто"}:
        await duties_cmd(update, context)
    elif action == "done" or head in {"готово", "зробив"}:
        await done_cmd(update, context)
    elif action == "list" or head == "список":
        await list_cmd(update, context)
    elif head == "купити":
        await buy_cmd(update, context)
    elif head == "вага":
        await weight_cmd(update, context)
    elif head == "віддай":
        await give_cmd(update, context)


async def _send(context: ContextTypes.DEFAULT_TYPE, text: str | None) -> None:
    if not text:
        return
    state = ensure_state()
    if not state.chat_id:
        log.info("немає чату хати, нагадування пропущено")
        return
    await context.bot.send_message(chat_id=state.chat_id, text=text)


async def job_dinner(context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    text = render_open(state, _today(), with_recipe=True)
    save_state(path, state)
    await _send(context, text)


async def job_nag(context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    text = render_open(state, _today(), with_recipe=False)
    save_state(path, state)
    if text:
        text = "👁 Досі висить.\n\n" + text
    await _send(context, text)


def build_app(token: str) -> Application:
    app = Application.builder().token(token).build()
    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("me", me_cmd))
    app.add_handler(CommandHandler("duties", duties_cmd))
    app.add_handler(CommandHandler("done", done_cmd))
    app.add_handler(CommandHandler("nag", nag_cmd))
    app.add_handler(CommandHandler("cook", cook_cmd))
    app.add_handler(CommandHandler("buy", buy_cmd))
    app.add_handler(CommandHandler("list", list_cmd))
    app.add_handler(CommandHandler("bought", bought_cmd))
    app.add_handler(CommandHandler("weight", weight_cmd))
    app.add_handler(CommandHandler("multi", multi_cmd))
    app.add_handler(CommandHandler("give", give_cmd))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, buttons))
    if app.job_queue is not None:
        from datetime import time

        app.job_queue.run_daily(job_dinner, time=time(17, 0, tzinfo=KYIV))
        app.job_queue.run_daily(job_nag, time=time(21, 0, tzinfo=KYIV))
    return app
