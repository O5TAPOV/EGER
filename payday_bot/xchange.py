import re
import urllib.request

from payday_bot.money import money

PAGE = "https://x-change-x.com/vinnitsa/"


def parse_usd_page(html: str):
    text = re.sub(r"<[^>]+>", " ", html)
    text = re.sub(r"\s+", " ", text)
    found = re.search(
        r"USD/UAH\s+([0-9]+(?:[.,][0-9]+)?)\s+([0-9]+(?:[.,][0-9]+)?)",
        text,
    )
    if not found:
        return None
    buy = money(found.group(1).replace(",", "."))
    sell = money(found.group(2).replace(",", "."))
    if buy <= 0 or sell <= 0:
        return None
    return buy, sell


def fetch_usd():
    request = urllib.request.Request(PAGE, headers={"User-Agent": "payday-bot"})
    with urllib.request.urlopen(request, timeout=8) as response:
        html = response.read().decode("utf-8", "replace")
    return parse_usd_page(html)
