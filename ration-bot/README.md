# Раціон

Особистий бот. Папка на сервері: `~/ration-bot`. До хати і до кредиту він не підключається.

Вранці о 10:00 кидає сніданок, обід, вечерю, список покупок і трену, якщо сьогодні коло. Гриби і будь-що інше викидаються командою `/hate`.

```bash
cp -a ~/payday-bot/ration-bot ~/ration-bot
cd ~/ration-bot
cp .env.example .env
```

У `.env` тільки `RATION_BOT_TOKEN` від третього бота в BotFather.

```bash
docker compose up -d --build
```

У приватному чаті з ботом: `/start`.
