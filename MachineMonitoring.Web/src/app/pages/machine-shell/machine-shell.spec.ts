import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  provideRouter,
  Router,
  RouterOutlet,
  withComponentInputBinding,
} from '@angular/router';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { MachineSnapshotApi } from '../../features/machine-monitoring/api/machine-snapshot.api';
import { MachineSnapshot } from '../../features/machine-monitoring/models/machine-snapshot.model';
import { MACHINE_SNAPSHOT_POLLING_INTERVAL_MS } from '../../features/machine-monitoring/services/machine-snapshot-monitoring.service';
import { MachineShell } from './machine-shell';

describe('MachineShell', () => {
  @Component({
    selector: 'app-live-child',
    template: 'Live child mounted',
  })
  class LiveChild {
    ngOnDestroy = vi.fn();
  }

  @Component({
    selector: 'app-programming-child',
    template: 'Programming child mounted',
  })
  class ProgrammingChild {}

  @Component({
    selector: 'app-test-root',
    template: '<router-outlet />',
    imports: [RouterOutlet],
  })
  class TestRoot {
    readonly router = inject(Router);
  }

  let fixture: ComponentFixture<TestRoot>;
  let api: {
    getByMachineId: ReturnType<typeof vi.fn>;
  };

  const snapshot: MachineSnapshot = {
    machine: {
      id: 'M-001',
      name: 'Laser M-001',
      status: 'Running',
      lastChangedAt: '2026-07-21T08:30:00Z',
    },
    runtimeVersion: 7,
    productionLot: null,
    currentWorkpiece: null,
    currentOperation: null,
    activeAlarms: [],
    snapshotAt: '2026-07-21T08:32:00Z',
  };

  beforeEach(async () => {
    api = {
      getByMachineId: vi.fn().mockReturnValue(of(snapshot)),
    };

    await TestBed.configureTestingModule({
      imports: [TestRoot],
      providers: [
        provideRouter(
          [
            {
              path: 'machines/:machineId',
              component: MachineShell,
              children: [
                {
                  path: 'live',
                  component: LiveChild,
                },
                {
                  path: 'programming',
                  component: ProgrammingChild,
                },
              ],
            },
          ],
          withComponentInputBinding(),
        ),
        {
          provide: MachineSnapshotApi,
          useValue: api,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TestRoot);
  });

  afterEach(() => {
    fixture.destroy();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('should activate monitoring from the parent machine route and render the shell layout', async () => {
    await fixture.componentInstance.router.navigateByUrl('/machines/M-001/live');
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(api.getByMachineId).toHaveBeenCalledWith('M-001');
    expect(element.querySelector('app-header')).not.toBeNull();
    expect(element.querySelector('app-sidebar')).not.toBeNull();
    expect(element.textContent).toContain('Live child mounted');
  });

  it('should keep the same shell while navigating between child routes', async () => {
    await fixture.componentInstance.router.navigateByUrl('/machines/M-001/live');
    fixture.detectChanges();

    const shellDebugElement = fixture.debugElement.query(By.directive(MachineShell));
    const shellInstance = shellDebugElement.componentInstance;

    await fixture.componentInstance.router.navigateByUrl('/machines/M-001/programming');
    fixture.detectChanges();

    const nextShellInstance = fixture.debugElement.query(
      By.directive(MachineShell),
    ).componentInstance;

    expect(nextShellInstance).toBe(shellInstance);
    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Programming child mounted');
  });

  it('should keep polling active after LivePage is no longer mounted', async () => {
    let pollingCallback: VoidFunction = () => undefined;
    const setIntervalSpy = vi
      .spyOn(window, 'setInterval')
      .mockImplementation((callback: TimerHandler, timeout?: number) => {
        if (timeout === MACHINE_SNAPSHOT_POLLING_INTERVAL_MS) {
          pollingCallback = callback as () => void;
        }

        return 1;
      });

    await fixture.componentInstance.router.navigateByUrl('/machines/M-001/live');
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance.router.navigateByUrl('/machines/M-001/programming');
    fixture.detectChanges();

    expect(
      setIntervalSpy.mock.calls.filter(
        (call) => call[1] === MACHINE_SNAPSHOT_POLLING_INTERVAL_MS,
      ),
    ).toHaveLength(1);

    pollingCallback();

    expect(api.getByMachineId).toHaveBeenCalledWith('M-001');
    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
  });

  it('should switch monitoring when navigating to another machine', async () => {
    const machine2Snapshot = {
      ...snapshot,
      machine: {
        ...snapshot.machine,
        id: 'M-002',
        name: 'Laser M-002',
      },
    };

    api.getByMachineId
      .mockReturnValueOnce(of(snapshot))
      .mockReturnValueOnce(of(machine2Snapshot));

    await fixture.componentInstance.router.navigateByUrl('/machines/M-001/live');
    fixture.detectChanges();

    await fixture.componentInstance.router.navigateByUrl('/machines/M-002/live');
    fixture.detectChanges();

    expect(api.getByMachineId).toHaveBeenNthCalledWith(1, 'M-001');
    expect(api.getByMachineId).toHaveBeenNthCalledWith(2, 'M-002');
    expect(fixture.nativeElement.textContent).toContain('Laser M-002');
  });
});
