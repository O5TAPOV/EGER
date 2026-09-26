# EGER

Навчальна система оцінювання та звітності (Educational Grade Evaluation & Reporting).

Стек: ASP.NET Core 8, MongoDB 7, Redis, React (Vite), Nginx. Чотири сервіси в одній мережі `eger-net`: `eger-mongo`, `eger-redis`, `eger-backend`, `eger-frontend`.

## Запуск

Команда `docker` з’являється лише після встановлення [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/). На Windows під час інсталяції залиш WSL 2, запусти Docker Desktop і дочекайся статусу **Running**. Потім закрий термінал і відкрий новий, інакше PowerShell напише, що `docker` не розпізнано.

Запускати з кореня репозиторію — там, де лежить `docker-compose.yml`. Якщо цього файлу немає, ти не на гілці з застосунком:

```powershell
git fetch origin
git checkout cursor/eger-full-stack-81c7
copy .env.example .env
docker compose up --build
```

На macOS і Linux те саме, лише `cp .env.example .env` замість `copy`. Перший запуск довгий: тягнуться образи MongoDB, Redis, .NET і Node. Коли бекенд стане healthy, відкрий http://localhost:8080 і увійди як `admin@eger.ua` / `Admin123!`.

- Інтерфейс: http://localhost:8080
- Swagger: http://localhost:5080/swagger (також проксується з http://localhost:8080/swagger)
- Перевірка API: http://localhost:5080/health

Під час старту API створює індекси в базі `EgerDb` і обліковий запис адміністратора, якщо його ще немає.

## Облікові записи

| Роль | Пошта | Пароль |
| --- | --- | --- |
| Адміністратор | `admin@eger.ua` | `Admin123!` |
| Викладач (після генерації даних) | `professor@eger.ua` | `Professor123!` |
| Студент Остапов Антон (після генерації) | `anton.ostapov@eger.ua` | `Student123!` |

Кнопка «Згенерувати тестові дані» в кабінеті адміністратора додає українські кафедри, дисципліни (зокрема «Теорія баз даних»), ступені («Доцент») і оцінки. Повторний виклик не дублює набір.

## Змінні середовища

Їх читає і Compose, і API (пріоритет у змінних `Mongo__…` / `JWT_SECRET`).

| Змінна | Призначення | Локальне значення за замовчуванням |
| --- | --- | --- |
| `JWT_SECRET` | Секрет підпису JWT, щонайменше 32 байти | `eger-dev-only-change-me-please-32b!` |
| `TELEGRAM_BOT_TOKEN` | Токен бота від BotFather для кодів 2FA | порожньо |
| `MONGO_CONNECTION_STRING` | Рядок підключення MongoDB | `mongodb://localhost:27017` (у Compose — `mongodb://eger-mongo:27017`) |
| `MONGO_DATABASE` | Назва бази | `EgerDb` |
| `REDIS_CONNECTION_STRING` | Redis | `localhost:6379` (у Compose — `eger-redis:6379`) |
| `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` | Початковий адміністратор | `admin@eger.ua` / `Admin123!` |

`TELEGRAM_BOT_TOKEN` потрібен лише для двофакторного входу. Без нього звичайний вхід працює, а вхід із увімкненою 2FA повертає помилку. Код живе в Redis 5 хвилин за ключем `eger:2fa:{userId}`. Текст повідомлення: `Твій код авторизації в EGER: 123456`. Команда `/start` боту повертає ідентифікатор чату для прив’язки в кабінеті студента.

Сесії JWT також лежать у Redis (`eger:session:{userId}`), тож вихід з облікового запису одразу відкликає токен.

Паролі зберігаються як bcrypt-хеш. У відповідях API хеш не повертається.

## Локальна розробка

Потрібні MongoDB і Redis на localhost.

```bash
dotnet run --project backend/src/Eger.Api
cd frontend && npm install && npm run dev
```

Інтерфейс розробки: http://localhost:5173 (проксі `/api` на http://localhost:5080).

Оцінювання — 100-бальна шкала: поточні бали до 80 і підсумок (залік або екзамен) до 20. Головне число — сума. Літера ECTS і національна оцінка — додатково. Поріг зарахування, максимум поточних і максимум підсумкових зберігаються в Mongo і змінюються в кабінеті адміністратора на екрані «Система оцінювання» (типово 50 / 80 / 20).

Відомість дисципліни — це таблиця, як журнал у Google Sheets: рядок студента і колонка на кожне заняття. Клітинку редагують на місці.

Якщо демонстраційні оцінки вже були згенеровані зі старої шкали, після оновлення натисніть «Згенерувати тестові дані» ще раз: журнал перебудується, люди не дублюються.
