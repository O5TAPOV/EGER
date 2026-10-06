import ast
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def _handlers(path: Path) -> set[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"))
    found: set[str] = set()
    for node in ast.walk(tree):
        if not isinstance(node, ast.Call) or not node.args:
            continue
        func = node.func
        called = func.attr if isinstance(func, ast.Attribute) else func.id if isinstance(func, ast.Name) else ""
        if called != "CommandHandler":
            continue
        name = node.args[0]
        if isinstance(name, ast.Constant) and isinstance(name.value, str):
            found.add(name.value)
    return found


def _menu_is_complete(commands, source: Path) -> None:
    names = [command.command for command in commands]
    assert set(names) == _handlers(source)
    assert len(names) == len(set(names))
    text = source.read_text(encoding="utf-8")
    assert "set_my_commands" in text
    for command in commands:
        assert command.description.strip()
        assert len(command.description) <= 256
        assert "\n" not in command.description


def test_ration_slash_menu_lists_every_command():
    from ration_bot.bot import COMMANDS

    _menu_is_complete(COMMANDS, ROOT / "ration-bot" / "ration_bot" / "bot.py")
    described = {command.command: command.description for command in COMMANDS}
    assert "Тарілки" in described["menu"]
    assert "купити" in described["shop"]


def test_payday_slash_menu_lists_every_command():
    from payday_bot.bot import COMMANDS

    _menu_is_complete(COMMANDS, ROOT / "payday_bot" / "bot.py")
    described = {command.command: command.description for command in COMMANDS}
    assert "п'ятниц" in described["plan"]
    assert "Лексус" in described["lexus"]


def test_flat_slash_menu_lists_every_command():
    from flat_bot.bot import COMMANDS

    _menu_is_complete(COMMANDS, ROOT / "flat-bot" / "flat_bot" / "bot.py")
    described = {command.command: command.description for command in COMMANDS}
    assert "Вечеря" in described["cook"]
    assert "закупки" in described["buy"].casefold()
