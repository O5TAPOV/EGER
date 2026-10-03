from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import date
from decimal import Decimal
from pathlib import Path

from payday_bot.money import money


@dataclass
class Debt:
    id: str
    title: str
    balance: Decimal
    settle: str
    monthly: Decimal | None = None
    due: date | None = None

    def to_json(self) -> dict:
        return {
            "id": self.id,
            "title": self.title,
            "balance": f"{self.balance:.2f}",
            "settle": self.settle,
            "monthly": None if self.monthly is None else f"{self.monthly:.2f}",
            "due": None if self.due is None else self.due.isoformat(),
        }

    @classmethod
    def from_json(cls, raw: dict) -> "Debt":
        monthly = raw.get("monthly")
        due = raw.get("due")
        return cls(
            id=raw["id"],
            title=raw["title"],
            balance=money(raw["balance"]),
            settle=raw["settle"],
            monthly=None if monthly in (None, "") else money(monthly),
            due=None if not due else date.fromisoformat(due),
        )


@dataclass
class Receivable:
    who: str
    amount: Decimal
    count_on: bool = False

    def to_json(self) -> dict:
        return {"who": self.who, "amount": f"{self.amount:.2f}", "count_on": self.count_on}

    @classmethod
    def from_json(cls, raw: dict) -> "Receivable":
        return cls(who=raw["who"], amount=money(raw["amount"]), count_on=bool(raw.get("count_on", False)))


@dataclass
class State:
    usd_uah: Decimal
    weekly_income_usd: Decimal
    living_uah: Decimal
    rent_uah: Decimal
    rent_week_living_uah: Decimal
    parents_uah: Decimal
    parents_from: date
    parked_uah: Decimal = field(default_factory=lambda: money(0))
    windfall_uah: Decimal = field(default_factory=lambda: money(0))
    lexus_target_usd: Decimal = field(default_factory=lambda: money(10000))
    lexus_saved_usd: Decimal = field(default_factory=lambda: money(0))
    parents_held_uah: Decimal = field(default_factory=lambda: money(0))
    xchange_buy: Decimal = field(default_factory=lambda: money(0))
    undo: list = field(default_factory=list)
    debts: list[Debt] = field(default_factory=list)
    installment_paid: dict[str, str] = field(default_factory=dict)
    applied_paydays: list[str] = field(default_factory=list)
    receivables: list[Receivable] = field(default_factory=list)
    owner_chat_id: int | None = None
    last_plan: dict | None = None

    def debt(self, debt_id: str) -> Debt:
        for item in self.debts:
            if item.id == debt_id:
                return item
        raise KeyError(debt_id)

    def open_debts(self) -> list[Debt]:
        return [item for item in self.debts if item.balance > 0]

    def to_json(self) -> dict:
        return {
            "usd_uah": f"{self.usd_uah:.2f}",
            "weekly_income_usd": f"{self.weekly_income_usd:.2f}",
            "living_uah": f"{self.living_uah:.2f}",
            "rent_uah": f"{self.rent_uah:.2f}",
            "rent_week_living_uah": f"{self.rent_week_living_uah:.2f}",
            "parents_uah": f"{self.parents_uah:.2f}",
            "parents_from": self.parents_from.isoformat(),
            "parked_uah": f"{self.parked_uah:.2f}",
            "windfall_uah": f"{self.windfall_uah:.2f}",
            "lexus_target_usd": f"{self.lexus_target_usd:.2f}",
            "lexus_saved_usd": f"{self.lexus_saved_usd:.2f}",
            "parents_held_uah": f"{self.parents_held_uah:.2f}",
            "xchange_buy": f"{self.xchange_buy:.2f}",
            "undo": self.undo,
            "debts": [item.to_json() for item in self.debts],
            "installment_paid": self.installment_paid,
            "applied_paydays": self.applied_paydays,
            "receivables": [item.to_json() for item in self.receivables],
            "owner_chat_id": self.owner_chat_id,
            "last_plan": self.last_plan,
        }

    @classmethod
    def from_json(cls, raw: dict) -> "State":
        return cls(
            usd_uah=money(raw["usd_uah"]),
            weekly_income_usd=money(raw["weekly_income_usd"]),
            living_uah=money(raw["living_uah"]),
            rent_uah=money(raw["rent_uah"]),
            rent_week_living_uah=money(raw["rent_week_living_uah"]),
            parents_uah=money(raw["parents_uah"]),
            parents_from=date.fromisoformat(raw["parents_from"]),
            parked_uah=money(raw.get("parked_uah", 0)),
            windfall_uah=money(raw.get("windfall_uah", 0)),
            lexus_target_usd=money(raw.get("lexus_target_usd", 10000)),
            lexus_saved_usd=money(raw.get("lexus_saved_usd", 0)),
            parents_held_uah=money(raw.get("parents_held_uah", 0)),
            xchange_buy=money(raw.get("xchange_buy", 0)),
            undo=list(raw.get("undo") or []),
            debts=[Debt.from_json(item) for item in raw["debts"]],
            installment_paid=dict(raw.get("installment_paid", {})),
            applied_paydays=list(raw.get("applied_paydays", [])),
            receivables=[Receivable.from_json(item) for item in raw.get("receivables", [])],
            owner_chat_id=raw.get("owner_chat_id"),
            last_plan=raw.get("last_plan"),
        )


def load_state(path: Path) -> State:
    return State.from_json(json.loads(path.read_text(encoding="utf-8")))


def save_state(path: Path, state: State) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(state.to_json(), ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)
