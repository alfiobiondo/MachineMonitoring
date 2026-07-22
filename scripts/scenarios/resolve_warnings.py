#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

from common.api_client import ApiClient, ApiError
from scenarios.create_live_demo import DEFAULT_BASE_URL, DEFAULT_MACHINE_ID


@dataclass(frozen=True)
class WarningAlarm:
    id: str
    code: str
    message: str
    status: str
    raised_at: str
    is_blocking: bool


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    client = ApiClient(base_url=args.base_url, timeout_seconds=args.timeout)

    try:
        selected_warnings = select_warnings(client, args)

        for warning in selected_warnings:
            resolve_warning(client, args.machine_id, warning, args.resolution_notes)
    except (ApiError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1

    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Risolve warning macchina non bloccanti presenti nello snapshot Live."
    )
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--machine-id", default=DEFAULT_MACHINE_ID)
    parser.add_argument("--alarm-id", help="Id del warning non bloccante da risolvere.")
    parser.add_argument("--all", action="store_true", help="Risolve tutti i warning non bloccanti.")
    parser.add_argument(
        "--resolution-notes",
        default="Resolved from resolve_warnings.py.",
        help="Note inviate nel payload resolve.",
    )
    parser.add_argument("--non-interactive", action="store_true")
    return parser


def select_warnings(client: ApiClient, args: argparse.Namespace) -> list[WarningAlarm]:
    snapshot = get_snapshot(client, args.machine_id)
    active_alarms = read_active_alarms(snapshot)
    warning_candidates = [
        alarm
        for alarm in active_alarms
        if not alarm.is_blocking and alarm.status in {"Active", "Acknowledged"}
    ]

    if args.alarm_id and args.all:
        raise ValueError("Usa solo uno tra --alarm-id e --all.")

    if args.non_interactive:
        return select_warnings_non_interactive(args, active_alarms, warning_candidates)

    return select_warnings_interactive(args, warning_candidates)


def select_warnings_non_interactive(
    args: argparse.Namespace,
    active_alarms: list[WarningAlarm],
    warning_candidates: list[WarningAlarm],
) -> list[WarningAlarm]:
    if args.all:
        if not warning_candidates:
            raise ValueError("Nessun warning non bloccante Active/Acknowledged da risolvere.")

        return warning_candidates

    if args.alarm_id:
        selected = next((alarm for alarm in active_alarms if alarm.id == args.alarm_id), None)
        if selected is None:
            raise ValueError(f"Alarm '{args.alarm_id}' non trovato tra gli allarmi attivi.")
        if selected.is_blocking:
            raise ValueError(
                f"Alarm '{args.alarm_id}' e' bloccante: lo script non risolve blocking alarm."
            )
        if selected.status not in {"Active", "Acknowledged"}:
            raise ValueError(
                f"Alarm '{args.alarm_id}' ha stato {selected.status}; attesi Active/Acknowledged."
            )

        return [selected]

    raise ValueError("In modalita' --non-interactive serve --alarm-id oppure --all.")


def select_warnings_interactive(
    args: argparse.Namespace,
    warning_candidates: list[WarningAlarm],
) -> list[WarningAlarm]:
    if not warning_candidates:
        raise ValueError("Nessun warning non bloccante Active/Acknowledged da risolvere.")

    print("\nWarning non bloccanti attivi")
    for index, warning in enumerate(warning_candidates, start=1):
        print(
            f"  {index}. id={warning.id} code={warning.code} "
            f"status={warning.status} raisedAt={warning.raised_at}"
        )
        print(f"     {warning.message}")

    if args.all or prompt_bool("Risolvere tutti i warning", default=False):
        if prompt_bool("Confermi il resolve di tutti i warning non bloccanti", default=True):
            return warning_candidates

        print("Resolve annullato.")
        return []

    selected = prompt_warning_selection(warning_candidates)
    if not prompt_bool(f"Risolvere il warning {selected.code}", default=True):
        print("Resolve annullato.")
        return []

    return [selected]


def resolve_warning(
    client: ApiClient,
    machine_id: str,
    warning: WarningAlarm,
    resolution_notes: str,
) -> None:
    print(f"\nResolve warning {warning.code} ({warning.id})")
    client.post_json(
        f"/api/alarms/{warning.id}/resolve",
        {"resolutionNotes": resolution_notes},
    )

    snapshot = get_snapshot(client, machine_id)
    remaining_warnings = [
        alarm
        for alarm in read_active_alarms(snapshot)
        if not alarm.is_blocking and alarm.status in {"Active", "Acknowledged"}
    ]

    if any(alarm.id == warning.id for alarm in remaining_warnings):
        raise ValueError(
            f"Resolve chiamato ma il warning {warning.id} e' ancora tra gli allarmi attivi."
        )

    print("  esito: risolto")
    print(f"  warning non bloccanti ancora attivi: {len(remaining_warnings)}")


def get_snapshot(client: ApiClient, machine_id: str) -> dict[str, Any]:
    snapshot = client.get_json(f"/api/machines/{machine_id}/live-snapshot")
    if not isinstance(snapshot, dict):
        raise ValueError("Lo snapshot Live non e' un oggetto JSON valido.")

    return snapshot


def read_active_alarms(snapshot: dict[str, Any]) -> list[WarningAlarm]:
    active_alarms = snapshot.get("activeAlarms")
    if not isinstance(active_alarms, list):
        raise ValueError("Lo snapshot non contiene una lista 'activeAlarms' valida.")

    return [read_warning_alarm(alarm) for alarm in active_alarms]


def read_warning_alarm(payload: Any) -> WarningAlarm:
    if not isinstance(payload, dict):
        raise ValueError(f"Allarme non valido nello snapshot: {payload}")

    return WarningAlarm(
        id=read_required_string(payload, "id"),
        code=read_required_string(payload, "code"),
        message=read_required_string(payload, "message"),
        status=read_required_string(payload, "status"),
        raised_at=read_required_string(payload, "raisedAt"),
        is_blocking=read_required_bool(payload, "isBlocking"),
    )


def prompt_warning_selection(warnings: list[WarningAlarm]) -> WarningAlarm:
    while True:
        answer = input(f"Scegli warning [1-{len(warnings)}]: ").strip()
        try:
            selected_index = int(answer)
        except ValueError:
            print("Inserisci un numero valido.")
            continue

        if 1 <= selected_index <= len(warnings):
            return warnings[selected_index - 1]

        print(f"Scegli un numero tra 1 e {len(warnings)}.")


def prompt_bool(label: str, default: bool) -> bool:
    default_label = "S/n" if default else "s/N"
    while True:
        answer = input(f"{label}? [{default_label}] ").strip().lower()
        if not answer:
            return default
        if answer in {"s", "si", "y", "yes"}:
            return True
        if answer in {"n", "no"}:
            return False
        print("Rispondi con s/n, si/no oppure y/n.")


def read_required_string(payload: dict[str, Any], field_name: str) -> str:
    value = payload.get(field_name)
    if value is None:
        raise ValueError(f"Campo '{field_name}' mancante nella risposta: {payload}")
    return str(value)


def read_required_bool(payload: dict[str, Any], field_name: str) -> bool:
    value = payload.get(field_name)
    if not isinstance(value, bool):
        raise ValueError(f"Campo '{field_name}' mancante o non booleano nella risposta: {payload}")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
