# MachineMonitoring — Istruzioni per Codex

## Scopo del repository

Questo repository è contemporaneamente:

1. un progetto didattico per imparare C# e .NET in modo pratico;
2. una simulazione realistica di un backend industriale per il monitoraggio di macchine e produzione;
3. una preparazione al futuro lavoro dell'utente in BLM, dove saranno rilevanti Angular, C#/.NET, API REST, database relazionali e pratiche enterprise.

Codex deve quindi trattare il progetto come software reale, ma preservarne sempre il valore didattico.

## Profilo dello sviluppatore

Lo sviluppatore ha esperienza professionale soprattutto frontend con React e TypeScript.

Conosce già:

- componenti e separazione delle responsabilità;
- TypeScript, tipi, interfacce e generics di base;
- chiamate HTTP e gestione dello stato frontend;
- npm, Git, VS Code e terminale;
- concetti generali di applicazioni web.

Sta imparando C# e .NET. Ha completato corsi introduttivi e intermedi di C#, ma alcuni concetti backend e .NET devono ancora essere consolidati.

Quando utile, Codex può creare collegamenti con TypeScript/React, ma deve evitare analogie forzate o fuorvianti.

## Obiettivo didattico

Le modifiche devono aiutare lo sviluppatore a comprendere i concetti realmente utili per lavorare su backend .NET enterprise.

Privilegiare:

- codice leggibile e intenzionale;
- spiegazioni legate al codice concreto;
- piccoli passaggi verificabili;
- comprensione delle responsabilità tra layer;
- ragionamento sulle scelte progettuali;
- strumenti e convenzioni usati davvero nei progetti .NET.

Evitare:

- refactoring estesi non richiesti dal task corrente;
- astrazioni premature;
- design pattern introdotti solo per mostrarli;
- dettagli accademici non utili al passo corrente;
- soluzioni “magiche” senza spiegazione;
- modifiche simultanee a molti concetti nuovi, salvo quando un requisito
  esplicito richiede un intervento coordinato e verificato tramite build e test.

Quando il task richiede esplicitamente un refactoring trasversale, Codex può
modificare più progetti e concetti coordinati, ma deve:

1. preservare le modifiche locali;
2. procedere per checkpoint interni;
3. mantenere il modello compilabile appena possibile;
4. eseguire build e test durante il lavoro, non soltanto alla fine;
5. riportare chiaramente le decisioni adottate.

## Metodo di lavoro richiesto

Prima di modificare il codice:

1. leggere i file coinvolti e la struttura della solution;
2. ricostruire il flusso attuale;
3. individuare il più piccolo passo coerente con l'obiettivo;
4. segnalare eventuali assunzioni importanti.

Durante il lavoro:

1. procedere per incrementi piccoli;
2. mantenere il progetto compilabile quando possibile;
3. non riscrivere file non necessari;
4. rispettare lo stile già presente;
5. spiegare il motivo delle modifiche, non soltanto il risultato;
6. distinguere chiaramente tra requisito, scelta progettuale e preferenza stilistica.

Dopo le modifiche:

1. eseguire almeno `dotnet build` sulla solution o sui progetti interessati;
2. eseguire i test pertinenti con `dotnet test`, quando presenti;
3. riportare con precisione cosa è stato modificato;
4. indicare cosa dovrebbe osservare o provare lo sviluppatore;
5. proporre un solo passo successivo naturale, senza anticipare intere sezioni del corso.

## Stile delle spiegazioni

Usare italiano chiaro e concreto.

Quando si introduce un concetto nuovo, seguire preferibilmente questa sequenza:

1. problema che il concetto risolve;
2. comportamento nel codice attuale;
3. modifica proposta;
4. spiegazione delle righe importanti;
5. verifica pratica;
6. breve collegamento al lavoro reale in un backend .NET.

Non dare per scontato che termini come lifetime, tracking, aggregate, migration, dependency inversion o unit of work siano già consolidati. Spiegarli quando diventano rilevanti, senza trasformare ogni risposta in una lezione teorica completa.

Quando si usa una sintassi C# non immediata, spiegare brevemente elementi come:

- `where T : ...`;
- nullable reference types;
- `record`, `class` e `struct`;
- proprietà `init`, `private set` e getter-only;
- `async` / `await` e `CancellationToken`;
- LINQ e deferred execution;
- `using` e `await using`;
- pattern matching;
- expression-bodied members;
- generics e variance, solo quando effettivamente presenti.

## Stato e struttura attuale del progetto

La solution si chiama `MachineMonitoring` ed è organizzata attualmente così:

```text
MachineMonitoring/
├── MachineMonitoring.Api/
├── MachineMonitoring.Application/
├── MachineMonitoring.Console/
├── MachineMonitoring.Domain/
├── MachineMonitoring.Infrastructure/
├── MachineMonitoring.Tests/
├── AGENTS.md
└── MachineMonitoring.slnx
```

Questa struttura è parte dello stato corrente del repository. Codex deve comunque verificarla sul filesystem, perché contenuti e file interni possono evolvere.

### Domain

Contiene il modello di dominio e le regole di business.

Entità e concetti già presenti o pianificati includono:

- `ProductionLot`;
- `Workpiece`;
- `MachineOperation`;
- `MachineOperationType`;
- `MachineOperationStatus`;
- geometrie come `Tube` e `Sheet`;
- `Material`;
- `Nozzle`;
- `DrawingFile`;
- `MachineCapabilities`;
- eccezioni di dominio.

Il Domain non deve dipendere da Infrastructure, database, EF Core, console, HTTP o dettagli esterni.

Le entità devono proteggere le proprie invarianti. Evitare setter pubblici soltanto per facilitare la persistenza.

### Application

Contiene casi d'uso, orchestrazione e contratti necessari all'applicazione.

Componenti già presenti o citati:

- `MachineManager`;
- `MachineFormatter`;
- `MachineDiagnosticService`;
- `MachineOperationApplicationService`;
- `ProductionDemoService`;
- interfacce di repository e provider;
- DTO o modelli applicativi quando necessari.

Application può dipendere da Domain, ma non deve conoscere dettagli concreti di PostgreSQL, file JSON o framework UI.

### Infrastructure

Contiene implementazioni concrete e integrazioni esterne.

Tecnologie e componenti attuali includono:

- PostgreSQL tramite Postgres.app;
- Entity Framework Core 10;
- provider Npgsql;
- `MachineMonitoringDbContext`;
- migration EF Core;
- repository PostgreSQL per cataloghi e produzione;
- provider JSON;
- caching con `IMemoryCache`.

Lo stato esatto di repository, record EF, migration e mapping deve sempre
essere verificato sul filesystem prima di intervenire.

### Api

`MachineMonitoring.Api` è già presente nella solution ed è il progetto ASP.NET Core Web API.

Non trattare quindi l'introduzione delle API come un'attività futura: prima di proporre o applicare modifiche, leggere sempre i file reali del progetto Api, in particolare `Program.cs`, configurazione, endpoint/controller già presenti e riferimenti di progetto.

Responsabilità attese del progetto Api:

- esporre via HTTP i casi d'uso definiti in Application;
- fungere da composition root per richieste web;
- registrare servizi applicativi, repository e `DbContext` con lifetime compatibili con la request;
- trasformare input e output HTTP in DTO, senza esporre automaticamente le entità di dominio;
- mappare in modo consapevole errori di dominio e applicativi su status code HTTP;
- usare operazioni asincrone e propagare `CancellationToken` dagli endpoint;
- non contenere regole di business che appartengono a Domain o Application.

Quando si lavora sulle API, verificare prima quale stile è già adottato nel repository, ad esempio Minimal API o controller. Non introdurre un secondo stile senza una motivazione esplicita.

Per ogni endpoint considerare separatamente:

- route e verbo HTTP;
- modello di request;
- validazione;
- chiamata al caso d'uso applicativo;
- modello di response;
- status code di successo e di errore;
- comportamento in caso di risorsa inesistente, conflitto o input non valido;
- eventuale accesso al database e numero di query.

Evitare endpoint che accedano direttamente a `MachineMonitoringDbContext` quando esiste già un servizio applicativo o un repository appropriato. L'Api deve coordinare HTTP, non sostituire Application.

### Console / Host

`MachineMonitoring.Console` è un secondo progetto eseguibile e usa il Generic Host di .NET. Non confonderlo con il progetto Api.

Concetti già affrontati:

- dependency injection;
- configuration;
- Options pattern e `ValidateOnStart`;
- logging con `Microsoft.Extensions.Logging`;
- hosted service / worker;
- polling con `PeriodicTimer`;
- cancellazione;
- retry selettivo;
- concorrenza con `SemaphoreSlim`;
- report e diagnostica delle macchine.

Configurazione di esempio già usata:

- intervallo di polling di circa 3 secondi;
- ritardo iniziale di circa 2 secondi;
- cinque macchine;
- diagnostica non disponibile per `M-003`;
- riepilogo degli stati calcolato dall'applicazione.

### Test

Sono presenti o previsti test con xUnit.

Favorire test su:

- regole di dominio;
- transizioni di stato;
- validazione degli input;
- comportamento dei servizi applicativi;
- casi limite significativi.

Evitare test che verifichino dettagli interni irrilevanti o che richiedano mock eccessivi.

## Regole architetturali

Rispettare queste direzioni di dipendenza:

```text
Api/Console -> Infrastructure -> Application -> Domain
Api/Console --------------------> Application -> Domain
Infrastructure -----------------> Domain
```

Più precisamente:

- Domain non dipende da nessun altro progetto della solution;
- Application dipende da Domain;
- Infrastructure implementa contratti definiti nei layer interni;
- il composition root registra le dipendenze concrete;
- il codice di business non deve recuperare servizi direttamente da `IServiceProvider`;
- evitare service locator e dipendenze statiche globali;
- passare il tempo tramite un'astrazione solo quando serve davvero a testare comportamento temporale;
- non creare interfacce per ogni classe senza una motivazione concreta.

## Convenzioni C# e .NET

Seguire le convenzioni già presenti nel repository. In assenza di indicazioni diverse:

- nullable reference types abilitati;
- implicit usings secondo la configurazione dei progetti;
- nomi pubblici in PascalCase;
- variabili e parametri in camelCase;
- interfacce con prefisso `I`;
- operazioni asincrone con suffisso `Async`;
- usare `CancellationToken` nelle operazioni I/O o di lunga durata;
- preferire `DateTimeOffset` a `DateTime` per timestamp applicativi;
- usare `Guid` per gli identificatori già modellati in questo modo;
- validare gli argomenti ai confini appropriati;
- non catturare `Exception` genericamente salvo che al confine dell'applicazione per logging e gestione controllata;
- non ignorare eccezioni senza una motivazione esplicita;
- evitare `async void` salvo event handler;
- evitare `.Result` e `.Wait()` su task;
- preferire metodi brevi con una responsabilità riconoscibile;
- aggiungere commenti solo per spiegare il perché, non per ripetere il codice.

## Entity Framework Core e PostgreSQL

Quando si lavora con EF Core:

- non esporre direttamente `DbContext` al Domain;
- usare `AsNoTracking()` per letture realmente read-only;
- mantenere tracking quando l'entità verrà modificata e salvata nello stesso unit of work;
- evitare chiamate ripetute al database dentro cicli quando una singola query può bastare;
- usare API asincrone (`ToListAsync`, `SingleOrDefaultAsync`, `SaveChangesAsync`, ecc.);
- propagare il `CancellationToken`;
- non inserire automaticamente `Include` senza verificare i dati necessari;
- controllare il SQL e il numero di query quando una scelta può avere impatto;
- creare migration esplicite per modifiche di schema;
- non modificare migration già applicate se non durante una fase chiaramente sperimentale;
- distinguere seed di sviluppo, dati iniziali obbligatori e dati di test;
- non memorizzare credenziali reali nel repository;
- usare configurazione e user secrets o variabili d'ambiente per le connection string.

Quando viene introdotto un repository, spiegare:

- quale dipendenza nasconde;
- perché serve al caso d'uso;
- quali operazioni espone;
- perché non deve diventare un generico CRUD universale per tutte le entità.

## Dependency Injection e lifetime

Usare i lifetime con intenzione:

- `Singleton`: un'istanza per l'intera applicazione, solo per servizi thread-safe e senza stato scoped;
- `Scoped`: un'istanza per scope, tipicamente `DbContext` e servizi che partecipano allo stesso caso d'uso;
- `Transient`: una nuova istanza a ogni risoluzione, per servizi leggeri e senza stato condiviso.

Non iniettare servizi scoped direttamente in singleton. In un `BackgroundService`, creare uno scope tramite `IServiceScopeFactory` quando occorre usare `DbContext` o repository scoped.

Ogni volta che si modifica una registrazione DI, verificare:

- interfaccia e implementazione;
- lifetime;
- grafo delle dipendenze;
- thread safety;
- eventuale captive dependency.

Poiché esistono due composition root, controllare sempre dove va applicata la registrazione:

- `MachineMonitoring.Api/Program.cs` per il processo web e gli scope HTTP;
- `MachineMonitoring.Console/Program.cs` o equivalente per worker e demo console.

Non presumere che una registrazione presente in uno dei due host sia automaticamente disponibile nell'altro. Quando utile, valutare un metodo di estensione condiviso in Infrastructure o Application, ma soltanto se riduce duplicazione reale senza nascondere troppo il percorso didattico.

## Logging

Usare logging strutturato:

```csharp
logger.LogInformation(
    "Machine {MachineId} completed operation {OperationId}",
    machineId,
    operationId);
```

Evitare interpolazione di stringhe nei messaggi di log quando i valori devono essere proprietà strutturate.

Livelli indicativi:

- `Trace` / `Debug`: dettagli diagnostici molto granulari;
- `Information`: eventi normali e significativi del flusso;
- `Warning`: situazione anomala ma gestibile;
- `Error`: operazione fallita;
- `Critical`: applicazione o componente non può continuare correttamente.

Non registrare password, token, connection string o dati sensibili.

## Concorrenza e background processing

Quando si lavora sul worker:

- rispettare `stoppingToken`;
- non usare `Task.Delay` senza cancellazione;
- gestire correttamente la durata di `PeriodicTimer`;
- evitare esecuzioni sovrapposte involontarie;
- usare `SemaphoreSlim` soltanto con un limite di concorrenza motivato;
- rilasciare sempre il semaforo in un blocco `finally`;
- distinguere errori transitori da errori permanenti;
- applicare retry soltanto a operazioni idempotenti o rese sicure;
- evitare retry infiniti e senza backoff;
- non condividere `DbContext` tra thread o operazioni concorrenti.

## Scope didattico dei prossimi passi

La direzione generale del percorso deve essere ricostruita dallo stato reale del repository prima di ogni intervento. Al momento `MachineMonitoring.Api` esiste già, quindi non va proposta come tecnologia ancora da introdurre.

Passi plausibili, da confermare leggendo il codice corrente, sono:

1. consolidare gli endpoint e il flusso Api -> Application -> Infrastructure -> Domain già presenti;
2. completare o rifinire la persistenza PostgreSQL per i concetti ancora mancanti;
3. comprendere bene repository, scope DI, lifetime per-request ed EF Core tracking;
4. aggiungere validazione delle request, mapping degli errori e risposte HTTP coerenti;
5. aggiungere test di dominio, applicativi e di integrazione delle API;
6. persistere progressivamente il modello Production;
7. collegare in seguito un frontend Angular;
8. affrontare Docker, CI/CD e concetti Azure quando il progetto sarà pronto.

Codex non deve saltare direttamente ai passi avanzati se il task corrente riguarda una fase precedente.

## Comandi utili

Prima di usare un comando, individuare il file solution effettivo (`.sln` o `.slnx`) e i nomi reali dei progetti.

Comandi tipici:

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project <percorso-progetto-eseguibile>
dotnet ef migrations add <NomeMigration> --project <Infrastructure> --startup-project <Console-o-Api>
dotnet ef database update --project <Infrastructure> --startup-project <Console-o-Api>
```

Non inventare percorsi o nomi di progetto: verificarli nel repository.

## Gestione delle modifiche

Prima di creare nuovi file o tipi, controllare se esistono già concetti equivalenti.

Non rinominare API pubbliche o spostare file su larga scala senza necessità.

Non modificare automaticamente:

- file di configurazione locale contenenti segreti;
- migration esistenti già utilizzate;
- versioni dei package;
- target framework;
- struttura della solution;
- naming consolidato del dominio.

Queste modifiche sono consentite solo quando il task le richiede esplicitamente e dopo averne spiegato l'impatto.

## Formato desiderato per le risposte di Codex

Per task non banali, concludere con sezioni simili a queste:

### Cosa ho cambiato

Breve elenco dei file e delle responsabilità modificate.

### Perché

Spiegazione concreta della scelta e del concetto .NET coinvolto.

### Verifica

Comandi eseguiti e relativo esito.

### Cosa osservare

Uno o due punti che lo sviluppatore dovrebbe leggere, eseguire o provare personalmente.

Non generare una lunga lista di possibili miglioramenti non richiesti.

## Regola fondamentale

Il miglior risultato non è soltanto codice funzionante. È codice funzionante che lo sviluppatore riesce a comprendere, spiegare e modificare autonomamente dopo il passaggio di Codex.

## Registro decisioni

Questa sezione serve come memoria operativa del progetto.

Aggiornare il registro quando emergono scelte stabili che avranno impatto
anche nelle sessioni successive.

Inserire qui:

- decisioni architetturali concordate;
- vincoli di dominio stabiliti;
- convenzioni pratiche adottate;
- incompatibilità note o punti da non reintrodurre;
- comandi o verifiche ricorrenti solo se hanno valore stabile per il progetto.

Non inserire:

- ogni singolo comando esplorativo;
- tentativi temporanei;
- note rumorose che non aiutano il lavoro futuro.

### Decisioni recenti

#### 2026-07-16 - Health checks API e PostgreSQL

- Gli health checks sono stati separati in due endpoint:
  - `/health/live` per verificare che il processo web sia vivo;
  - `/health/ready` per verificare che l'applicazione sia pronta, inclusa la raggiungibilità di PostgreSQL.
- Il controllo della raggiungibilità di PostgreSQL appartiene a `Infrastructure`.
  Il formato HTTP della risposta appartiene a `Api`.
- `HealthCheckResponseWriter` deve quindi restare nel progetto `MachineMonitoring.Api`.
- `PostgreSqlHealthCheck` deve restare nel progetto `MachineMonitoring.Infrastructure`.
- I pacchetti `Microsoft.Extensions.*` aggiunti per gli health checks vanno mantenuti allineati con le versioni già usate nella solution, per evitare errori NuGet `NU1605`.

#### 2026-07-16 - Regola per l'uso futuro di AGENTS.md

- Da questo punto in poi `AGENTS.md` va aggiornato quando una scelta è stabile e utile anche per sessioni future.
- Non usare `AGENTS.md` come log completo del terminale.
- Preferire note brevi ma durevoli, orientate a dominio, architettura, flusso di lavoro e compatibilità.

#### 2026-07-16 - Stato attuale del refactor produttivo

- Il refactor richiesto del modello produttivo non è ancora implementato.
- Il repository contiene già `ProductionLot` e `Workpiece` nel dominio, ma non sono ancora integrati realmente in persistenza, application service, API e worker.
- Il comportamento attuale del simulatore è in conflitto con il refactor desiderato:
  - seleziona automaticamente la prossima `MachineOperation` in stato `Queued`;
  - la avvia in automatico;
  - questo comportamento non dovrà essere mantenuto nel nuovo modello.
- Nel refactor futuro il worker non dovrà mai avviare automaticamente operazioni `Queued`.
  Dovrà avanzare soltanto operazioni `Running` e avviare la successiva solo quando il `Workpiece` risulterà attivato come sequenza.

#### 2026-07-16 - Refactor produttivo implementato

- La gerarchia produttiva adottata è:
  `ProductionLot -> Workpiece -> MachineOperation`.
- `MachineOperation` ora ha `SequenceNumber` obbligatorio, maggiore di zero.
- L'ordine tecnologico delle operazioni dello stesso `Workpiece` non deve più dipendere da `CreatedAt`.
  Per le regole di sequenza e per le query dedicate si usa `SequenceNumber`.
- Lo start singolo di una `MachineOperation`:
  - avvia solo l'operazione indicata;
  - non attiva `Workpiece.IsSequenceActive`;
  - è consentito solo se tutte le operazioni precedenti del pezzo sono `Completed`.
- Lo start di `Workpiece` attiva `IsSequenceActive` solo su quel pezzo e avvia soltanto la prima operazione `Queued` eseguibile.
- Lo start di `ProductionLot` attiva la sequenza di tutti i `Workpiece` non terminali del lotto e avvia, per ciascuno, solo la prima operazione eseguibile.
- Il coordinamento tra più entità produttive non deve vivere dentro `MachineOperation`.
  È stato introdotto un coordinatore applicativo dedicato: `ProductionSequenceService`.
- `Failed` o `Cancelled` di una `MachineOperation` bloccano la sequenza del relativo `Workpiece`.
- Il worker produttivo non deve mai avviare automaticamente operazioni `Queued`.
  Deve leggere solo operazioni `Running` e avanzarle di un solo step per ciclo.
- La persistenza PostgreSQL del modello produttivo ora richiede:
  - tabella `production_lots`;
  - tabella `workpieces`;
  - relazione `production_lots 1 -> N workpieces`;
  - relazione `workpieces 1 -> N machine_operations`;
  - vincolo univoco su `(workpiece_id, sequence_number)`.
- La migration incrementale del refactor deve preservare i dati legacy:
  - crea un lotto legacy;
  - crea un workpiece legacy per ogni `WorkpieceId` già presente;
  - conserva gli stessi `Guid` dei workpiece legacy;
  - assegna `SequenceNumber` ordinando per `CreatedAt` e poi `Id`.

#### 2026-07-16 - Gerarchia produttiva e avvio sequenziale

- La gerarchia produttiva è:
  - `ProductionLot`;
  - `Workpiece`;
  - `MachineOperation`.
- Ogni `MachineOperation` appartiene a un solo `Workpiece`.
- Ogni `Workpiece` appartiene a un solo `ProductionLot`.
- Ogni operazione possiede un `SequenceNumber` positivo.
- `SequenceNumber` deve essere univoco all'interno dello stesso
  `Workpiece`.
- L'ordine tecnologico delle operazioni è determinato da
  `SequenceNumber`, non da `CreatedAt`.

##### Start di una singola operazione

- Lo start puntuale avvia esclusivamente l'operazione richiesta.
- Non attiva la sequenza automatica del `Workpiece`.
- Al completamento non deve partire automaticamente l'operazione successiva.
- Lo start è consentito solo quando tutte le operazioni precedenti dello
  stesso `Workpiece` sono `Completed`.
- Il tentativo di avvio fuori sequenza costituisce una violazione di una
  regola di business e deve essere tradotto dall'API in HTTP 422.

##### Start di un Workpiece

- Lo start di un `Workpiece` attiva la sequenza del solo pezzo.
- Deve avviare soltanto la prima operazione eseguibile.
- Dopo il completamento di un'operazione deve partire soltanto la successiva
  dello stesso `Workpiece`.
- Non deve influenzare gli altri pezzi appartenenti allo stesso lotto.
- Non devono mai esistere due operazioni `Running` dello stesso
  `Workpiece`.

##### Start di un ProductionLot

- Lo start di un `ProductionLot` attiva tutti i suoi `Workpiece` non
  terminali.
- Per ogni `Workpiece` deve partire soltanto la prima operazione eseguibile.
- Le operazioni dello stesso pezzo restano strettamente sequenziali.
- Pezzi differenti dello stesso lotto possono avanzare indipendentemente.

##### Worker e avanzamento

- Il worker non deve mai trasformare autonomamente una generica operazione
  `Queued` in `Running`.
- Deve avanzare soltanto operazioni già in stato `Running`.
- Deve effettuare un singolo incremento per ciclo e rileggere lo stato nei
  cicli successivi.
- Deve ignorare operazioni `Queued`, `Paused`, `Completed`, `Failed` e
  `Cancelled`.
- Quando un'operazione raggiunge il completamento, la successiva può partire
  soltanto se la sequenza del relativo `Workpiece` è attiva.

##### Fallimenti, cancellazioni e completamenti

- Un'operazione `Failed` o `Cancelled` blocca la sequenza del relativo
  `Workpiece`.
- Nessuna operazione successiva deve partire automaticamente.
- Gli altri `Workpiece` dello stesso lotto possono continuare.
- Il completamento dell'ultima operazione completa automaticamente il
  `Workpiece`.
- Il completamento di tutti i `Workpiece` completa automaticamente il
  `ProductionLot`.
- Quando tutti i pezzi sono terminali e almeno uno non è stato completato
  con successo, il lotto deve terminare in uno stato di fallimento coerente
  con gli enum implementati.

##### Attivazione della sequenza

- L'attivazione automatica della sequenza deve essere rappresentata
  esplicitamente sul `Workpiece`, inizialmente tramite
  `IsSequenceActive`.
- Lo stato dell'operazione da solo non è sufficiente a distinguere uno start
  puntuale dallo start dell'intera sequenza.

#### Sicurezza del working tree

- Prima di un intervento esteso, eseguire `git status`.
- Non usare autonomamente:
  - `git reset --hard`;
  - `git clean`;
  - checkout o restore distruttivi;
  - rebase;
  - amend;
  - force push.
- Non eliminare modifiche locali non correlate al task.
- Non creare commit salvo richiesta esplicita.
- Al termine lasciare il diff revisionabile.

#### Migration con dati produttivi esistenti

Quando una nuova foreign key o una colonna obbligatoria viene aggiunta a
tabelle che possono già contenere dati:

1. non assumere che il database sia vuoto;
2. progettare un backfill esplicito;
3. aggiungere vincoli `NOT NULL`, foreign key e indici univoci soltanto dopo
   il backfill;
4. verificare sia il caso database vuoto sia quello con dati preesistenti;
5. non applicare automaticamente modifiche distruttive ai dati;
6. fermarsi se non è possibile garantire la conservazione dei dati.

#### Verifiche EF Core per migration manuali

- Se una migration viene scritta o rifinita manualmente, verificare sempre
  anche il relativo file `Designer` e il
  `MachineMonitoringDbContextModelSnapshot`.
- Se il model snapshot non rappresenta il modello corrente, i test che
  eseguono `Database.MigrateAsync()` possono fallire con
  `PendingModelChangesWarning`, anche quando il codice compila.
- Prima di considerare valida una migration manuale, eseguire un controllo EF
  che confermi l'assenza di differenze ulteriori tra modello e snapshot,
  senza aggiornare il database principale di sviluppo.

#### Verifiche del refactoring produttivo

- La proprietà `SequenceNumber` è l'unico ordinamento affidabile delle
  operazioni all'interno di un `Workpiece`: `CreatedAt` può servire al
  backfill iniziale, ma non deve guidare la sequenza a regime.
- Le query globali non devono più selezionare o avviare automaticamente una
  generica operazione `Queued`.
- Il worker produttivo deve leggere soltanto operazioni `Running` e lasciare
  a `ProductionSequenceService` la decisione di avvio della successiva.
- I casi d'uso che coordinano più entità del flusso produttivo devono passare
  tramite una transazione applicativa esplicita
  (`IProductionTransactionManager`).
- I test API con PostgreSQL sono parte della validazione del refactoring,
  perché esercitano migration, EF Core e repository reali tramite
  Testcontainers.

#### 2026-07-17 - Eventi operazione, allarmi macchina e start parziali

- Lo storico delle `MachineOperation` è persistente e append-only tramite la
  tabella `machine_operation_events`.
- Gli eventi di transizione significativi vengono aggiunti, non aggiornati o
  riscritti.
- Gli eventi minimi tracciati sono:
  `Created`, `Started`, `Paused`, `Resumed`, `Faulted`, `Recovered`,
  `Completed`, `Failed`, `Cancelled`, `Skipped`.
- Gli allarmi macchina sono un concetto separato dagli eventi operazione e
  vivono nella tabella `machine_alarms`.
- Un `MachineAlarm` può riferirsi a una `MachineOperation` specifica oppure
  soltanto a una macchina.
- `MachineOperationStatus.Faulted` rappresenta un errore recuperabile:
  - conserva progresso e fase correnti;
  - blocca temporaneamente la sequenza del `Workpiece`;
  - non disattiva automaticamente `Workpiece.IsSequenceActive`;
  - non fallisce immediatamente il `Workpiece`.
- La risoluzione dell'allarme associato porta l'operazione da `Faulted` a
  `Paused`.
- Dopo il recovery l'operazione non riparte automaticamente: serve il normale
  comando `Resume`.
- Dopo `Resolve -> Paused -> Resume -> Complete`, l'avvio automatico della
  successiva dipende dallo stato precedente della sequenza:
  - se il `Workpiece` era stato avviato come sequenza attiva, la successiva
    `Queued` può partire automaticamente;
  - se lo start era puntuale e `IsSequenceActive` era `false`, la successiva
    deve restare `Queued`.
- `Failed` resta terminale e non recuperabile.
- `Skipped` indica un elemento escluso intenzionalmente da uno start
  parziale:
  - una `MachineOperation` `Skipped` non deve più essere eseguita;
  - un `Workpiece` `Skipped` conta come terminale per il lotto, ma non come
    pezzo completato con successo.
- `Workpiece.SequenceNumber` è obbligatorio, positivo e univoco nel relativo
  `ProductionLot`.
- Lo start parziale di `Workpiece` da `StartFromSequenceNumber`:
  - marca `Skipped` le operazioni precedenti ancora `Queued`;
  - registra per ciascuna un evento `Skipped`;
  - avvia solo la sequenza richiesta;
  - considera i predecessori `Skipped` come già superati ai fini del vincolo
    di sequenza.
- Lo start parziale di `ProductionLot` da
  `StartFromWorkpieceSequenceNumber`:
  - marca `Skipped` i `Workpiece` precedenti ancora `Pending`;
  - marca `Skipped` le loro operazioni `Queued`;
  - registra gli eventi delle operazioni saltate;
  - attiva solo il sottoinsieme di pezzi dal numero richiesto in poi.
- La velocità del simulatore produttivo passa tramite
  `IOperationProgressStrategy`, con implementazione reale casuale ma
  verificabile nei test tramite fake deterministiche.
- La configurazione `OperationSimulator` deve rispettare esplicitamente la
  relazione `MaximumProgressIncrement >= MinimumProgressIncrement`.
- La migration `AddOperationEventsAndMachineAlarms` deve fare il backfill di
  `workpieces.sequence_number` per lotto con ordinamento `CreatedAt`, `Id`
  prima di applicare `NOT NULL` e indici univoci.
