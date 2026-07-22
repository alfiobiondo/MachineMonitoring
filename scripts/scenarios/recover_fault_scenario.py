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
class RecoveryOptions:
    base_url: str
    timeout_seconds: float
    machine_id: str
    should_resolve: bool
    should_resume: bool
    non_interactive: bool


@dataclass(frozen=True)
class RecoveryState:
    machine_status: str | None
    operation_id: str | None
    operation_status: str | None
    active_alarm_count: int


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    try:
        options = resolve_options(args)
        client = ApiClient(base_url=options.base_url, timeout_seconds=options.timeout_seconds)
        run_recovery(client, options)
    except (ApiError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1

    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Recupera una macchina da un fault creato dallo scenario manuale."
    )
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--machine-id", default=DEFAULT_MACHINE_ID)
    parser.add_argument("--resolve", action="store_true", help="Risolve l'allarme selezionato.")
    parser.add_argument(
        "--resume",
        action="store_true",
        help="Esegue il resume dell'operation dopo il resolve.",
    )
    parser.add_argument("--non-interactive", action="store_true")
    return parser


def resolve_options(args: argparse.Namespace) -> RecoveryOptions:
    should_resolve = bool(args.resolve)
    should_resume = bool(args.resume)

    if args.non_interactive:
        if not should_resolve:
            raise ValueError("In modalita' --non-interactive serve il flag --resolve.")
        if should_resume and not should_resolve:
            raise ValueError("--resume richiede anche --resolve.")
    else:
        if not should_resolve:
            should_resolve = prompt_bool("Risolvere l'allarme", default=True)
        if should_resolve and not should_resume:
            should_resume = prompt_bool("Eseguire Resume dopo la risoluzione", default=True)

    if should_resume and not should_resolve:
        raise ValueError("--resume richiede anche --resolve.")

    return RecoveryOptions(
        base_url=args.base_url.rstrip("/"),
        timeout_seconds=args.timeout,
        machine_id=args.machine_id,
        should_resolve=should_resolve,
        should_resume=should_resume,
        non_interactive=args.non_interactive,
    )


def run_recovery(client: ApiClient, options: RecoveryOptions) -> None:
    initial_snapshot = get_snapshot(client, options.machine_id)
    initial_state = read_state(initial_snapshot)

    if initial_state.machine_status != "Faulted":
        raise ValueError(
            f"La macchina {options.machine_id} non e' Faulted: stato attuale {initial_state.machine_status}."
        )

    if initial_state.operation_id is None:
        raise ValueError("Lo snapshot non contiene una currentOperation da recuperare.")

    candidates = find_alarm_candidates(client, initial_snapshot, initial_state.operation_id)
    alarm = select_alarm(candidates, options.non_interactive)
    alarm_id = read_required_string(alarm, "id")

    after_resolve_state: RecoveryState | None = None
    after_resume_state: RecoveryState | None = None

    if options.should_resolve:
        client.post_json(
            f"/api/alarms/{alarm_id}/resolve",
            {"resolutionNotes": "Resolved from recover_fault_scenario.py."},
        )
        after_resolve_snapshot = get_snapshot(client, options.machine_id)
        after_resolve_state = read_state(after_resolve_snapshot)
        assert_after_resolve(initial_state.operation_id, after_resolve_state)

    if options.should_resume:
        client.post_json(f"/api/operations/{initial_state.operation_id}/resume")
        after_resume_snapshot = get_snapshot(client, options.machine_id)
        after_resume_state = read_state(after_resume_snapshot)
        assert_after_resume(initial_state.operation_id, after_resume_state)

    latest_snapshot = get_snapshot(client, options.machine_id)
    print_summary(
        machine_id=options.machine_id,
        operation_id=initial_state.operation_id,
        alarm_id=alarm_id,
        initial_state=initial_state,
        after_resolve_state=after_resolve_state,
        after_resume_state=after_resume_state,
        remaining_active_alarms=read_active_alarms(latest_snapshot),
    )


def get_snapshot(client: ApiClient, machine_id: str) -> dict[str, Any]:
    snapshot = client.get_json(f"/api/machines/{machine_id}/live-snapshot")
    if not isinstance(snapshot, dict):
        raise ValueError("Lo snapshot Live non e' un oggetto JSON valido.")
    return snapshot


def read_state(snapshot: dict[str, Any]) -> RecoveryState:
    machine = snapshot.get("machine")
    current_operation = snapshot.get("currentOperation")
    active_alarms = read_active_alarms(snapshot)

    if not isinstance(machine, dict):
        raise ValueError("Lo snapshot non contiene un oggetto 'machine' valido.")

    operation_id = None
    operation_status = None

    if isinstance(current_operation, dict):
        operation_id = read_required_string(current_operation, "id")
        operation_status = optional_string(current_operation.get("status"))

    return RecoveryState(
        machine_status=optional_string(machine.get("status")),
        operation_id=operation_id,
        operation_status=operation_status,
        active_alarm_count=len(active_alarms),
    )


def find_alarm_candidates(
    client: ApiClient,
    snapshot: dict[str, Any],
    operation_id: str,
) -> list[dict[str, Any]]:
    snapshot_alarm_ids = {
        read_required_string(alarm, "id")
        for alarm in read_active_alarms(snapshot)
        if isinstance(alarm, dict)
    }

    if not snapshot_alarm_ids:
        raise ValueError("Nessun allarme attivo nello snapshot Live.")

    operation_alarms = client.get_json(f"/api/operations/{operation_id}/alarms")
    if not isinstance(operation_alarms, list):
        raise ValueError("La risposta degli allarmi operation non e' una lista JSON.")

    candidates = [
        alarm
        for alarm in operation_alarms
        if isinstance(alarm, dict)
        and read_required_string(alarm, "id") in snapshot_alarm_ids
        and optional_string(alarm.get("status")) != "Resolved"
        and optional_string(alarm.get("machineOperationId")) == operation_id
    ]

    if not candidates:
        raise ValueError(
            "Nessun allarme attivo coerente con la currentOperation dello snapshot."
        )

    return candidates


def select_alarm(candidates: list[dict[str, Any]], non_interactive: bool) -> dict[str, Any]:
    if len(candidates) == 1:
        return candidates[0]

    if non_interactive:
        raise ValueError(
            "Piu' allarmi attivi sono coerenti con la currentOperation; "
            "riesegui senza --non-interactive e scegli quale risolvere."
        )

    print("Allarmi attivi candidati:")
    for index, alarm in enumerate(candidates, start=1):
        print(
            f"  {index}. {alarm.get('id')} "
            f"{alarm.get('code')} {alarm.get('severity')} - {alarm.get('message')}"
        )

    while True:
        answer = input(f"Quale allarme risolvere? [1-{len(candidates)}]: ").strip()
        try:
            selected_index = int(answer)
        except ValueError:
            print("Inserisci il numero dell'allarme.")
            continue

        if 1 <= selected_index <= len(candidates):
            return candidates[selected_index - 1]

        print("Numero fuori intervallo.")


def assert_after_resolve(operation_id: str, state: RecoveryState) -> None:
    if state.operation_id != operation_id:
        raise ValueError(
            "Resolve riuscito ma lo snapshot punta a una operation diversa: "
            f"{state.operation_id}."
        )
    if state.operation_status != "Paused" or state.machine_status != "Paused":
        raise ValueError(
            "Resolve riuscito ma stato inatteso: "
            f"machine={state.machine_status}, operation={state.operation_status}; "
            "attesi Paused/Paused."
        )


def assert_after_resume(operation_id: str, state: RecoveryState) -> None:
    if state.operation_id != operation_id:
        raise ValueError(
            "Resume eseguito ma lo snapshot punta a una operation diversa: "
            f"{state.operation_id}."
        )
    if state.operation_status != "Running" or state.machine_status != "Running":
        raise ValueError(
            "Resume non consentito o stato inatteso: "
            f"machine={state.machine_status}, operation={state.operation_status}; "
            "attesi Running/Running."
        )


def print_summary(
    *,
    machine_id: str,
    operation_id: str,
    alarm_id: str,
    initial_state: RecoveryState,
    after_resolve_state: RecoveryState | None,
    after_resume_state: RecoveryState | None,
    remaining_active_alarms: list[dict[str, Any]],
) -> None:
    print("\nRiepilogo recovery")
    print(f"  machineId: {machine_id}")
    print(f"  operationId: {operation_id}")
    print(f"  alarmId: {alarm_id}")
    print(f"  prima del resolve: {format_state(initial_state)}")
    print(
        "  dopo il resolve: "
        f"{format_state(after_resolve_state) if after_resolve_state else 'non eseguito'}"
    )
    print(
        "  dopo il resume: "
        f"{format_state(after_resume_state) if after_resume_state else 'non eseguito'}"
    )
    print(f"  allarmi ancora attivi: {format_remaining_alarms(remaining_active_alarms)}")


def format_state(state: RecoveryState) -> str:
    return (
        f"machine={state.machine_status}, "
        f"operation={state.operation_status}, "
        f"operationId={state.operation_id}, "
        f"activeAlarms={state.active_alarm_count}"
    )


def format_remaining_alarms(alarms: list[dict[str, Any]]) -> str:
    if not alarms:
        return "0"
    return ", ".join(
        f"{alarm.get('id')}:{alarm.get('code')}:{alarm.get('severity')}" for alarm in alarms
    )


def read_active_alarms(snapshot: dict[str, Any]) -> list[dict[str, Any]]:
    alarms = snapshot.get("activeAlarms")
    if not isinstance(alarms, list):
        raise ValueError("Lo snapshot non contiene una lista 'activeAlarms' valida.")
    return [alarm for alarm in alarms if isinstance(alarm, dict)]


def read_required_string(payload: dict[str, Any], field_name: str) -> str:
    value = payload.get(field_name)
    if value is None:
        alt_name = field_name[:1].upper() + field_name[1:]
        value = payload.get(alt_name)

    if value is None or str(value).strip() == "":
        raise ValueError(f"Campo '{field_name}' mancante nella risposta: {payload}")

    return str(value)


def optional_string(value: Any) -> str | None:
    if value is None:
        return None
    return str(value)


def prompt_bool(label: str, default: bool) -> bool:
    default_label = "s" if default else "n"
    while True:
        answer = input(f"{label}? [{default_label}]: ").strip().lower()
        if not answer:
            return default
        if answer in {"s", "si", "y", "yes"}:
            return True
        if answer in {"n", "no"}:
            return False
        print("Rispondi con s/n, si/no oppure y/n.")


if __name__ == "__main__":
    raise SystemExit(main())
