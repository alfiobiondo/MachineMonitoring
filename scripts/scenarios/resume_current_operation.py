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
class OperationState:
    machine_status: str | None
    operation_id: str
    operation_status: str | None
    progress_percentage: int


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    client = ApiClient(base_url=args.base_url, timeout_seconds=args.timeout)

    try:
        if not args.non_interactive and not prompt_bool("Riprendere l'operation corrente", True):
            print("Resume annullato.")
            return 0

        before = read_current_state(client, args.machine_id)
        validate_before_resume(before)

        client.post_json(f"/api/operations/{before.operation_id}/resume")

        after = read_current_state(client, args.machine_id)
        validate_after_resume(before, after)
    except (ApiError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1

    print_summary(args.machine_id, before, after)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Riprende l'operation corrente in pausa.")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--machine-id", default=DEFAULT_MACHINE_ID)
    parser.add_argument("--non-interactive", action="store_true")
    return parser


def read_current_state(client: ApiClient, machine_id: str) -> OperationState:
    snapshot = client.get_json(f"/api/machines/{machine_id}/live-snapshot")
    if not isinstance(snapshot, dict):
        raise ValueError("Lo snapshot Live non e' un oggetto JSON valido.")

    machine = snapshot.get("machine")
    operation = snapshot.get("currentOperation")

    if not isinstance(machine, dict):
        raise ValueError("Lo snapshot non contiene un oggetto 'machine' valido.")
    if not isinstance(operation, dict):
        raise ValueError("Nessuna operation corrente da riprendere.")

    return OperationState(
        machine_status=optional_string(machine.get("status")),
        operation_id=read_required_string(operation, "id"),
        operation_status=optional_string(operation.get("status")),
        progress_percentage=read_required_int(operation, "progressPercentage"),
    )


def validate_before_resume(state: OperationState) -> None:
    if state.operation_status != "Paused":
        raise ValueError(
            f"L'operation corrente non e' Paused: stato attuale {state.operation_status}."
        )
    if state.machine_status == "Faulted":
        raise ValueError("La macchina e' Faulted: risolvi prima l'allarme bloccante.")


def validate_after_resume(before: OperationState, after: OperationState) -> None:
    if after.operation_id != before.operation_id:
        raise ValueError(
            "Resume eseguito ma lo snapshot punta a una operation diversa: "
            f"{after.operation_id}."
        )
    if after.operation_status != "Running" or after.machine_status != "Running":
        raise ValueError(
            "Resume eseguito ma stato inatteso: "
            f"machine={after.machine_status}, operation={after.operation_status}; "
            "attesi Running/Running."
        )
    if after.progress_percentage != before.progress_percentage:
        raise ValueError(
            "Resume eseguito ma il progress e' cambiato: "
            f"prima={before.progress_percentage}, dopo={after.progress_percentage}."
        )


def print_summary(machine_id: str, before: OperationState, after: OperationState) -> None:
    print("\nRiepilogo resume")
    print(f"  machineId: {machine_id}")
    print(f"  operationId: {before.operation_id}")
    print(f"  prima: machine={before.machine_status}, operation={before.operation_status}, progress={before.progress_percentage}%")
    print(f"  dopo: machine={after.machine_status}, operation={after.operation_status}, progress={after.progress_percentage}%")


def read_required_string(payload: dict[str, Any], field_name: str) -> str:
    value = payload.get(field_name)
    if value is None:
        raise ValueError(f"Campo '{field_name}' mancante nella risposta: {payload}")
    return str(value)


def read_required_int(payload: dict[str, Any], field_name: str) -> int:
    value = payload.get(field_name)
    if not isinstance(value, int):
        raise ValueError(f"Campo '{field_name}' mancante o non intero nella risposta: {payload}")
    return value


def optional_string(value: Any) -> str | None:
    return None if value is None else str(value)


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


if __name__ == "__main__":
    raise SystemExit(main())
