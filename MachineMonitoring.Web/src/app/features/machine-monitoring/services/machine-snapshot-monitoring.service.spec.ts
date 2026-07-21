import { Component, inject, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { MachineSnapshotStore } from '../state/machine-snapshot.store';
import {
  MACHINE_SNAPSHOT_POLLING_INTERVAL_MS,
  MachineSnapshotMonitoringService,
} from './machine-snapshot-monitoring.service';

describe('MachineSnapshotMonitoringService', () => {
  @Component({
    template: '',
    providers: [MachineSnapshotMonitoringService],
  })
  class CoordinatorHost {
    readonly service = inject(MachineSnapshotMonitoringService);
  }

  let fixture: ComponentFixture<CoordinatorHost>;
  let load: ReturnType<typeof vi.fn>;
  let destroy: ReturnType<typeof vi.fn>;

  function configure(platformId: 'browser' | 'server') {
    load = vi.fn();
    destroy = vi.fn();

    TestBed.configureTestingModule({
      imports: [CoordinatorHost],
      providers: [
        {
          provide: PLATFORM_ID,
          useValue: platformId,
        },
        {
          provide: MachineSnapshotStore,
          useValue: {
            load,
            destroy,
          },
        },
      ],
    });

    fixture = TestBed.createComponent(CoordinatorHost);
    fixture.detectChanges();
  }

  afterEach(() => {
    fixture?.destroy();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('should load the initial machine and avoid duplicate loads for the same machine', () => {
    configure('server');

    fixture.componentInstance.service.monitor('M-001');
    fixture.componentInstance.service.monitor('M-001');

    expect(load).toHaveBeenCalledOnce();
    expect(load).toHaveBeenCalledWith('M-001', { force: true });
  });

  it('should load the new machine when the machineId changes', () => {
    configure('server');

    fixture.componentInstance.service.monitor('M-001');
    fixture.componentInstance.service.monitor('M-002');

    expect(load).toHaveBeenNthCalledWith(1, 'M-001', { force: true });
    expect(load).toHaveBeenNthCalledWith(2, 'M-002', { force: true });
  });

  it('should not start polling during server rendering', () => {
    const setIntervalSpy = vi.spyOn(window, 'setInterval');

    configure('server');

    fixture.componentInstance.service.monitor('M-001');

    expect(
      setIntervalSpy.mock.calls.some(
        (call) => call[1] === MACHINE_SNAPSHOT_POLLING_INTERVAL_MS,
      ),
    ).toBe(false);
  });

  it('should start one browser polling timer and refresh the current machine silently', async () => {
    let pollingCallback: VoidFunction = () => undefined;
    const setIntervalSpy = vi
      .spyOn(window, 'setInterval')
      .mockImplementation((callback: TimerHandler, timeout?: number) => {
        if (timeout === MACHINE_SNAPSHOT_POLLING_INTERVAL_MS) {
          pollingCallback = callback as () => void;
        }

        return 1;
      });
    const clearIntervalSpy = vi.spyOn(window, 'clearInterval');

    configure('browser');

    fixture.componentInstance.service.monitor('M-001');
    fixture.componentInstance.service.monitor('M-001');
    await fixture.whenStable();

    expect(
      setIntervalSpy.mock.calls.filter(
        (call) => call[1] === MACHINE_SNAPSHOT_POLLING_INTERVAL_MS,
      ),
    ).toHaveLength(1);
    expect(setIntervalSpy).toHaveBeenCalledWith(
      expect.any(Function),
      MACHINE_SNAPSHOT_POLLING_INTERVAL_MS,
    );

    pollingCallback();

    expect(load).toHaveBeenCalledWith('M-001', {
      force: true,
      silent: true,
    });

    fixture.componentInstance.service.monitor('M-002');
    pollingCallback();

    expect(load).toHaveBeenCalledWith('M-002', {
      force: true,
      silent: true,
    });

    fixture.destroy();

    expect(clearIntervalSpy).toHaveBeenCalled();
    expect(destroy).toHaveBeenCalled();
  });
});
