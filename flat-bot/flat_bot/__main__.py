import logging
import os
import sys
import time

from flat_bot.bot import build_app
from flat_bot.chores import preview


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    if "--preview" in sys.argv:
        print(preview())
        return
    token = os.environ.get("FLAT_BOT_TOKEN", "").strip()
    if not token or token.startswith("сюди"):
        logging.error("FLAT_BOT_TOKEN порожній. Другий бот у BotFather, токен у .env, потім docker compose up -d --force-recreate flat")
        while True:
            time.sleep(3600)
    build_app(token).run_polling()


if __name__ == "__main__":
    main()
