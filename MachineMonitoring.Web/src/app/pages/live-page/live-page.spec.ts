import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { MachineSnapshot } from '../../features/machine-monitoring/models/machine-snapshot.model';
import { MachineSnapshotStore } from '../../features/machine-monitoring/state/machine-snapshot.store';
import { LivePage } from './live-page';

describe('LivePage', () => {
  let fixture: ComponentFixture<LivePage>;
  let load: ReturnType<typeof vi.fn>;
  let destroy: ReturnType<typeof vi.fn>;
  let currentMachineIdState: ReturnType<typeof signal<string | null>>;
  let snapshotState: ReturnType<typeof signal<MachineSnapshot | null>>;
  let loadingState: ReturnType<typeof signal<boolean>>;
  let refreshingState: ReturnType<typeof signal<boolean>>;
  let errorMessageState: ReturnType<typeof signal<string | null>>;

  const liveSnapshot: MachineSnapshot = {
    machine: {
      id: 'M-001',
      name: 'Laser M-001',
      status: 'Running',
      lastChangedAt: '2026-07-21T08:30:00Z',
    },
    runtimeVersion: 7,
    productionLot: {
      id: 'lot-1',
      code: 'LOT-001',
      status: 'Running',
      progressPercentage: 50,
      completedOperations: 4,
      totalOperations: 8,
    },
    currentWorkpiece: {
      id: 'wp-1',
      code: 'WP-001',
      status: 'Running',
      sequenceNumber: 2,
      position: 2,
      totalWorkpieces: 5,
      progressPercentage: 35,
      completedOperations: 1,
      totalOperations: 3,
    },
    currentOperation: {
      id: 'op-1',
      type: 'LaserCutting',
      status: 'Running',
      sequenceNumber: 2,
      position: 2,
      totalOperations: 3,
      progressPercentage: 65,
      currentPhase: 'Cutting',
      startedAt: '2026-07-21T08:25:00Z',
    },
    activeAlarms: [
      {
        id: 'alarm-1',
        code: 'AL-001',
        severity: 'Critical',
        status: 'Active',
        message: 'Safety door is open.',
        isBlocking: true,
        raisedAt: '2026-07-21T08:31:00Z',
      },
    ],
    warnings: [],
    snapshotAt: '2026-07-21T08:32:00Z',
  };

  async function createComponent(options: {
    machineId?: string | null;
    snapshot?: MachineSnapshot | null;
    loading?: boolean;
    errorMessage?: string | null;
  } = {}) {
    load = vi.fn();
    destroy = vi.fn();

    currentMachineIdState = signal(options.machineId ?? 'M-001');
    snapshotState = signal<MachineSnapshot | null>(options.snapshot ?? liveSnapshot);
    loadingState = signal(options.loading ?? false);
    refreshingState = signal(false);
    errorMessageState = signal<string | null>(options.errorMessage ?? null);

    await TestBed.configureTestingModule({
      imports: [LivePage],
      providers: [
        {
          provide: MachineSnapshotStore,
          useValue: {
            load,
            destroy,
            currentMachineId: currentMachineIdState.asReadonly(),
            snapshot: snapshotState.asReadonly(),
            loading: loadingState.asReadonly(),
            refreshing: refreshingState.asReadonly(),
            errorMessage: errorMessageState.asReadonly(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LivePage);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await createComponent();
  });

  afterEach(() => {
    fixture.destroy();
  });

  it('should not start polling or load snapshots directly', () => {
    expect(load).not.toHaveBeenCalled();
  });

  it('should not destroy the shared machine snapshot store when the page is destroyed', () => {
    fixture.destroy();

    expect(destroy).not.toHaveBeenCalled();
  });

  it('should render the shared live production content', () => {
    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Macchina richiesta: M-001');
    expect(element.textContent).toContain('Lotto');
    expect(element.textContent).toContain('Pezzo');
    expect(element.textContent).toContain('Operazione');
    expect(element.textContent).toContain('Lavorazione bloccata');
    expect(element.textContent).toContain('Operazione 2 di 3');
    expect(element.textContent).toContain('Laser Cutting');
    expect(element.textContent).toContain('Cutting');
    expect(element.textContent).toContain('Running');
  });

  it('should show an informative state when no machine is selected', async () => {
    currentMachineIdState.set(null);
    snapshotState.set(null);
    errorMessageState.set(null);
    loadingState.set(false);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Seleziona una macchina per visualizzare lo stato Live.');
    expect(element.textContent).not.toContain('Errore');
  });

  it('should keep the three cards when the snapshot has no active production lot', () => {
    snapshotState.set({
      ...liveSnapshot,
      machine: {
        ...liveSnapshot.machine,
        status: 'Available',
      },
      productionLot: null,
      currentWorkpiece: null,
      currentOperation: null,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).not.toContain('Errore');
    expect(element.querySelectorAll('app-live-progress-card')).toHaveLength(3);
    expect(element.textContent).toContain('Macchina disponibile · Nessun contesto caricato');
    expect(element.textContent).toContain('Nessun lotto attivo');
    expect(element.textContent).toContain('Nessun pezzo attivo');
    expect(element.textContent).toContain('Nessuna operazione attiva');
    expect(element.textContent).not.toContain('In attesa di una lavorazione');
    expect(Array.from(element.querySelectorAll('[role="progressbar"]'))).toHaveLength(0);
    expect(element.textContent).not.toContain('0%');

    const operationCard = Array.from(element.querySelectorAll('app-live-progress-card'))[2];
    expect(operationCard.textContent).toContain('In attesa');
    expect(operationCard.textContent).not.toContain('Available');
    expect(operationCard.textContent).not.toContain('In attesa di una lavorazione');
  });

  it('should keep the three cards when the snapshot has no active workpiece', () => {
    snapshotState.set({
      ...liveSnapshot,
      currentWorkpiece: null,
      currentOperation: null,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).not.toContain('Errore');
    expect(element.querySelectorAll('app-live-progress-card')).toHaveLength(3);
    expect(element.textContent).toContain('LOT-001');
    expect(element.textContent).toContain('Nessun pezzo attivo');
    expect(element.textContent).toContain('Nessuna operazione attiva');
  });

  it('should keep the three cards when the snapshot has no active operation', () => {
    snapshotState.set({
      ...liveSnapshot,
      currentOperation: null,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).not.toContain('Errore');
    expect(element.querySelectorAll('app-live-progress-card')).toHaveLength(3);
    expect(element.textContent).toContain('LOT-001');
    expect(element.textContent).toContain('WP-001');
    expect(element.textContent).toContain('Nessuna operazione attiva');
  });

  it('should distinguish an existing entity with zero progress from an empty entity', () => {
    snapshotState.set({
      ...liveSnapshot,
      productionLot: {
        ...liveSnapshot.productionLot!,
        progressPercentage: 0,
      },
      currentWorkpiece: {
        ...liveSnapshot.currentWorkpiece!,
        progressPercentage: 0,
      },
      currentOperation: {
        ...liveSnapshot.currentOperation!,
        progressPercentage: 0,
      },
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelectorAll('[role="progressbar"]')).toHaveLength(3);
    expect(element.textContent?.match(/0%/g)).toHaveLength(3);
  });

  it('should not render fake phase or fake position text when operation data is missing', () => {
    snapshotState.set({
      ...liveSnapshot,
      currentOperation: {
        ...liveSnapshot.currentOperation!,
        currentPhase: null,
      },
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Operazione 2 di 3');
    expect(element.textContent).toContain('Laser Cutting');
    expect(element.textContent).not.toContain('Fase non disponibile');
  });

  it('should keep the global production status outside progress cards', () => {
    const element: HTMLElement = fixture.nativeElement;
    const globalStatus = element.querySelector('.live-page__production-context') as HTMLElement;
    const cards = Array.from(element.querySelectorAll('app-live-progress-card'));

    expect(globalStatus.textContent).toContain('Lavorazione bloccata');
    expect(cards.every((card) => !card.textContent?.includes('Lavorazione bloccata'))).toBe(true);
  });

  it('should show a global error only for a real initial load error', async () => {
    snapshotState.set(null);
    errorMessageState.set('Non è stato possibile caricare lo stato Live.');
    loadingState.set(false);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Errore');
    expect(element.textContent).toContain('Non è stato possibile caricare lo stato Live.');
  });

  it('should keep the live cards visible while a silent refresh is running', () => {
    refreshingState.set(true);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Lotto');
    expect(element.textContent).toContain('Pezzo');
    expect(element.textContent).toContain('Operazione');
  });
});
