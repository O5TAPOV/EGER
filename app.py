import hashlib
import hmac
import io
import logging
import os
import re
import secrets
import sqlite3
import subprocess
import time
from datetime import datetime
from pathlib import Path

from flask import (
    Flask,
    abort,
    g,
    redirect,
    render_template,
    request,
    send_from_directory,
    url_for,
)
from PIL import Image, ImageOps, UnidentifiedImageError
from werkzeug.exceptions import HTTPException, RequestEntityTooLarge
from zoneinfo import ZoneInfo

KYIV = ZoneInfo("Europe/Kyiv")
POW_ZEROS = 4

CHOICES = [
    ("dist", "Повністю дистанційно, поки тривоги такі часті"),
    ("mixed", "Лекції дистанційно, практика і лабораторні очно"),
    ("offline", "Лишати як зараз, очно"),
    ("unsure", "Не беруся сказати"),
]
FACULTIES = [
    "Не вказую",
    "Природничо-географічний",
    "Математики, фізики і комп'ютерних наук",
    "Історичний",
    "Права, публічного управління та адміністрування",
    "Іноземних мов",
    "Мистецтв і художньо-освітніх технологій",
    "Філології й журналістики ім. М. Стельмаха",
    "Дошкільної і початкової освіти ім. В. Волошиної",
    "Фізичного виховання і спорту",
    "ННІ педагогіки, психології, підготовки фахівців вищої кваліфікації",
]
COURSES = ["Не вказую", "1", "2", "3", "4", "5", "6", "магістратура"]
FORMS = ["Не вказую", "денна", "заочна", "викладач"]
CATEGORIES = [
    "Дорога, гроші, транспорт",
    "Ворота, скасована пара, пів години на дорогу",
    "Укриття",
    "Вода, світло, холод",
    "Як говорили про опитування",
    "Інше",
]

EMAIL_RE = re.compile(r"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", re.I)
PHONE_RE = re.compile(r"(?:\+?38[\s\-]?)?0[\s\-]?\d{2}[\s\-]?\d{3}[\s\-]?\d{2}[\s\-]?\d{2}")
HANDLE_RE = re.compile(r"(?:^|\s)@[A-Za-z0-9_]{4,}")
SOCIAL_RE = re.compile(
    r"(?:t\.me/|telegram\.me/|wa\.me/|instagram\.com/|facebook\.com/|fb\.com/|tiktok\.com/)",
    re.I,
)
HIDDEN_CHARS = dict.fromkeys(map(ord, "\u200b\u200c\u200d\ufeff\u202a\u202b\u202c\u202d\u202e\u2066\u2067\u2068\u2069"), None)


def today():
    return datetime.now(KYIV).strftime("%Y-%m-%d")


def create_app(config=None):
    app = Flask(__name__)
    base = Path(app.root_path)
    data = Path(os.environ.get("DATA_DIR", base / "data"))
    app.config.update(
        DATA_DIR=str(data),
        MAX_CONTENT_LENGTH=46 * 1024 * 1024,
        POW_ZEROS=POW_ZEROS,
        SESSION_COOKIE_HTTPONLY=True,
        SESSION_COOKIE_SAMESITE="Lax",
    )
    if config:
        app.config.update(config)
    data = Path(app.config["DATA_DIR"])
    data.mkdir(parents=True, exist_ok=True)
    (data / "uploads").mkdir(exist_ok=True)
    (data / "tmp").mkdir(exist_ok=True)
    app.config["DATA_DIR"] = str(data)

    logging.getLogger("werkzeug").setLevel(logging.ERROR)

    if os.environ.get("BEHIND_PROXY") == "1":
        from werkzeug.middleware.proxy_fix import ProxyFix
        app.wsgi_app = ProxyFix(app.wsgi_app, x_for=0, x_proto=1, x_host=1)

    def db():
        if "db" not in g:
            conn = sqlite3.connect(Path(app.config["DATA_DIR"]) / "site.db", timeout=15)
            conn.row_factory = sqlite3.Row
            conn.execute("PRAGMA journal_mode=WAL")
            conn.execute("PRAGMA foreign_keys=ON")
            g.db = conn
        return g.db

    @app.teardown_appcontext
    def close_db(_exc):
        conn = g.pop("db", None)
        if conn is not None:
            conn.close()

    def init_db():
        conn = sqlite3.connect(Path(app.config["DATA_DIR"]) / "site.db")
        conn.executescript(
            """
            CREATE TABLE IF NOT EXISTS votes (
              id INTEGER PRIMARY KEY,
              choice TEXT NOT NULL,
              faculty TEXT NOT NULL,
              course TEXT NOT NULL,
              form TEXT NOT NULL,
              day TEXT NOT NULL,
              ballot_hash TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS posts (
              id INTEGER PRIMARY KEY,
              category TEXT NOT NULL,
              faculty TEXT NOT NULL,
              course TEXT NOT NULL,
              body TEXT NOT NULL,
              day TEXT NOT NULL,
              delete_hash TEXT NOT NULL,
              hidden INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS media (
              id INTEGER PRIMARY KEY,
              post_id INTEGER NOT NULL REFERENCES posts(id),
              kind TEXT NOT NULL,
              filename TEXT NOT NULL,
              content_hash TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS challenges (
              challenge TEXT PRIMARY KEY,
              expires INTEGER NOT NULL,
              used INTEGER NOT NULL DEFAULT 0
            );
            """
        )
        conn.commit()
        conn.close()

    init_db()

    def issue_csrf():
        tok = request.cookies.get("csrf", "")
        if not re.fullmatch(r"[a-f0-9]{32}", tok):
            tok = secrets.token_hex(16)
            g.set_csrf = tok
        g.csrf = tok
        return tok

    def check_csrf():
        # Довга cookie була б міткою в журналі хостера, тож її немає.
        # Чужий сайт не прочитає одноразове завдання з цієї сторінки.
        # Якщо браузер прислав чужий Origin, запит відсікається.
        origin = request.headers.get("Origin", "")
        if origin and origin != "null":
            expected = request.host_url.rstrip("/")
            if origin != expected:
                abort(400)

    def new_challenge():
        now = int(time.time())
        c = secrets.token_hex(16)
        conn = db()
        conn.execute("DELETE FROM challenges WHERE expires < ?", (now,))
        conn.execute(
            "INSERT INTO challenges(challenge, expires, used) VALUES (?,?,0)",
            (c, now + 900),
        )
        conn.commit()
        return c

    def take_challenge(challenge, nonce):
        if not re.fullmatch(r"[a-f0-9]{32}", challenge or ""):
            return False
        if not re.fullmatch(r"[0-9a-f]{1,16}", nonce or ""):
            return False
        if not pow_ok(challenge, nonce, app.config["POW_ZEROS"]):
            return False
        now = int(time.time())
        conn = db()
        conn.execute("BEGIN IMMEDIATE")
        cur = conn.execute(
            "UPDATE challenges SET used = 1 WHERE challenge = ? AND used = 0 AND expires >= ?",
            (challenge, now),
        )
        ok = cur.rowcount == 1
        conn.commit()
        return ok

    def one_of(value, options):
        if value in options:
            return value
        return options[0]

    def tally():
        conn = db()
        rows = conn.execute("SELECT choice, COUNT(*) AS c FROM votes GROUP BY choice").fetchall()
        by = {r["choice"]: r["c"] for r in rows}
        total = sum(by.values())
        items = []
        for code, label in CHOICES:
            n = by.get(code, 0)
            items.append({
                "code": code,
                "label": label,
                "n": n,
                "pct": (100.0 * n / total) if total else 0.0,
            })
        fac_rows = conn.execute(
            "SELECT faculty, COUNT(*) AS c FROM votes GROUP BY faculty ORDER BY c DESC"
        ).fetchall()
        form_rows = conn.execute(
            "SELECT form, COUNT(*) AS c FROM votes GROUP BY form ORDER BY c DESC"
        ).fetchall()
        return {
            "total": total,
            "items": items,
            "faculties": [{"name": r["faculty"], "n": r["c"]} for r in fac_rows],
            "forms": [{"name": r["form"], "n": r["c"]} for r in form_rows],
            "audit": audit_hash(conn),
            "day": today(),
        }

    @app.context_processor
    def inject():
        return {
            "csrf": issue_csrf(),
            "today": today(),
            "choices": CHOICES,
            "faculties": FACULTIES,
            "courses": COURSES,
            "forms": FORMS,
            "categories": CATEGORIES,
        }

    @app.after_request
    def harden(resp):
        resp.headers["X-Content-Type-Options"] = "nosniff"
        resp.headers["X-Frame-Options"] = "DENY"
        resp.headers["Referrer-Policy"] = "no-referrer"
        resp.headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()"
        resp.headers["Content-Security-Policy"] = (
            "default-src 'self'; img-src 'self'; media-src 'self'; "
            "style-src 'self'; script-src 'self'; form-action 'self'; "
            "base-uri 'none'; frame-ancestors 'none'; object-src 'none'"
        )
        resp.headers["Cross-Origin-Resource-Policy"] = "same-origin"
        resp.headers["Cache-Control"] = "no-store"
        resp.headers.pop("Server", None)
        path = request.path
        if path.startswith("/p/") or path.startswith("/stories"):
            resp.headers["X-Robots-Tag"] = "noindex, nofollow"
        return resp

    @app.get("/")
    def index():
        stats = tally()
        return render_template("index.html", stats=stats)

    @app.get("/poll")
    def poll():
        return render_template("poll.html", challenge=new_challenge(), error=None)

    @app.post("/poll")
    def poll_post():
        check_csrf()
        if not take_challenge(request.form.get("challenge"), request.form.get("nonce")):
            return render_template(
                "poll.html",
                challenge=new_challenge(),
                error="Перевірка застаріла або не зійшлася. Онови сторінку і натисни ще раз.",
            ), 400
        choice = request.form.get("choice", "")
        if choice not in dict(CHOICES):
            return render_template(
                "poll.html",
                challenge=new_challenge(),
                error="Треба вибрати один варіант.",
            ), 400
        ballot = request.form.get("ballot", "")
        if not re.fullmatch(r"[a-f0-9]{64}", ballot or ""):
            return render_template(
                "poll.html",
                challenge=new_challenge(),
                error="Браузер не передав ключ голосу. Онови сторінку. Сховище сайту має бути дозволене.",
            ), 400
        faculty = one_of(request.form.get("faculty"), FACULTIES)
        course = one_of(request.form.get("course"), COURSES)
        form = one_of(request.form.get("form"), FORMS)
        ballot_hash = hashlib.sha256(ballot.encode()).hexdigest()
        conn = db()
        prev = conn.execute(
            "SELECT id FROM votes WHERE ballot_hash = ?", (ballot_hash,)
        ).fetchone()
        if prev:
            conn.execute(
                "UPDATE votes SET choice=?, faculty=?, course=?, form=?, day=? WHERE id=?",
                (choice, faculty, course, form, today(), prev["id"]),
            )
            replaced = True
        else:
            conn.execute(
                "INSERT INTO votes(choice, faculty, course, form, day, ballot_hash) VALUES (?,?,?,?,?,?)",
                (choice, faculty, course, form, today(), ballot_hash),
            )
            replaced = False
        conn.commit()
        return render_template("voted.html", replaced=replaced, stats=tally())

    @app.get("/results")
    def results():
        return render_template("results.html", stats=tally())

    @app.get("/export/results.json")
    def results_json():
        stats = tally()
        payload = {
            "day": stats["day"],
            "total": stats["total"],
            "note": "Самовибірка. Один браузер — один голос. Це не список усіх студентів ВДПУ.",
            "choices": [
                {"code": i["code"], "label": i["label"], "n": i["n"]} for i in stats["items"]
            ],
            "faculties": stats["faculties"],
            "forms": stats["forms"],
            "audit_sha256": stats["audit"],
        }
        import json
        return app.response_class(
            json.dumps(payload, ensure_ascii=False, indent=2),
            mimetype="application/json; charset=utf-8",
        )

    @app.get("/export/ballots.txt")
    def ballots_txt():
        conn = db()
        rows = conn.execute(
            "SELECT id, choice, faculty, course, form, day FROM votes ORDER BY id"
        ).fetchall()
        lines = ["id|choice|faculty|course|form|day"]
        for r in rows:
            lines.append("|".join(str(r[k]) for k in ("id", "choice", "faculty", "course", "form", "day")))
        lines.append("sha256=" + audit_hash(conn))
        body = "\n".join(lines) + "\n"
        return app.response_class(body, mimetype="text/plain; charset=utf-8")

    @app.get("/stories")
    def stories():
        conn = db()
        rows = conn.execute(
            "SELECT id, category, faculty, course, body, day FROM posts WHERE hidden = 0 ORDER BY id DESC LIMIT 50"
        ).fetchall()
        items = []
        for r in rows:
            text = r["body"]
            items.append({
                "id": r["id"],
                "category": r["category"],
                "faculty": r["faculty"],
                "course": r["course"],
                "day": r["day"],
                "excerpt": text[:240] + ("…" if len(text) > 240 else ""),
            })
        return render_template("stories.html", items=items)

    @app.get("/stories/new")
    def new_story():
        return render_template("new_story.html", challenge=new_challenge(), error=None)

    @app.post("/stories/new")
    def new_story_post():
        check_csrf()
        if request.form.get("agree") != "1":
            return render_template(
                "new_story.html",
                challenge=new_challenge(),
                error="Без галочки, що ти розумієш правила анонімності, допис не беремо.",
            ), 400
        body = clean_text(request.form.get("body", ""), 4000)
        problem = text_problem(body)
        if problem:
            return render_template("new_story.html", challenge=new_challenge(), error=problem), 400
        category = one_of(request.form.get("category"), CATEGORIES)
        faculty = one_of(request.form.get("faculty"), FACULTIES)
        course = one_of(request.form.get("course"), COURSES)
        files = request.files.getlist("files")
        files = [f for f in files if f and f.filename]
        if len(files) > 4:
            return render_template(
                "new_story.html", challenge=new_challenge(), error="Не більше чотирьох файлів."
            ), 400
        # Перевірку «не скрипт» спалюємо до важкого перекодування,
        # щоб не тримати базу закритою на час ffmpeg.
        if not take_challenge(request.form.get("challenge"), request.form.get("nonce")):
            return render_template(
                "new_story.html",
                challenge=new_challenge(),
                error="Перевірка застаріла. Онови сторінку і надішли ще раз. Файл доведеться вибрати заново.",
            ), 400
        prepared = []
        tmp_dir = Path(app.config["DATA_DIR"]) / "tmp"
        try:
            videos = 0
            upload_dir = Path(app.config["DATA_DIR"]) / "uploads"
            for f in files:
                kind, stored_name, digest = accept_upload(f, tmp_dir)
                if kind == "video":
                    videos += 1
                if videos > 1:
                    (upload_dir / stored_name).unlink(missing_ok=True)
                    raise ValueError("Відео можна одне. Фото — до чотирьох.")
                prepared.append((kind, stored_name, digest))
        except ValueError as exc:
            for _kind, name, _digest in prepared:
                (Path(app.config["DATA_DIR"]) / "uploads" / name).unlink(missing_ok=True)
            return render_template(
                "new_story.html", challenge=new_challenge(), error=str(exc)
            ), 400

        token = secrets.token_hex(32)
        delete_hash = hashlib.sha256(token.encode()).hexdigest()
        conn = db()
        cur = conn.execute(
            "INSERT INTO posts(category, faculty, course, body, day, delete_hash, hidden) VALUES (?,?,?,?,?,?,0)",
            (category, faculty, course, body, today(), delete_hash),
        )
        post_id = cur.lastrowid
        for kind, name, digest in prepared:
            conn.execute(
                "INSERT INTO media(post_id, kind, filename, content_hash) VALUES (?,?,?,?)",
                (post_id, kind, name, digest),
            )
        conn.commit()
        return render_template("posted.html", post_id=post_id, token=token)

    @app.get("/p/<int:post_id>")
    def story(post_id):
        conn = db()
        row = conn.execute(
            "SELECT id, category, faculty, course, body, day FROM posts WHERE id = ? AND hidden = 0",
            (post_id,),
        ).fetchone()
        if row is None:
            abort(404)
        media = conn.execute(
            "SELECT kind, filename FROM media WHERE post_id = ? ORDER BY id",
            (post_id,),
        ).fetchall()
        return render_template("story.html", post=row, media=media, error=None)

    @app.post("/p/<int:post_id>/delete")
    def delete_story(post_id):
        check_csrf()
        token = request.form.get("token", "")
        if not re.fullmatch(r"[a-f0-9]{64}", token or ""):
            abort(404)
        digest = hashlib.sha256(token.encode()).hexdigest()
        conn = db()
        row = conn.execute(
            "SELECT id, delete_hash FROM posts WHERE id = ? AND hidden = 0",
            (post_id,),
        ).fetchone()
        if row is None or not hmac.compare_digest(row["delete_hash"], digest):
            return render_template(
                "story.html",
                post=conn.execute(
                    "SELECT id, category, faculty, course, body, day FROM posts WHERE id = ? AND hidden = 0",
                    (post_id,),
                ).fetchone() or abort(404),
                media=conn.execute(
                    "SELECT kind, filename FROM media WHERE post_id = ? ORDER BY id", (post_id,)
                ).fetchall(),
                error="Ключ не підійшов.",
            ), 403
        files = conn.execute("SELECT filename FROM media WHERE post_id = ?", (post_id,)).fetchall()
        conn.execute("DELETE FROM media WHERE post_id = ?", (post_id,))
        conn.execute("DELETE FROM posts WHERE id = ?", (post_id,))
        conn.commit()
        upload_dir = Path(app.config["DATA_DIR"]) / "uploads"
        for f in files:
            (upload_dir / f["filename"]).unlink(missing_ok=True)
        return render_template("deleted.html")

    @app.get("/media/<name>")
    def media(name):
        if not re.fullmatch(r"[a-f0-9]{32}\.(jpg|mp4)", name or ""):
            abort(404)
        row = db().execute(
            """
            SELECT m.kind FROM media m
            JOIN posts p ON p.id = m.post_id
            WHERE m.filename = ? AND p.hidden = 0
            """,
            (name,),
        ).fetchone()
        if row is None:
            abort(404)
        mime = "image/jpeg" if row["kind"] == "image" else "video/mp4"
        return send_from_directory(
            Path(app.config["DATA_DIR"]) / "uploads",
            name,
            mimetype=mime,
            max_age=0,
            conditional=False,
        )

    @app.get("/facts")
    def facts():
        return render_template("facts.html")

    @app.get("/method")
    def method():
        return render_template("method.html")

    @app.get("/press")
    def press():
        return render_template("press.html", stats=tally())

    @app.get("/health")
    def health():
        return "ok", 200, {"Content-Type": "text/plain; charset=utf-8"}

    @app.post("/sluzhba/hide")
    def hide():
        want = os.environ.get("ADMIN_TOKEN", "")
        got = request.headers.get("X-Admin-Token", "")
        if not want or not got or not hmac.compare_digest(got, want):
            abort(404)
        try:
            post_id = int(request.form.get("post_id", "0"))
        except ValueError:
            abort(404)
        conn = db()
        files = conn.execute("SELECT filename FROM media WHERE post_id = ?", (post_id,)).fetchall()
        conn.execute("UPDATE posts SET hidden = 1, body = '' WHERE id = ?", (post_id,))
        conn.execute("DELETE FROM media WHERE post_id = ?", (post_id,))
        conn.commit()
        upload_dir = Path(app.config["DATA_DIR"]) / "uploads"
        for f in files:
            (upload_dir / f["filename"]).unlink(missing_ok=True)
        return "hidden", 200, {"Content-Type": "text/plain; charset=utf-8"}

    @app.get("/favicon.ico")
    def favicon():
        return "", 204

    @app.errorhandler(400)
    def err400(_e):
        return render_template("error.html", text="Запит не пройшов перевірку. Онови сторінку і спробуй ще раз."), 400

    @app.errorhandler(404)
    def err404(_e):
        return render_template("error.html", text="Немає такої сторінки."), 404

    @app.errorhandler(RequestEntityTooLarge)
    def err413(_e):
        return render_template("error.html", text="Файл завеликий. Відео до трьох хвилин і приблизно до 40 МБ, фото легші."), 413

    @app.errorhandler(Exception)
    def err500(e):
        if isinstance(e, HTTPException):
            return e
        app.logger.exception("fail")
        return render_template("error.html", text="Сервер спіткнувся. Якщо надсилав допис, глянь чи він з'явився, перш ніж слати ще раз."), 500

    def accept_upload(file_storage, tmp_dir: Path):
        suffix = secrets.token_hex(8)
        raw_path = tmp_dir / f"in-{suffix}"
        limit = 40 * 1024 * 1024
        written = 0
        with raw_path.open("wb") as out:
            while True:
                chunk = file_storage.stream.read(1024 * 1024)
                if not chunk:
                    break
                written += len(chunk)
                if written > limit:
                    raw_path.unlink(missing_ok=True)
                    raise ValueError("Файл завеликий.")
                out.write(chunk)
        try:
            head = raw_path.read_bytes()[:64]
            kind = sniff(head)
            if kind is None:
                raise ValueError("Беремо тільки фото (jpg, png, webp) і відео (mp4, mov). HEIC з айфона часто не проходить.")
            if kind == "image" and written > 8 * 1024 * 1024:
                raise ValueError("Фото більше 8 МБ. Зменш або зроби скрін.")
            upload_dir = Path(app.config["DATA_DIR"]) / "uploads"
            if kind == "image":
                jpg = rejpeg(raw_path.read_bytes())
                name = secrets.token_hex(16) + ".jpg"
                (upload_dir / name).write_bytes(jpg)
                return "image", name, hashlib.sha256(jpg).hexdigest()
            name = secrets.token_hex(16) + ".mp4"
            dest = upload_dir / name
            revideo(raw_path, dest)
            data = dest.read_bytes()
            return "video", name, hashlib.sha256(data).hexdigest()
        finally:
            raw_path.unlink(missing_ok=True)

    return app


def audit_hash(conn):
    rows = conn.execute(
        "SELECT id, choice, faculty, course, form, day FROM votes ORDER BY id"
    ).fetchall()
    lines = [
        "|".join(str(r[k]) for k in ("id", "choice", "faculty", "course", "form", "day"))
        for r in rows
    ]
    return hashlib.sha256("\n".join(lines).encode()).hexdigest()


def pow_ok(challenge, nonce, zeros):
    digest = hashlib.sha256(f"{challenge}:{nonce}".encode()).hexdigest()
    return digest.startswith("0" * zeros)


def clean_text(value, limit):
    text = (value or "").replace("\r\n", "\n").replace("\r", "\n")
    text = text.translate(HIDDEN_CHARS)
    cleaned = []
    for ch in text:
        o = ord(ch)
        if ch in "\n\t" or (32 <= o != 127 and o != 0x7F):
            if o >= 32 or ch == "\n":
                cleaned.append(ch)
    text = "".join(cleaned).strip()
    if len(text) > limit:
        text = text[:limit]
    return text


def text_problem(text):
    if len(text) < 10:
        return "Мало тексту. Хоч одним реченням напиши, що на фото або що сталося."
    if EMAIL_RE.search(text) or PHONE_RE.search(text) or HANDLE_RE.search(text) or SOCIAL_RE.search(text):
        return "Прибери пошту, телефон, @нік і посилання на телеграм чи інстаграм. Через них людину знаходять швидше, ніж через IP."
    return None


def sniff(head: bytes):
    if head[:3] == b"\xff\xd8\xff":
        return "image"
    if head[:8] == b"\x89PNG\r\n\x1a\n":
        return "image"
    if head[:6] in (b"GIF87a", b"GIF89a"):
        return "image"
    if len(head) >= 12 and head[:4] == b"RIFF" and head[8:12] == b"WEBP":
        return "image"
    if len(head) >= 12 and head[4:8] == b"ftyp":
        return "video"
    if head[:4] == b"\x1a\x45\xdf\xa3":
        return "video"
    return None


def rejpeg(data: bytes) -> bytes:
    Image.MAX_IMAGE_PIXELS = 12_000_000
    try:
        im = Image.open(io.BytesIO(data))
        im.load()
        im = ImageOps.exif_transpose(im)
        im = im.convert("RGB")
        raw = Image.frombytes("RGB", im.size, im.tobytes())
    except (UnidentifiedImageError, Image.DecompressionBombError, OSError, ValueError) as exc:
        raise ValueError("Фото не прочиталося. Скинь jpg або png.") from exc
    out = io.BytesIO()
    raw.save(out, format="JPEG", quality=78, optimize=True)
    result = out.getvalue()
    check = Image.open(io.BytesIO(result))
    exif = check.getexif()
    if exif and any(True for _ in exif):
        raise ValueError("Не вдалося зрізати дані фотоапарата. Надішли інший файл.")
    return result


def revideo(src: Path, dest: Path):
    probe = subprocess.run(
        [
            "ffprobe", "-v", "error",
            "-show_entries", "format=duration",
            "-of", "default=nw=1:nk=1",
            str(src),
        ],
        capture_output=True, text=True, timeout=30,
    )
    try:
        duration = float((probe.stdout or "0").strip() or "0")
    except ValueError:
        duration = 0
    if duration <= 0 or duration > 180:
        raise ValueError("Відео має бути коротшим за 3 хвилини.")
    audio = subprocess.run(
        [
            "ffprobe", "-v", "error", "-select_streams", "a",
            "-show_entries", "stream=index", "-of", "csv=p=0",
            str(src),
        ],
        capture_output=True, text=True, timeout=30,
    )
    cmd = [
        "ffmpeg", "-y", "-i", str(src),
        "-map_metadata", "-1", "-map_chapters", "-1",
        "-vf", "scale='min(1280,iw)':-2",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "30",
        "-pix_fmt", "yuv420p",
    ]
    if audio.stdout.strip():
        cmd += ["-c:a", "aac", "-b:a", "96k"]
    else:
        cmd += ["-an"]
    cmd += [
        "-movflags", "+faststart",
        "-fflags", "+bitexact",
        "-flags:v", "+bitexact",
        "-flags:a", "+bitexact",
        str(dest),
    ]
    try:
        done = subprocess.run(cmd, capture_output=True, timeout=150)
    except subprocess.TimeoutExpired as exc:
        dest.unlink(missing_ok=True)
        raise ValueError("Відео занадто довго перепаковувалось. Скороти його.") from exc
    if done.returncode != 0 or not dest.exists() or dest.stat().st_size < 32:
        dest.unlink(missing_ok=True)
        raise ValueError("Відео не прочиталося. Скинь mp4 або mov.")


app = create_app()

if __name__ == "__main__":
    app.run(host="127.0.0.1", port=8000, debug=False)
