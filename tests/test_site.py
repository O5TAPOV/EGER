import io
import os
import re
import sqlite3
import subprocess
from pathlib import Path

from PIL import Image

from app import create_app, pow_ok


def solve(challenge):
    n = 0
    while n < 2000000:
        nonce = format(n, "x")
        if pow_ok(challenge, nonce, 4):
            return nonce
        n += 1
    raise AssertionError("pow")


def make_client(tmp_path, token="adminkeytokenvalue123456"):
    os.environ["ADMIN_TOKEN"] = token
    app = create_app({"DATA_DIR": str(tmp_path), "TESTING": True})
    return app.test_client(), tmp_path


def fields(html):
    csrf = re.search(r'name="csrf" value="([a-f0-9]+)"', html).group(1)
    challenge = re.search(r'name="challenge" value="([a-f0-9]+)"', html).group(1)
    return csrf, challenge


def origin():
    return {"Origin": "http://localhost"}


def test_sha256_js_matches_python():
    import hashlib
    script = Path(__file__).resolve().parents[1] / "static" / "pow.js"
    node = subprocess.run(
        ["node", "-e", "const p=require(process.argv[1]); console.log(p.sha256hex('')); console.log(p.sha256hex('abc')); console.log(p.sha256hex('hello:1'));", str(script)],
        capture_output=True, text=True, check=True,
    )
    lines = node.stdout.strip().splitlines()
    assert lines[0] == hashlib.sha256(b"").hexdigest()
    assert lines[1] == hashlib.sha256(b"abc").hexdigest()
    assert lines[2] == hashlib.sha256(b"hello:1").hexdigest()


def test_pages_quote_rector_and_skip_the_channel_trash(tmp_path):
    client, _ = make_client(tmp_path)
    for path in ("/", "/facts", "/method", "/press", "/poll"):
        html = client.get(path).get_data(as_text=True)
        assert "Гітлер" not in html
        assert "367" not in html
        assert "пізда" not in html.lower()
        assert "шахра" not in html.lower()
    home = client.get("/").get_data(as_text=True)
    assert "Замкова" in home
    assert "анкет" in home
    assert "гугл-форм" in home.lower()
    assert "окремої анкети їм не давали" not in home
    assert "Це дірка" not in home
    assert 'target="_blank"' in home
    assert "suspilne.media" in home
    facts = client.get("/facts").get_data(as_text=True)
    assert "Юлія Жалюк" in facts
    assert "ерготерапію" in facts
    assert "гугл-форм" in facts.lower()
    assert "nnpol13.pdf" in facts
    assert "4,5" in facts or "4.5" in facts
    press = client.get("/press").get_data(as_text=True)
    assert "окремої анкети їм не давали" not in press
    assert "гугл-форму" in press
    poll = client.get("/poll").get_data(as_text=True)
    assert "по черзі" in poll
    assert "2 тижні" in poll
    assert "10 днів" in poll
    method = client.get("/method").get_data(as_text=True)
    assert "не має стосунку до ВДПУ" in method
    assert "не перевірити" in facts
    for html in (home, facts, press, poll, method):
        assert "\u2014" not in html
        assert "\u2013" not in html


def test_vote_is_one_row_and_has_no_ip(tmp_path):
    client, folder = make_client(tmp_path)
    page = client.get("/poll")
    csrf, challenge = fields(page.get_data(as_text=True))
    ballot = "ab" * 32
    res = client.post("/poll", data={
        "csrf": csrf,
        "challenge": challenge,
        "nonce": solve(challenge),
        "ballot": ballot,
        "choice": "dist",
        "faculty": "Не вказую",
        "course": "Не вказую",
        "form": "денна",
    }, headers=origin())
    assert res.status_code == 200
    assert "зараховано" in res.get_data(as_text=True)

    page = client.get("/poll")
    csrf, challenge = fields(page.get_data(as_text=True))
    res = client.post("/poll", data={
        "csrf": csrf,
        "challenge": challenge,
        "nonce": solve(challenge),
        "ballot": ballot,
        "choice": "mixed",
        "faculty": "<script>",
        "course": "2",
        "form": "денна",
    }, headers=origin())
    assert res.status_code == 200
    body = client.get("/results").get_data(as_text=True)
    assert "Лекції дистанційно" in body
    assert "<script>" not in body
    assert "Усього голосів: <b>1</b>" in body

    conn = sqlite3.connect(folder / "site.db")
    cols = [r[1] for r in conn.execute("PRAGMA table_info(votes)")]
    assert "ip" not in cols
    blob = (folder / "site.db").read_bytes()
    assert b"127.0.0.1" not in blob
    assert conn.execute("SELECT COUNT(*) FROM votes").fetchone()[0] == 1
    choice = conn.execute("SELECT choice, faculty FROM votes").fetchone()
    assert choice[0] == "mixed"
    assert choice[1] == "Не вказую"


def test_rejects_foreign_origin(tmp_path):
    client, _ = make_client(tmp_path)
    page = client.get("/poll")
    csrf, challenge = fields(page.get_data(as_text=True))
    res = client.post("/poll", data={
        "csrf": csrf,
        "challenge": challenge,
        "nonce": solve(challenge),
        "ballot": "cd" * 32,
        "choice": "offline",
    }, headers={"Origin": "http://evil.test"})
    assert res.status_code == 400


def test_story_strips_exif_escapes_html_and_deletes(tmp_path):
    client, folder = make_client(tmp_path)
    img = Image.new("RGB", (24, 18), (40, 10, 10))
    exif = img.getexif()
    exif[271] = "SECRETPHONE"
    exif[272] = "SECRETMODEL"
    buf = io.BytesIO()
    img.save(buf, format="JPEG", exif=exif.tobytes())
    raw = buf.getvalue()
    assert b"SECRETPHONE" in raw

    page = client.get("/stories/new")
    csrf, challenge = fields(page.get_data(as_text=True))
    res = client.post("/stories/new", data={
        "csrf": csrf,
        "challenge": challenge,
        "nonce": solve(challenge),
        "agree": "1",
        "category": "Укриття",
        "faculty": "Історичний",
        "course": "Не вказую",
        "body": "<script>alert(1)</script> підвал корпусу, пліснява на стелі",
        "files": (io.BytesIO(raw), "myphone.jpg"),
    }, headers=origin(), content_type="multipart/form-data")
    assert res.status_code == 200, res.get_data(as_text=True)
    html = res.get_data(as_text=True)
    token = re.search(r"<textarea[^>]*>([a-f0-9]{64})</textarea>", html).group(1)
    post_id = re.search(r'href="/p/(\d+)"', html).group(1)

    story = client.get(f"/p/{post_id}").get_data(as_text=True)
    assert "<script>alert" not in story
    assert "&lt;script&gt;" in story
    name = re.search(r'src="/media/([a-f0-9]+\.jpg)"', story).group(1)
    media = client.get(f"/media/{name}")
    assert media.status_code == 200
    assert media.mimetype == "image/jpeg"
    assert b"SECRETPHONE" not in media.data
    assert b"SECRETMODEL" not in media.data
    opened = Image.open(io.BytesIO(media.data))
    assert not any(True for _ in opened.getexif())

    conn = sqlite3.connect(folder / "site.db")
    stored = conn.execute("SELECT body FROM posts").fetchone()[0]
    assert "myphone" not in stored
    assert b"SECRETPHONE" not in (folder / "site.db").read_bytes()

    bad = client.post(f"/p/{post_id}/delete", data={"csrf": csrf, "token": "ab" * 32}, headers=origin())
    assert bad.status_code == 403
    page = client.get("/stories/new")
    csrf2, _challenge = fields(page.get_data(as_text=True))
    gone = client.post(f"/p/{post_id}/delete", data={"csrf": csrf2, "token": token}, headers=origin())
    assert gone.status_code == 200
    assert client.get(f"/p/{post_id}").status_code == 404
    assert client.get(f"/media/{name}").status_code == 404
    assert not (folder / "uploads" / name).exists()


def test_phone_and_video_metadata(tmp_path):
    client, folder = make_client(tmp_path)
    page = client.get("/stories/new")
    csrf, challenge = fields(page.get_data(as_text=True))
    blocked = client.post("/stories/new", data={
        "csrf": csrf,
        "challenge": challenge,
        "nonce": solve(challenge),
        "agree": "1",
        "body": "пишіть мені user@example.com будь ласка сьогодні",
    }, headers=origin())
    assert blocked.status_code == 400

    src = folder / "in.mp4"
    subprocess.run(
        [
            "ffmpeg", "-y", "-f", "lavfi", "-i", "color=c=red:s=64x64:d=1",
            "-f", "lavfi", "-i", "sine=f=440:d=1", "-shortest",
            "-metadata", "title=SECRETPHONE", "-metadata", "comment=SECRETPHONE",
            str(src),
        ],
        check=True, capture_output=True,
    )
    page = client.get("/stories/new")
    csrf, challenge = fields(page.get_data(as_text=True))
    res = client.post("/stories/new", data={
        "csrf": csrf,
        "challenge": challenge,
        "nonce": solve(challenge),
        "agree": "1",
        "body": "коротке відео дверей укриття без людей",
        "files": (src.open("rb"), "clip.mov"),
    }, headers=origin(), content_type="multipart/form-data")
    assert res.status_code == 200, res.get_data(as_text=True)
    story = client.get("/p/1").get_data(as_text=True)
    name = re.search(r'src="/media/([a-f0-9]+\.mp4)"', story).group(1)
    data = client.get(f"/media/{name}").data
    assert b"SECRETPHONE" not in data
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format_tags", "-of", "default=nw=1", "-i", "pipe:0"],
        input=data, capture_output=True,
    )
    assert b"SECRETPHONE" not in probe.stdout


def test_admin_hide_is_invisible_without_token(tmp_path):
    client, _ = make_client(tmp_path)
    assert client.post("/sluzhba/hide", data={"post_id": "1"}).status_code == 404
    page = client.get("/stories/new")
    csrf, challenge = fields(page.get_data(as_text=True))
    res = client.post("/stories/new", data={
        "csrf": csrf, "challenge": challenge, "nonce": solve(challenge),
        "agree": "1", "body": "текст достатньої довжини для ховання",
    }, headers=origin())
    assert res.status_code == 200
    hidden = client.post("/sluzhba/hide", data={"post_id": "1"}, headers={"X-Admin-Token": "adminkeytokenvalue123456"})
    assert hidden.status_code == 200
    assert client.get("/p/1").status_code == 404


def test_templates_external_links_and_dashes():
    root = Path(__file__).resolve().parents[1] / "templates"
    found = 0
    for path in root.glob("*.html"):
        body = path.read_text(encoding="utf-8")
        assert "\u2014" not in body, path.name
        assert "\u2013" not in body, path.name
        for tag in re.findall(r"<a\s[^>]*href=\"https?://[^\"]+\"[^>]*>", body):
            found += 1
            assert 'target="_blank"' in tag, tag
            assert "noopener" in tag, tag
    assert found >= 4
