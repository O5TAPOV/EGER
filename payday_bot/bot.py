import logging
import os
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

from telegram import ReplyKeyboardMarkup, Update
from telegram.ext import Application, CommandHandler, ContextTypes, MessageHandler, filters

from payday_bot.model import load_state, save_state
from payday_bot.money import fmt, money
from payday_bot.plan import apply_plan, build_plan, plan_from_json, plan_to_json, target_payday
from payday_bot.render import render_balances, render_friday, render_hold, render_lexus, render_plan
from payday_bot.seed import find_debt, initial_state, parse_amount

KYIV = ZoneInfo("Europe/Kyiv")
KEYBOARD = ReplyKeyboardMarkup(
    [["План", "Борги"], ["Готово", "Лексус"]],
    resize_keyboard=True,
)
log = logging.getLogger("payday_bot")


def state_path() -> Path:
    return Path(os.environ.get("PAYDAY_STATE", "data/state.json"))


def ensure_state():
    path = state_path()
    if not path.exists():
        save_state(path, initial_state())
    return load_state(path)


def _today():
    return datetime.now(KYIV).date()


def _remember(state, today):
    payday = target_payday(today, state)
    drop = payday if today < payday else None
    plan = build_plan(state, payday, drop_dues_before=drop)
    state.last_plan = plan_to_json(plan)
    return plan


async def _reply(update: Update, text: str) -> None:
    if update.message:
        await update.message.reply_text(text, reply_markup=KEYBOARD)


def _allowed(update: Update, state) -> bool:
    chat = update.effective_chat
    user = update.effective_user
    if chat is None or user is None or chat.type != "private":
        return False
    if state.owner_chat_id is None:
        return True
    return user.id == state.owner_chat_id


async def start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if state.owner_chat_id is None and update.effective_user:
        state.owner_chat_id = update.effective_user.id
        save_state(path, state)
    today = _today()
    _remember(state, today)
    save_state(path, state)
    text = (
        "Я пишу двічі на тиждень: у четвер о 19:00 план на п'ятницю, "
        "у неділю о 12:00 щоб гроші на борг не зникли до зарплати.\n\n"
        + render_plan(state, today)
    )
    await _reply(update, text)


async def plan_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    today = _today()
    _remember(state, today)
    save_state(path, state)
    await _reply(update, render_plan(state, today))


async def debts_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    state = ensure_state()
    if not _allowed(update, state):
        return
    await _reply(update, render_balances(state))


async def lexus_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    state = ensure_state()
    if not _allowed(update, state):
        return
    await _reply(update, render_lexus(state))


async def done_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not state.last_plan:
        await _reply(update, "Спочатку відкрий /plan, потім оплати це в банку і напиши /done.")
        return
    plan = plan_from_json(state.last_plan)
    if not apply_plan(state, plan):
        await _reply(update, f"П'ятниця {plan.payday.strftime('%d.%m')} вже записана. Наступний план: /plan")
        return
    save_state(path, state)
    hanging = [debt.title for debt in state.debts if debt.settle == "asap" and debt.balance > 0]
    extra = ""
    if hanging:
        extra = "\n\nЩе висить: " + ", ".join(hanging) + ". Це не входило в п'ятничний план, закрий окремо."
    await _reply(update, "Записав оплату.\n\n" + render_balances(state) + extra)


async def saved_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        await _reply(update, "Напиши суму в доларах, яку вже купив. Приклад: /saved 90")
        return
    try:
        amount = parse_amount(context.args[0])
    except Exception:
        await _reply(update, "Не бачу суму. Приклад: /saved 90")
        return
    if amount <= 0:
        await _reply(update, "Сума має бути більша за нуль.")
        return
    state.lexus_saved_usd = money(state.lexus_saved_usd + amount)
    save_state(path, state)
    await _reply(update, render_lexus(state))


async def set_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if len(context.args) < 2:
        await _reply(update, "Приклад: /set ліміт 8000")
        return
    debt = find_debt(context.args[0], state)
    if debt is None:
        await _reply(update, "Не знаю такого боргу. Є ліміт, телефон, екофлоу, мишка, завтра.")
        return
    try:
        amount = parse_amount(context.args[1])
    except Exception:
        await _reply(update, "Не бачу суму. Приклад: /set ліміт 8000")
        return
    if amount < 0:
        await _reply(update, "Сума не може бути менша за нуль.")
        return
    debt.balance = amount
    save_state(path, state)
    await _reply(update, render_balances(state))


async def got_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        await _reply(update, "Скільки тобі повернули? Приклад: /got 800")
        return
    try:
        amount = parse_amount(context.args[0])
    except Exception:
        await _reply(update, "Не бачу суму. Приклад: /got 800")
        return
    for item in state.receivables:
        if item.amount >= amount:
            item.amount = money(item.amount - amount)
            break
    state.windfall_uah = money(state.windfall_uah + amount)
    save_state(path, state)
    await _reply(update, f"Додав {fmt(amount)} грн до наступної п'ятниці.\n\n" + render_balances(state))


async def rate_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        await _reply(update, f"Зараз рахую долар по {fmt(state.usd_uah)} грн. Змінити: /rate 45.10")
        return
    try:
        rate = parse_amount(context.args[0])
    except Exception:
        await _reply(update, "Приклад: /rate 45.10")
        return
    if rate <= 0:
        await _reply(update, "Курс має бути більший за нуль.")
        return
    state.usd_uah = rate
    save_state(path, state)
    await _reply(update, f"Курс записав: {fmt(rate)} грн за долар.\n\n" + render_plan(state, _today()))


async def buttons(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    text = (update.message.text or "").strip().casefold()
    if text == "план":
        await plan_cmd(update, context)
    elif text == "борги":
        await debts_cmd(update, context)
    elif text == "готово":
        await done_cmd(update, context)
    elif text == "лексус":
        await lexus_cmd(update, context)


async def _send_owner(context: ContextTypes.DEFAULT_TYPE, text: str) -> None:
    state = ensure_state()
    if not state.owner_chat_id:
        log.info("немає власника, нагадування пропущено")
        return
    await context.bot.send_message(chat_id=state.owner_chat_id, text=text, reply_markup=KEYBOARD)


async def job_plan(context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    today = _today()
    _remember(state, today)
    save_state(path, state)
    await _send_owner(context, render_plan(state, today))


async def job_hold(context: ContextTypes.DEFAULT_TYPE) -> None:
    state = ensure_state()
    await _send_owner(context, render_hold(state, _today()))


def build_app(token: str) -> Application:
    app = Application.builder().token(token).build()
    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("plan", plan_cmd))
    app.add_handler(CommandHandler("debts", debts_cmd))
    app.add_handler(CommandHandler("done", done_cmd))
    app.add_handler(CommandHandler("lexus", lexus_cmd))
    app.add_handler(CommandHandler("saved", saved_cmd))
    app.add_handler(CommandHandler("set", set_cmd))
    app.add_handler(CommandHandler("got", got_cmd))
    app.add_handler(CommandHandler("rate", rate_cmd))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, buttons))
    if app.job_queue is not None:
        from datetime import time

        app.job_queue.run_daily(job_plan, time=time(19, 0, tzinfo=KYIV), days=(4,))
        app.job_queue.run_daily(job_hold, time=time(12, 0, tzinfo=KYIV), days=(0,))
    return app


def preview(weeks: int = 6) -> str:
    from payday_bot.model import State
    from payday_bot.money import money
    from payday_bot.plan import project_weeks

    state = ensure_state()
    today = _today()
    start = target_payday(today, state)
    chunks = [
        render_plan(state, today),
        "",
        "Далі по п'ятницях, якщо до п'ятниці закриєш те, у чого строк уже настав:",
        "",
    ]
    working = State.from_json(state.to_json())
    for debt in working.debts:
        if debt.settle == "asap" and debt.due is not None and debt.due < start:
            debt.balance = money(0)
    for plan, _ in project_weeks(working, start, weeks):
        chunks.append(render_friday(plan, state.usd_uah))
        chunks.append("")
    return "\n".join(chunks).strip()
