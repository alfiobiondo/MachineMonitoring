#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path
from typing import Any


SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

from common.api_client import ApiClient, ApiError
from scenarios.create_live_demo import (
    DEFAULT_BASE_URL,
    DEFAULT_INITIAL_PHASE,
    DEFAULT_MACHINE_ID,
    DEFAULT_MATERIAL_CODE,
    DemoOptions,
    create_live_demo,
)


DEFAULT_FAULT_CODE = "MANUAL_DEMO_FAULT"
DEFAULT_FAULT_MESSAGE = "Manual deterministic fault."
DEFAULT_FAILURE_REASON = "Manual deterministic fault."


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    blocking = resolve_blocking(args)
    client = ApiClient(base_url=args.base_url, timeout_seconds=args.timeout)
    demo_options = DemoOptions(
        base_url=args.base_url.rstrip("/"),
        timeout_seconds=args.timeout,
        machine_id=args.machine_id,
        workpieces=1,
        operations=args.operations,
        material_code=args.material_code,
        initial_phase=args.initial_phase,
        should_start=True,
        start_from_workpiece_sequence_number=1,
        non_interactive=args.non_interactive,
    )

    try:
        demo_result = create_live_demo(client, demo_options)
        operation_id = get_current_operation_id(client, args.machine_id)

        if args.fault_after_seconds > 0:
            print(f"Attendo {args.fault_after_seconds} secondi prima del fault...")
            time.sleep(args.fault_after_seconds)

        severity = "Error" if blocking else "Warning"
        client.post_json(
            f"/api/operations/{operation_id}/fault",
            {
                "failureReason": args.failure_reason,
                "alarmCode": args.alarm_code,
                "alarmMessage": args.alarm_message,
                "severity": severity,
            },
        )

        faulted_snapshot = client.get_json(f"/api/machines/{args.machine_id}/live-snapshot")
        operation = client.get_json(f"/api/operations/{operation_id}")
        alarms = client.get_json(f"/api/operations/{operation_id}/alarms")
    except (ApiError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1

    alarm = find_latest_alarm(alarms)
    print_fault_summary(
        base_url=args.base_url,
        production_lot_id=demo_result.production_lot_id,
        snapshot=faulted_snapshot,
        operation=operation,
        alarm=alarm,
        blocking=blocking,
    )
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Crea uno scenario deterministico e invoca il fault reale via API."
    )
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--machine-id", default=DEFAULT_MACHINE_ID)
    parser.add_argument("--material-code", default=DEFAULT_MATERIAL_CODE)
    parser.add_argument("--operations", type=parse_positive_int, default=1)
    parser.add_argument("--initial-phase", default=DEFAULT_INITIAL_PHASE)
    parser.add_argument("--fault-after-seconds", type=parse_non_negative_int, default=0)
    parser.add_argument("--alarm-code", default=DEFAULT_FAULT_CODE)
    parser.add_argument("--alarm-message", default=DEFAULT_FAULT_MESSAGE)
    parser.add_argument("--failure-reason", default=DEFAULT_FAILURE_REASON)

    blocking_group = parser.add_mutually_exclusive_group()
    blocking_group.add_argument("--blocking", action="store_true", help="Usa severity Error.")
    blocking_group.add_argument(
        "--non-blocking",
        action="store_true",
        help=(
            "Usa severity Warning. Nota: l'endpoint operation fault attuale "
            "porta comunque operation/runtime in Faulted."
        ),
    )

    parser.add_argument("--non-interactive", action="store_true")
    return parser


def resolve_blocking(args: argparse.Namespace) -> bool:
    if args.blocking:
        return True
    if args.non_blocking:
        return False
    if args.non_interactive:
        return True

    while True:
        answer = input("Creare un allarme bloccante? [s]: ").strip().lower()
        if not answer:
            return True
        if answer in {"s", "si", "y", "yes"}:
            return True
        if answer in {"n", "no"}:
            return False
        print("Rispondi con s/n, si/no oppure y/n.")


def get_current_operation_id(client: ApiClient, machine_id: str) -> str:
    snapshot = client.get_json(f"/api/machines/{machine_id}/live-snapshot")
    if not isinstance(snapshot, dict):
        raise ValueError("Lo snapshot Live non e' un oggetto JSON valido.")

    current_operation = snapshot.get("currentOperation")
    if not isinstance(current_operation, dict) or not current_operation.get("id"):
        raise ValueError("Lo snapshot non contiene una operation corrente da faultare.")

    return str(current_operation["id"])


def find_latest_alarm(alarms: Any) -> dict[str, Any] | None:
    if not isinstance(alarms, list) or not alarms:
        return None
    first = alarms[0]
    return first if isinstance(first, dict) else None


def print_fault_summary(
    *,
    base_url: str,
    production_lot_id: str,
    snapshot: Any,
    operation: Any,
    alarm: dict[str, Any] | None,
    blocking: bool,
) -> None:
    if not isinstance(snapshot, dict):
        raise ValueError("Lo snapshot Live dopo il fault non e' un oggetto JSON valido.")
    if not isinstance(operation, dict):
        raise ValueError("Il dettaglio operation non e' un oggetto JSON valido.")

    operation_id = str(operation.get("id"))
    alarm_id = None if alarm is None else str(alarm.get("id"))
    machine = snapshot.get("machine")
    current_operation = snapshot.get("currentOperation")
    active_alarms = snapshot.get("activeAlarms")

    if not isinstance(machine, dict):
        raise ValueError("Lo snapshot non contiene un oggetto 'machine' valido.")

    print("\nRiepilogo fault")
    print(f"  productionLotId: {production_lot_id}")
    print(f"  operationId: {operation_id}")
    print(f"  operation status: {operation.get('status')}")
    print(f"  machine status: {machine.get('status')}")
    print(f"  current snapshot operation: {format_snapshot_operation(current_operation)}")
    print(f"  active alarms: {format_active_alarms(active_alarms)}")
    print(f"  alarmId: {alarm_id or 'non trovato'}")
    print(f"  blocking richiesto: {'si' if blocking else 'no'}")

    if alarm_id:
        print("\nResolve:")
        print(
            "  curl -X POST "
            f"{base_url.rstrip('/')}/api/alarms/{alarm_id}/resolve "
            '-H "Content-Type: application/json" '
            '-d \'{"resolutionNotes":"Resolved from manual scenario."}\''
        )
        print("\nResume operation dopo il resolve:")
        print(f"  curl -X POST {base_url.rstrip('/')}/api/operations/{operation_id}/resume")


def format_snapshot_operation(value: Any) -> str:
    if not isinstance(value, dict):
        return "null"
    return f"{value.get('id')} ({value.get('status')})"


def format_active_alarms(value: Any) -> str:
    if not isinstance(value, list):
        return "risposta non valida"
    if not value:
        return "0"

    return ", ".join(
        f"{alarm.get('id')}:{alarm.get('code')}:{alarm.get('severity')}"
        for alarm in value
        if isinstance(alarm, dict)
    )


def parse_positive_int(value: str) -> int:
    parsed = parse_non_negative_int(value)
    if parsed < 1:
        raise argparse.ArgumentTypeError("Il valore deve essere un intero positivo.")
    return parsed


def parse_non_negative_int(value: str) -> int:
    try:
        parsed = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError(f"'{value}' non e' un intero valido.") from error

    if parsed < 0:
        raise argparse.ArgumentTypeError("Il valore non puo' essere negativo.")

    return parsed


if __name__ == "__main__":
    raise SystemExit(main())
