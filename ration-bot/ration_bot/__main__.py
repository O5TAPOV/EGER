import logging
import os
import sys
import time
from datetime import date

from ration_bot.bot import build_app
from ration_bot.store import State, menu_for
from ration_bot.meals import render_menu
from ration_bot.train import render_train


def preview() -> str:
    state = State(hates=["гриби"])
    day = date(2026, 10, 5)
    return render_menu(menu_for(state, day), state.hates) + "\n\n" + render_train(0, done_at_level=0)


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    if "--preview" in sys.argv:
        print(preview())
        return
    token = os.environ.get("RATION_BOT_TOKEN", "").strip()
    if not token or token.startswith("сюди"):
        logging.error("RATION_BOT_TOKEN порожній. Третій бот у BotFather, токен у .env цієї папки.")
        while True:
            time.sleep(3600)
    build_app(token).run_polling()


if __name__ == "__main__":
    main()
