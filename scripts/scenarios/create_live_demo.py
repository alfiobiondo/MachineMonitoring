#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

from common.api_client import ApiClient, ApiError


DEFAULT_BASE_URL = "http://localhost:5221"
DEFAULT_MACHINE_ID = "M-001"
DEFAULT_WORKPIECES = 3
DEFAULT_OPERATIONS = 4
DEFAULT_MATERIAL_CODE = "INOX-304"
DEFAULT_INITIAL_PHASE = "Preparing production lot"
DEFAULT_DRAWING_FILE_INDEX = 0


@dataclass(frozen=True)
class DemoOptions:
    base_url: str
    timeout_seconds: float
    machine_id: str
    workpieces: int
    operations: int
    material_code: str
    initial_phase: str
    should_start: bool
    start_from_workpiece_sequence_number: int | None
    non_interactive: bool


@dataclass(frozen=True)
class DemoResult:
    production_lot_id: str
    workpiece_ids: list[str]
    operation_ids: list[str]
    machine_id: str
    started: bool
    start_from_workpiece_sequence_number: int | None


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    try:
        options = resolve_options(args, parser)
        client = ApiClient(
            base_url=options.base_url,
            timeout_seconds=options.timeout_seconds,
        )
        result = create_live_demo(client, options)
    except (ApiError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1

    print_demo_summary(options.base_url, result)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Crea tramite API un lotto demo per la pagina Live."
    )
    parser.add_argument("--base-url", default=None, help=f"Default: {DEFAULT_BASE_URL}")
    parser.add_argument("--timeout", type=float, default=20, help="Timeout HTTP in secondi.")
    parser.add_argument("--machine-id", default=None, help=f"Default: {DEFAULT_MACHINE_ID}")
    parser.add_argument("--workpieces", type=parse_positive_int, default=None)
    parser.add_argument("--operations", type=parse_positive_int, default=None)
    parser.add_argument("--material-code", default=None, help=f"Default: {DEFAULT_MATERIAL_CODE}")
    parser.add_argument(
        "--operation-type",
        choices=["LaserCutting"],
        default="LaserCutting",
        help="Attualmente l'API crea solo operation LaserCutting.",
    )
    parser.add_argument("--initial-phase", default=None, help=f"Default: {DEFAULT_INITIAL_PHASE}")

    start_group = parser.add_mutually_exclusive_group()
    start_group.add_argument("--start", action="store_true", help="Avvia il production lot.")
    start_group.add_argument("--no-start", action="store_true", help="Crea i dati senza avviare.")

    parser.add_argument(
        "--start-from-workpiece-sequence-number",
        type=parse_positive_int,
        default=None,
        help="Sequenza del solo workpiece iniziale da cui partire.",
    )
    parser.add_argument(
        "--non-interactive",
        action="store_true",
        help="Non apre prompt; usa argomenti e default.",
    )
    return parser


def resolve_options(args: argparse.Namespace, parser: argparse.ArgumentParser) -> DemoOptions:
    non_interactive = bool(args.non_interactive)
    base_url = prompt_text("Base URL API", args.base_url, DEFAULT_BASE_URL, non_interactive)
    machine_id = prompt_text("Machine ID", args.machine_id, DEFAULT_MACHINE_ID, non_interactive)
    workpieces = prompt_int(
        "Numero workpiece",
        args.workpieces,
        DEFAULT_WORKPIECES,
        non_interactive,
    )
    operations = prompt_int(
        "Operation per workpiece",
        args.operations,
        DEFAULT_OPERATIONS,
        non_interactive,
    )
    material_code = prompt_text(
        "Material code",
        args.material_code,
        DEFAULT_MATERIAL_CODE,
        non_interactive,
    )
    initial_phase = prompt_text(
        "Initial phase",
        args.initial_phase,
        DEFAULT_INITIAL_PHASE,
        non_interactive,
    )

    if args.start and args.no_start:
        parser.error("--start e --no-start sono mutuamente esclusivi")

    if args.start:
        should_start = True
    elif args.no_start:
        should_start = False
    else:
        should_start = prompt_bool("Avviare il production lot", True, non_interactive)

    start_from = args.start_from_workpiece_sequence_number
    if should_start and start_from is None and not non_interactive:
        start_from = prompt_optional_int(
            "Start from workpiece sequence number",
            default=1,
        )

    if start_from is not None and start_from > workpieces:
        raise ValueError(
            "--start-from-workpiece-sequence-number non puo' essere maggiore del numero di workpiece."
        )

    return DemoOptions(
        base_url=base_url.rstrip("/"),
        timeout_seconds=args.timeout,
        machine_id=machine_id,
        workpieces=workpieces,
        operations=operations,
        material_code=material_code,
        initial_phase=initial_phase,
        should_start=should_start,
        start_from_workpiece_sequence_number=start_from,
        non_interactive=non_interactive,
    )


def create_live_demo(client: ApiClient, options: DemoOptions) -> DemoResult:
    material = find_material_by_code(client, options.material_code)
    capabilities = get_machine_capabilities(client, options.machine_id)
    ensure_material_supported(material, capabilities, options.machine_id)
    nozzle = choose_supported_nozzle(client, capabilities, options.machine_id)
    drawing_file = choose_first(client.get_json("/api/drawing-files"), "drawing file")

    suffix = time.strftime("%Y%m%d-%H%M%S")
    lot_response = client.post_json(
        "/api/production-lots",
        {
            "code": f"LOT-LIVE-{suffix}",
            "plannedQuantity": options.workpieces,
        },
    )
    production_lot_id = read_required_id(lot_response, "productionLotId")
    print(f"Production lot creato: {production_lot_id}")

    workpiece_ids: list[str] = []
    operation_ids: list[str] = []

    for workpiece_sequence in range(1, options.workpieces + 1):
        workpiece_response = client.post_json(
            "/api/workpieces",
            {
                "productionLotId": production_lot_id,
                "sequenceNumber": workpiece_sequence,
                "code": f"WP-LIVE-{suffix}-{workpiece_sequence:02d}",
                "materialCode": options.material_code,
            },
        )
        workpiece_id = read_required_id(workpiece_response, "workpieceId")
        workpiece_ids.append(workpiece_id)
        print(f"  Workpiece {workpiece_sequence}/{options.workpieces}: {workpiece_id}")

        for operation_sequence in range(1, options.operations + 1):
            operation_response = client.post_json(
                "/api/operations",
                create_laser_cutting_operation_payload(
                    workpiece_id=workpiece_id,
                    sequence_number=operation_sequence,
                    machine_id=options.machine_id,
                    material_id=read_required_id(material, "id"),
                    nozzle_id=read_required_id(nozzle, "id"),
                    drawing_file_id=read_required_id(drawing_file, "id"),
                ),
            )
            operation_id = read_required_id(operation_response, "operationId")
            operation_ids.append(operation_id)
            print(f"    Operation {operation_sequence}/{options.operations}: {operation_id}")

    if options.should_start:
        client.post_json(
            f"/api/production-lots/{production_lot_id}/start",
            {
                "initialPhase": options.initial_phase,
                "startFromWorkpieceSequenceNumber": options.start_from_workpiece_sequence_number,
            },
        )
        print("Production lot avviato.")
    else:
        print("Production lot creato ma non avviato.")

    return DemoResult(
        production_lot_id=production_lot_id,
        workpiece_ids=workpiece_ids,
        operation_ids=operation_ids,
        machine_id=options.machine_id,
        started=options.should_start,
        start_from_workpiece_sequence_number=options.start_from_workpiece_sequence_number,
    )


def create_laser_cutting_operation_payload(
    *,
    workpiece_id: str,
    sequence_number: int,
    machine_id: str,
    material_id: str,
    nozzle_id: str,
    drawing_file_id: str,
) -> dict[str, Any]:
    return {
        "workpieceId": workpiece_id,
        "sequenceNumber": sequence_number,
        "machineId": machine_id,
        "materialId": material_id,
        "nozzleId": nozzle_id,
        "drawingFileId": drawing_file_id,
        "geometry": {
            "type": "Tube",
            "thicknessMillimeters": 3,
            "outerDiameterMillimeters": 80,
            "lengthMillimeters": 1000,
            "widthMillimeters": None,
            "heightMillimeters": None,
        },
        "laserPowerWatts": 2000,
        "cuttingSpeedMillimetersPerMinute": 1500,
        "assistGas": "Oxygen",
        "gasPressureBar": 10,
        "focalOffsetMillimeters": 0,
        "numberOfPasses": 1,
    }


def find_material_by_code(client: ApiClient, material_code: str) -> dict[str, Any]:
    materials = client.get_json("/api/materials")
    if not isinstance(materials, list):
        raise ValueError("La risposta di /api/materials non e' una lista JSON.")

    for material in materials:
        if isinstance(material, dict) and str(material.get("code")) == material_code:
            return material

    available_codes = ", ".join(
        str(item.get("code")) for item in materials if isinstance(item, dict) and item.get("code")
    )
    raise ValueError(
        f"Materiale '{material_code}' non trovato. Materiali disponibili: {available_codes or 'nessuno'}."
    )


def get_machine_capabilities(client: ApiClient, machine_id: str) -> dict[str, Any]:
    payload = client.get_json(f"/api/machines/{machine_id}/capabilities")
    if not isinstance(payload, dict):
        raise ValueError("La risposta capabilities non e' un oggetto JSON valido.")
    return payload


def ensure_material_supported(
    material: dict[str, Any],
    capabilities: dict[str, Any],
    machine_id: str,
) -> None:
    material_category = material.get("category")
    supported_categories = capabilities.get("supportedMaterialCategories")
    if not isinstance(supported_categories, list) or material_category in supported_categories:
        return

    raise ValueError(
        f"La macchina {machine_id} non supporta la categoria materiale {material_category}. "
        f"Categorie supportate: {', '.join(str(item) for item in supported_categories)}."
    )


def choose_supported_nozzle(
    client: ApiClient,
    capabilities: dict[str, Any],
    machine_id: str,
) -> dict[str, Any]:
    nozzles = client.get_json("/api/nozzles")
    supported_nozzle_ids = capabilities.get("supportedNozzleIds")

    if not isinstance(nozzles, list) or not nozzles:
        raise ValueError("Nessun nozzle disponibile.")
    if not isinstance(supported_nozzle_ids, list) or not supported_nozzle_ids:
        raise ValueError(f"La macchina {machine_id} non dichiara nozzle supportati.")

    supported = {str(nozzle_id) for nozzle_id in supported_nozzle_ids}
    for nozzle in nozzles:
        if isinstance(nozzle, dict) and str(nozzle.get("id")) in supported:
            return nozzle

    raise ValueError(f"Nessun nozzle disponibile e' supportato dalla macchina {machine_id}.")


def choose_first(payload: Any, resource_name: str) -> dict[str, Any]:
    if not isinstance(payload, list) or not payload:
        raise ValueError(f"Nessun elemento disponibile per {resource_name}.")

    first = payload[DEFAULT_DRAWING_FILE_INDEX]
    if not isinstance(first, dict):
        raise ValueError(f"La risposta per {resource_name} non contiene oggetti JSON.")

    return first


def read_required_id(payload: Any, field_name: str) -> str:
    if not isinstance(payload, dict):
        raise ValueError(f"Risposta JSON inattesa: {payload!r}")

    value = payload.get(field_name)
    if value is None:
        alt_name = field_name[:1].upper() + field_name[1:]
        value = payload.get(alt_name)

    if not value:
        raise ValueError(f"Campo '{field_name}' mancante nella risposta: {payload}")

    return str(value)


def prompt_text(
    label: str,
    value: str | None,
    default: str,
    non_interactive: bool,
) -> str:
    if value:
        return value
    if non_interactive:
        return default

    answer = input(f"{label} [{default}]: ").strip()
    return answer or default


def prompt_int(
    label: str,
    value: int | None,
    default: int,
    non_interactive: bool,
) -> int:
    if value is not None:
        return value
    if non_interactive:
        return default

    while True:
        answer = input(f"{label} [{default}]: ").strip()
        if not answer:
            return default
        try:
            return parse_positive_int(answer)
        except argparse.ArgumentTypeError as error:
            print(error)


def prompt_optional_int(label: str, default: int | None) -> int | None:
    default_label = "" if default is None else str(default)
    while True:
        answer = input(f"{label} [{default_label}]: ").strip()
        if not answer:
            return default
        try:
            return parse_positive_int(answer)
        except argparse.ArgumentTypeError as error:
            print(error)


def prompt_bool(label: str, default: bool, non_interactive: bool) -> bool:
    if non_interactive:
        return default

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


def parse_positive_int(value: str) -> int:
    try:
        parsed = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError(f"'{value}' non e' un intero valido.") from error

    if parsed < 1:
        raise argparse.ArgumentTypeError("Il valore deve essere un intero positivo.")

    return parsed


def print_demo_summary(base_url: str, result: DemoResult) -> None:
    live_url = f"{base_url.rstrip('/')}/api/machines/{result.machine_id}/live-snapshot"
    print("\nRiepilogo")
    print(f"  productionLotId: {result.production_lot_id}")
    print(f"  workpiece creati: {len(result.workpiece_ids)}")
    for index, workpiece_id in enumerate(result.workpiece_ids, start=1):
        print(f"    {index}. {workpiece_id}")
    print(f"  operation create: {len(result.operation_ids)}")
    print(f"  macchina: {result.machine_id}")
    print(f"  start: {'eseguito' if result.started else 'non eseguito'}")
    if result.started:
        print(
            "  startFromWorkpieceSequenceNumber: "
            f"{result.start_from_workpiece_sequence_number}"
        )
    print("\nLive snapshot:")
    print(f"  curl {live_url}")


if __name__ == "__main__":
    raise SystemExit(main())
