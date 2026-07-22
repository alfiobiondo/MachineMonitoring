#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any


SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

from common.api_client import ApiClient, ApiError


DEFAULT_BASE_URL = "http://localhost:5221"
DEFAULT_MACHINE_ID = "M-001"


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Verifica tramite API se una macchina esistente ha uno snapshot Live "
            "valido ma nessun contesto produttivo corrente."
        )
    )
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--machine-id", default=DEFAULT_MACHINE_ID)
    args = parser.parse_args()

    client = ApiClient(base_url=args.base_url, timeout_seconds=args.timeout)

    try:
        snapshot = client.get_json(f"/api/machines/{args.machine_id}/live-snapshot")
    except ApiError as error:
        print(error, file=sys.stderr)
        return 1

    try:
        print_empty_machine_report(args.base_url, args.machine_id, snapshot)
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1

    return 0


def print_empty_machine_report(
    base_url: str,
    machine_id: str,
    snapshot: Any,
) -> None:
    if not isinstance(snapshot, dict):
        raise ValueError("Lo snapshot Live non e' un oggetto JSON valido.")

    machine = snapshot.get("machine")
    if not isinstance(machine, dict):
        raise ValueError("Lo snapshot non contiene un oggetto 'machine' valido.")

    machine_status = machine.get("status")
    production_lot = snapshot.get("productionLot")
    current_workpiece = snapshot.get("currentWorkpiece")
    current_operation = snapshot.get("currentOperation")

    is_empty = (
        production_lot is None
        and current_workpiece is None
        and current_operation is None
    )

    print(f"Macchina: {machine.get('id', machine_id)}")
    print(f"Machine status: {machine_status}")
    print(f"ProductionLot corrente: {format_optional_id(production_lot)}")
    print(f"Workpiece corrente: {format_optional_id(current_workpiece)}")
    print(f"Operation corrente: {format_optional_id(current_operation)}")

    if is_empty:
        print("\nLa macchina e' vuota per la Live: snapshot valido e nessun contesto produttivo corrente.")
    else:
        print(
            "\nLa macchina non e' vuota: esiste un contesto produttivo corrente "
            "oppure il runtime ha un'operation assegnata."
        )

    print("\nLive snapshot:")
    print(f"  curl {base_url.rstrip('/')}/api/machines/{machine_id}/live-snapshot")


def format_optional_id(value: Any) -> str:
    if value is None:
        return "null"
    if isinstance(value, dict):
        return str(value.get("id", value))
    return str(value)


if __name__ == "__main__":
    raise SystemExit(main())
