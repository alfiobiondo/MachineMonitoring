import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { MachineSnapshot } from '../../features/machine-monitoring/models/machine-snapshot.model';
import { MachineSnapshotStore } from '../../features/machine-monitoring/state/machine-snapshot.store';
import { LivePage } from './live-page';

describe('LivePage', () => {
  let fixture: ComponentFixture<LivePage>;
  let load: ReturnType<typeof vi.fn>;
  let destroy: ReturnType<typeof vi.fn>;
  let refreshingState: ReturnType<typeof signal<boolean>>;

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
    snapshotAt: '2026-07-21T08:32:00Z',
  };

  async function createComponent() {
    load = vi.fn();
    destroy = vi.fn();

    const currentMachineId = signal('M-001');
    const snapshot = signal<MachineSnapshot | null>(liveSnapshot);
    const loading = signal(false);
    refreshingState = signal(false);
    const errorMessage = signal<string | null>(null);

    await TestBed.configureTestingModule({
      imports: [LivePage],
      providers: [
        {
          provide: MachineSnapshotStore,
          useValue: {
            load,
            destroy,
            currentMachineId: currentMachineId.asReadonly(),
            snapshot: snapshot.asReadonly(),
            loading: loading.asReadonly(),
            refreshing: refreshingState.asReadonly(),
            errorMessage: errorMessage.asReadonly(),
            hasProductionContext: computed(() => true),
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
    expect(element.textContent).toContain('Running');
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
