# MachineMonitoring Scripts

Script Python versionati per creare scenari manuali usando solo le API pubbliche.

## Requisiti

- Python 3, senza dipendenze esterne.
- API avviata, per esempio su `http://localhost:5221`.
- Per vedere avanzare il progresso in automatico, avviare anche il Console worker del progetto.

Gli script non leggono o modificano direttamente PostgreSQL.

## Struttura

```text
scripts/
|-- common/
|   `-- api_client.py
|-- scenarios/
|   |-- create_live_demo.py
|   |-- create_empty_machine.py
|   |-- create_fault_scenario.py
|   |-- pause_current_operation.py
|   |-- recover_fault_scenario.py
|   |-- resolve_warnings.py
|   `-- resume_current_operation.py
|-- pause-machine.sh
|-- recover-machine.sh
|-- resolve-warnings.sh
|-- run-live-demo.sh
`-- resume-machine.sh
```

`common/api_client.py` contiene il client HTTP comune: `GET`, `POST`, JSON, risposte senza body e `application/problem+json`.

## create_live_demo

Crea:

- un production lot;
- N workpiece;
- N operation laser cutting per workpiece;
- opzionalmente avvia il production lot.

Default principali:

- `--base-url http://localhost:5221`
- `--machine-id M-001`
- `--workpieces 3`
- `--operations 4`
- `--material-code INOX-304`
- `--initial-phase "Preparing production lot"`

L'API corrente crea operation laser cutting; `--operation-type` accetta solo `LaserCutting` e non viene inviato al backend perche' il DTO non espone un campo operation type.

Esempi:

```bash
./scripts/run-live-demo.sh
./scripts/run-live-demo.sh --workpieces 3 --operations 4 --start
python3 scripts/scenarios/create_live_demo.py --non-interactive --start
python3 scripts/scenarios/create_live_demo.py --machine-id M-001 --workpieces 2 --operations 2 --no-start
```

`--start-from-workpiece-sequence-number` seleziona un solo workpiece iniziale della sequenza del lotto. Non significa avviare tutti i workpiece da quel numero in poi.

## create_empty_machine

Nel dominio attuale "macchina vuota" significa:

- macchina esistente;
- snapshot Live valido;
- `machine.status` leggibile dallo snapshot;
- nessun `productionLot`, `currentWorkpiece` o `currentOperation` nello snapshot.

Lo script usa solo:

```text
GET /api/machines/{machineId}/live-snapshot
```

Le API non espongono un comando dedicato per svuotare deterministicamente una macchina senza incidere su lotti/operation esistenti. Per questo lo script verifica lo stato corrente e stampa un report.

Esempio:

```bash
python3 scripts/scenarios/create_empty_machine.py --machine-id M-001
```

## create_fault_scenario

Crea un lotto minimo, avvia il production lot, legge l'operation corrente dallo snapshot Live e invoca l'endpoint reale:

```text
POST /api/operations/{operationId}/fault
```

Default:

- `--operations 1`
- `--blocking`, se non specificato diversamente;
- `--alarm-code MANUAL_DEMO_FAULT`
- `--alarm-message "Manual deterministic fault."`
- `--failure-reason "Manual deterministic fault."`

Esempi:

```bash
python3 scripts/scenarios/create_fault_scenario.py --non-interactive
python3 scripts/scenarios/create_fault_scenario.py --fault-after-seconds 3 --blocking
python3 scripts/scenarios/create_fault_scenario.py --non-blocking
```

Nota: l'endpoint operation fault attuale porta comunque operation e runtime in `Faulted`, anche se la severity inviata e' `Warning`.

Al termine lo script rilegge:

```text
GET /api/machines/{machineId}/live-snapshot
```

e stampa:

- `operationId`;
- stato operation;
- `machine.status`;
- allarmi attivi nello snapshot;
- eventuale `alarmId`;
- comandi `curl` per resolve e resume.

Flusso manuale:

1. crea lo scenario fault;
2. risolvi l'allarme con `POST /api/alarms/{alarmId}/resolve`;
3. riprendi l'operation con `POST /api/operations/{operationId}/resume`.

## recover_fault_scenario

Recupera una macchina da un fault operation-level creato dallo scenario manuale.

Flusso:

```text
create fault
-> machine/operation Faulted
-> resolve alarm
-> machine/operation Paused
-> resume
-> machine/operation Running
```

Endpoint usati:

```text
GET  /api/machines/{machineId}/live-snapshot
GET  /api/operations/{operationId}/alarms
POST /api/alarms/{alarmId}/resolve
POST /api/operations/{operationId}/resume
```

Payload resolve:

```json
{
  "resolutionNotes": "Resolved from recover_fault_scenario.py."
}
```

Resume non richiede body.

Esempi:

```bash
./scripts/recover-machine.sh --machine-id M-001
python3 scripts/scenarios/recover_fault_scenario.py --machine-id M-001 --resolve --resume --non-interactive
python3 scripts/scenarios/recover_fault_scenario.py --machine-id M-001 --resolve --non-interactive
```

In modalita' interattiva lo script chiede se risolvere l'allarme e se eseguire `resume`. Se piu' allarmi attivi sono coerenti con la current operation, mostra l'elenco e chiede quale risolvere.

In modalita' `--non-interactive` serve `--resolve`; se piu' allarmi sono candidati, lo script termina con un errore chiaro invece di scegliere automaticamente.

## pause_current_operation

Mette in pausa la current operation della macchina usando:

```text
GET  /api/machines/{machineId}/live-snapshot
POST /api/operations/{operationId}/pause
GET  /api/machines/{machineId}/live-snapshot
```

Esempi:

```bash
./scripts/pause-machine.sh --machine-id M-001 --non-interactive
python3 scripts/scenarios/pause_current_operation.py --machine-id M-001
```

Lo script richiede una current operation `Running` e una macchina `Running`. Dopo il pause verifica che machine e operation siano `Paused` e che il progress non sia stato azzerato.

## resume_current_operation

Riprende la current operation in pausa usando:

```text
GET  /api/machines/{machineId}/live-snapshot
POST /api/operations/{operationId}/resume
GET  /api/machines/{machineId}/live-snapshot
```

Esempi:

```bash
./scripts/resume-machine.sh --machine-id M-001 --non-interactive
python3 scripts/scenarios/resume_current_operation.py --machine-id M-001
```

Lo script richiede una current operation `Paused`. Dopo il resume verifica che machine e operation siano `Running`, che la current operation sia la stessa e che il progress sia conservato.

## resolve_warnings

Risolve soltanto warning macchina non bloccanti presenti nello snapshot Live.
Non risolve mai blocking alarm.

Endpoint usati:

```text
GET  /api/machines/{machineId}/live-snapshot
POST /api/alarms/{alarmId}/resolve
GET  /api/machines/{machineId}/live-snapshot
```

Payload resolve:

```json
{
  "resolutionNotes": "Resolved from resolve_warnings.py."
}
```

Esempi:

```bash
./scripts/resolve-warnings.sh --machine-id M-001
./scripts/resolve-warnings.sh --machine-id M-001 --alarm-id <id> --non-interactive
./scripts/resolve-warnings.sh \
  --machine-id M-001 \
  --all \
  --resolution-notes "Resolved during smoke test." \
  --non-interactive
```

In modalita' interattiva mostra i warning non bloccanti `Active` o `Acknowledged` con id, code, message, status e raisedAt; poi chiede se risolverne uno o tutti.

In modalita' `--non-interactive` serve `--alarm-id` oppure `--all`. Se l'id indicato appartiene a un blocking alarm, lo script termina con errore senza chiamare resolve.

## Modalita' Interattiva E CLI

`create_live_demo.py` senza `--non-interactive` chiede i valori principali mostrando i default tra parentesi. Invio accetta il default. I booleani accettano `s/n`, `si/no`, `y/n`.

Con `--non-interactive` non viene aperto nessun prompt; lo script usa gli argomenti passati e i default documentati.

## Errori Comuni

- API non raggiungibile: controlla `--base-url` e che il progetto API sia avviato.
- Macchina occupata: lo start puo' fallire con `422 Business rule violation` se il runtime e' gia' assegnato.
- Workpiece gia' terminale: lo start da sequence number puo' fallire se il workpiece scelto e' `Completed`, `Failed`, `Cancelled` o `Skipped`.
- Macchina non `Faulted`: `recover_fault_scenario.py` richiede una macchina in fault con current operation presente.
- Nessuna operation corrente: pause/resume/recover richiedono `currentOperation` nello snapshot Live.
- Operation non `Running`: `pause_current_operation.py` puo' mettere in pausa solo operation `Running`.
- Operation non `Paused`: `resume_current_operation.py` puo' riprendere solo operation `Paused`.
- Macchina `Faulted`: risolvere prima l'allarme bloccante con `recover_fault_scenario.py`.
- Piu' allarmi ambigui: usare la modalita' interattiva per scegliere quale allarme risolvere.
- Alarm bloccante passato a `resolve_warnings.py`: lo script rifiuta l'operazione e non invia resolve.
- `422 Business rule violation`: il client stampa status, title, detail e `traceId` quando presenti nel `problem+json`.
