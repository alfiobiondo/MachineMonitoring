import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { MachineSnapshotApi } from '../api/machine-snapshot.api';
import { MachineSnapshot } from '../models/machine-snapshot.model';
import { MachineSnapshotStore } from './machine-snapshot.store';

describe('MachineSnapshotStore', () => {
  let store: MachineSnapshotStore;
  let api: {
    getByMachineId: ReturnType<typeof vi.fn>;
  };

  const snapshot: MachineSnapshot = {
    machine: {
      id: 'M-001',
      name: 'Laser M-001',
      status: 'Running',
      lastChangedAt: '2026-07-20T12:00:00Z',
    },
    runtimeVersion: 3,
    productionLot: {
      id: 'lot-1',
      code: 'LOT-001',
      status: 'Running',
      progressPercentage: 50,
      completedOperations: 2,
      totalOperations: 4,
    },
    currentWorkpiece: {
      id: 'workpiece-1',
      code: 'WP-001',
      status: 'Running',
      sequenceNumber: 1,
      position: 1,
      totalWorkpieces: 2,
      progressPercentage: 50,
      completedOperations: 1,
      totalOperations: 2,
    },
    currentOperation: {
      id: 'operation-1',
      type: 'Cutting',
      status: 'Running',
      sequenceNumber: 2,
      position: 2,
      totalOperations: 4,
      progressPercentage: 60,
      currentPhase: 'Cutting',
      startedAt: '2026-07-20T11:55:00Z',
    },
    activeAlarms: [],
    snapshotAt: '2026-07-20T12:00:01Z',
  };

  beforeEach(() => {
    api = {
      getByMachineId: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        MachineSnapshotStore,
        {
          provide: MachineSnapshotApi,
          useValue: api,
        },
      ],
    });

    store = TestBed.inject(MachineSnapshotStore);
  });

  afterEach(() => {
    store.destroy();
  });

  it('should expose the initial state', () => {
    expect(store.currentMachineId()).toBeNull();
    expect(store.snapshot()).toBeNull();
    expect(store.loading()).toBe(false);
    expect(store.refreshing()).toBe(false);
    expect(store.errorMessage()).toBeNull();
  });

  it('should load the requested machine snapshot', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');

    expect(api.getByMachineId).toHaveBeenCalledWith('M-001');
    expect(store.currentMachineId()).toBe('M-001');
    expect(store.snapshot()).toEqual(snapshot);
    expect(store.loading()).toBe(false);
    expect(store.errorMessage()).toBeNull();
    expect(store.hasProductionContext()).toBe(true);
  });

  it('should expose loading while the initial request is pending', () => {
    const request = new Subject<MachineSnapshot>();

    api.getByMachineId.mockReturnValue(request.asObservable());

    store.load('M-001');

    expect(store.loading()).toBe(true);
    expect(store.snapshot()).toBeNull();

    request.next(snapshot);
    request.complete();

    expect(store.loading()).toBe(false);
    expect(store.snapshot()).toEqual(snapshot);
  });

  it('should keep the previous snapshot visible during a silent refresh', () => {
    const refreshRequest = new Subject<MachineSnapshot>();

    api.getByMachineId
      .mockReturnValueOnce(of(snapshot))
      .mockReturnValueOnce(refreshRequest.asObservable());

    store.load('M-001');
    store.load('M-001', { force: true, silent: true });

    expect(store.snapshot()).toEqual(snapshot);
    expect(store.loading()).toBe(false);
    expect(store.refreshing()).toBe(true);

    refreshRequest.next({
      ...snapshot,
      runtimeVersion: 4,
    });
    refreshRequest.complete();

    expect(store.refreshing()).toBe(false);
    expect(store.snapshot()?.runtimeVersion).toBe(4);
  });

  it('should keep the previous snapshot visible when a silent refresh fails', () => {
    const refreshError = new HttpErrorResponse({
      status: 503,
      statusText: 'Service Unavailable',
    });

    api.getByMachineId
      .mockReturnValueOnce(of(snapshot))
      .mockReturnValueOnce(throwError(() => refreshError));

    store.load('M-001');
    store.load('M-001', { force: true, silent: true });

    expect(store.snapshot()).toEqual(snapshot);
    expect(store.errorMessage()).toBeNull();
    expect(store.refreshing()).toBe(false);
  });

  it('should cancel the previous request when another machine is loaded', () => {
    let firstRequestUnsubscribed = false;
    const machine2Snapshot = {
      ...snapshot,
      machine: {
        ...snapshot.machine,
        id: 'M-002',
        name: 'Laser M-002',
      },
    };

    const firstRequest = new Observable<MachineSnapshot>(() => {
      return () => {
        firstRequestUnsubscribed = true;
      };
    });

    api.getByMachineId
      .mockReturnValueOnce(firstRequest)
      .mockReturnValueOnce(of(machine2Snapshot));

    store.load('M-001');
    store.load('M-002');

    expect(firstRequestUnsubscribed).toBe(true);
    expect(store.currentMachineId()).toBe('M-002');
    expect(store.snapshot()?.machine.id).toBe('M-002');
  });

  it('should not reload the same machine when a snapshot is already available', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.load('M-001');

    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
  });

  it('should skip duplicate silent refreshes while a request is pending', () => {
    const request = new Subject<MachineSnapshot>();

    api.getByMachineId.mockReturnValue(request.asObservable());

    store.load('M-001', { force: true, silent: true });
    store.load('M-001', { force: true, silent: true });

    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
  });

  it('should expose active alarms and blocking alarm counters', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'alarm-1',
            code: 'AL-001',
            severity: 'Warning',
            status: 'Active',
            message: 'Cooling pressure is low.',
            isBlocking: false,
            raisedAt: '2026-07-20T11:58:00Z',
          },
          {
            id: 'alarm-2',
            code: 'AL-002',
            severity: 'Critical',
            status: 'Active',
            message: 'Door interlock is open.',
            isBlocking: true,
            raisedAt: '2026-07-20T11:59:00Z',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(store.hasActiveAlarms()).toBe(true);
    expect(store.activeAlarmCount()).toBe(2);
    expect(store.hasBlockingAlarms()).toBe(true);
    expect(store.blockingAlarmCount()).toBe(1);
    expect(store.machineStatusLabel()).toBe('Running');
  });

  it('should clear active work on destroy without clearing the visible snapshot', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.destroy();

    expect(store.currentMachineId()).toBeNull();
    expect(store.loading()).toBe(false);
    expect(store.refreshing()).toBe(false);
    expect(store.snapshot()).toEqual(snapshot);
  });
});
