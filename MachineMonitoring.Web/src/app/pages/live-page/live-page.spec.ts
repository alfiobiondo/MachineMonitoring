import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { LiveSnapshot } from '../../features/live/models/live-snapshot.model';
import { LivePageStore } from '../../features/live/state/live-page.store';
import { LivePage } from './live-page';

describe('LivePage', () => {
  let fixture: ComponentFixture<LivePage>;
  let load: ReturnType<typeof vi.fn>;
  let destroy: ReturnType<typeof vi.fn>;

  const liveSnapshot: LiveSnapshot = {
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

  beforeEach(async () => {
    load = vi.fn();
    destroy = vi.fn();

    const snapshot = signal<LiveSnapshot | null>(liveSnapshot);
    const loading = signal(false);
    const errorMessage = signal<string | null>(null);

    await TestBed.configureTestingModule({
      imports: [LivePage],
    })
      .overrideComponent(LivePage, {
        remove: {
          providers: [LivePageStore],
        },
        add: {
          providers: [
            {
              provide: LivePageStore,
              useValue: {
                load,
                destroy,
                snapshot,
                loading,
                errorMessage,
                hasProductionContext: computed(() => true),
                activeAlarms: computed(() => liveSnapshot.activeAlarms),
                activeAlarmCount: computed(() => 1),
                blockingAlarms: computed(() => liveSnapshot.activeAlarms),
                blockingAlarmCount: computed(() => 1),
                hasActiveAlarms: computed(() => true),
                hasBlockingAlarms: computed(() => true),
                machineStatusLabel: computed(() => 'Running'),
              },
            },
          ],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(LivePage);
    fixture.componentRef.setInput('machineId', 'M-001');
    fixture.detectChanges();
  });

  it('should load the requested machine', () => {
    expect(load).toHaveBeenCalledWith('M-001');
  });

  it('should render runtime and blocking alarm information', () => {
    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Laser M-001');
    expect(element.textContent).toContain('Macchina con allarme bloccante');
    expect(element.textContent).toContain('Bloccante');
    expect(element.textContent).toContain('AL-001');
    expect(element.textContent).toContain('Safety door is open.');
    expect(element.textContent).toContain('Running');
  });
});
