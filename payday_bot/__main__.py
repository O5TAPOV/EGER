import logging
import os
import sys

from payday_bot.bot import build_app, preview


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    if "--preview" in sys.argv:
        print(preview())
        return
    token = os.environ.get("TELEGRAM_BOT_TOKEN")
    if not token:
        raise SystemExit("Потрібен TELEGRAM_BOT_TOKEN від BotFather. Подивитись план без бота: python -m payday_bot --preview")
    build_app(token).run_polling()


if __name__ == "__main__":
    main()
