import logging
import os
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

from telegram import ReplyKeyboardMarkup, Update
from telegram.ext import Application, CommandHandler, ContextTypes, MessageHandler, filters

from payday_bot.model import load_state, save_state
from payday_bot.money import fmt, money
from payday_bot.plan import (
    apply_plan,
    build_plan,
    money_snapshot,
    plan_from_json,
    plan_to_json,
    restore_snapshot,
    target_payday,
)
from payday_bot.render import (
    render_balances,
    render_done,
    render_friday,
    render_rates,
    render_hold,
    render_lexus,
    render_plan,
)
from payday_bot.xchange import fetch_usd
from payday_bot.seed import find_debt, initial_state, parse_amount

KYIV = ZoneInfo("Europe/Kyiv")
KEYBOARD = ReplyKeyboardMarkup(
    [["📅 План", "💳 Борги"], ["✅ Готово", "🚗 Лексус"]],
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


def refresh_rate(state) -> str:
    try:
        parsed = fetch_usd()
    except Exception:
        parsed = None
    if not parsed:
        return "⚠️ Курс X-Change зараз не відкрився. Рахую по останньому збереженому."
    buy, sell = parsed
    state.xchange_buy = buy
    state.xchange_sell = sell
    return ""


def button_name(text: str) -> str:
    folded = text.casefold()
    for name in ("план", "борги", "готово", "лексус"):
        if name in folded:
            return name
    return ""


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
    refresh_rate(state)
    _remember(state, today)
    save_state(path, state)
    await _reply(update, render_plan(state, today))


async def debts_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    state = ensure_state()
    if not _allowed(update, state):
        return
    await _reply(update, render_balances(state))


async def lexus_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    note = refresh_rate(state)
    save_state(path, state)
    await _reply(update, render_lexus(state, note))


async def done_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not state.last_plan:
        await _reply(update, "Спочатку відкрий /plan, потім оплати це в банку і напиши /done.")
        return
    plan = plan_from_json(state.last_plan)
    if plan.payday.isoformat() in state.applied_paydays:
        await _reply(update, f"П'ятниця {plan.payday.strftime('%d.%m')} вже записана. Наступний план: /plan")
        return
    snapshot = money_snapshot(state)
    apply_plan(state, plan)
    state.undo = list(state.undo or [])[-2:]
    state.undo.append(snapshot)
    save_state(path, state)
    hanging = [debt.title for debt in state.debts if debt.settle == "asap" and debt.balance > 0]
    extra = ""
    if hanging:
        extra = "\n\n⏰ Ще висить: " + ", ".join(hanging) + ". Це не входило в п'ятничний план, закрий окремо."
    await _reply(update, render_done(plan) + extra + "\n\n" + render_balances(state))


async def saved_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        await _reply(update, "Скільки доларів є зараз: /saved 150. Докинув ще: /saved +40")
        return
    raw = context.args[0]
    adding = raw.startswith("+")
    try:
        amount = parse_amount(raw[1:] if adding else raw)
    except Exception:
        await _reply(update, "Не бачу суму. Приклад: /saved 150 або /saved +40")
        return
    if amount < 0 or (adding and amount == 0):
        await _reply(update, "Сума має бути більша за нуль.")
        return
    if adding:
        state.lexus_saved_usd = money(state.lexus_saved_usd + amount)
    else:
        state.lexus_saved_usd = amount
    note = refresh_rate(state)
    save_state(path, state)
    await _reply(update, render_lexus(state, note))


async def parents_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not context.args:
        await _reply(update, "Скільки гривень закинув батькам: /parents 10000. Поставити всю суму: /parents =20000")
        return
    raw = context.args[0]
    setting = raw.startswith("=")
    try:
        amount = parse_amount(raw[1:] if setting else raw)
    except Exception:
        await _reply(update, "Не бачу суму. Приклад: /parents 10000")
        return
    if amount < 0:
        await _reply(update, "Сума не може бути менша за нуль.")
        return
    if setting:
        state.parents_held_uah = amount
    else:
        state.parents_held_uah = money(state.parents_held_uah + amount)
    note = refresh_rate(state)
    save_state(path, state)
    await _reply(update, render_lexus(state, note))


async def undo_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if not state.undo:
        await _reply(update, "Повертати нічого. /done ще не записував оплату.")
        return
    restore_snapshot(state, state.undo.pop())
    save_state(path, state)
    await _reply(update, "↩️ Повернув залишки як до останнього /done.\n\n" + render_balances(state))


async def owe_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    if not _allowed(update, state):
        return
    if len(context.args) < 2:
        await _reply(update, "Приклад: /owe Ватіля 3500")
        return
    try:
        amount = parse_amount(context.args[-1])
    except Exception:
        await _reply(update, "Не бачу суму. Приклад: /owe Ватіля 3500")
        return
    if amount <= 0:
        await _reply(update, "Сума має бути більша за нуль.")
        return
    title = " ".join(context.args[:-1]).strip()
    debt_id = "".join(ch for ch in title.casefold() if ch.isalnum())
    if not title or not debt_id:
        await _reply(update, "Приклад: /owe Ватіля 3500")
        return
    from payday_bot.model import Debt

    existing = find_debt(debt_id, state)
    if existing is None:
        state.debts.append(Debt(debt_id, title, amount, "manual"))
    else:
        existing.balance = amount
        existing.settle = "manual"
    save_state(path, state)
    await _reply(
        update,
        f"Записав: {title} {fmt(amount)} грн. П'ятничний план це не чіпає, віддаєш сам. Закрив: /set {debt_id} 0\n\n"
        + render_balances(state),
    )


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
        await _reply(update, render_rates(state) + "\nПоки сайт мовчить, можна вписати обидва курси однаково: /rate 45.10")
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
    state.xchange_buy = rate
    state.xchange_sell = rate
    save_state(path, state)
    await _reply(update, f"Поки що обидва курси {fmt(rate)}. Наступне відкриття плану знову візьме X-Change.\n\n" + render_plan(state, _today()))


async def buttons(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    name = button_name(update.message.text or "")
    if name == "план":
        await plan_cmd(update, context)
    elif name == "борги":
        await debts_cmd(update, context)
    elif name == "готово":
        await done_cmd(update, context)
    elif name == "лексус":
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
    refresh_rate(state)
    _remember(state, today)
    save_state(path, state)
    await _send_owner(context, render_plan(state, today))


async def job_hold(context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    refresh_rate(state)
    save_state(path, state)
    await _send_owner(context, render_hold(state, _today()))


async def job_rate(context: ContextTypes.DEFAULT_TYPE) -> None:
    path = state_path()
    state = ensure_state()
    note = refresh_rate(state)
    save_state(path, state)
    log.info(note or f"X-Change {state.xchange_buy}/{state.xchange_sell}")


def build_app(token: str) -> Application:
    app = Application.builder().token(token).build()
    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("plan", plan_cmd))
    app.add_handler(CommandHandler("debts", debts_cmd))
    app.add_handler(CommandHandler("done", done_cmd))
    app.add_handler(CommandHandler("lexus", lexus_cmd))
    app.add_handler(CommandHandler("saved", saved_cmd))
    app.add_handler(CommandHandler("parents", parents_cmd))
    app.add_handler(CommandHandler("undo", undo_cmd))
    app.add_handler(CommandHandler("set", set_cmd))
    app.add_handler(CommandHandler("owe", owe_cmd))
    app.add_handler(CommandHandler("got", got_cmd))
    app.add_handler(CommandHandler("rate", rate_cmd))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, buttons))
    if app.job_queue is not None:
        from datetime import time

        app.job_queue.run_daily(job_plan, time=time(19, 0, tzinfo=KYIV), days=(4,))
        app.job_queue.run_daily(job_hold, time=time(12, 0, tzinfo=KYIV), days=(0,))
        app.job_queue.run_daily(job_rate, time=time(10, 0, tzinfo=KYIV))
        app.job_queue.run_once(job_rate, when=15)
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
        chunks.append(render_friday(plan, state))
        chunks.append("")
    return "\n".join(chunks).strip()
