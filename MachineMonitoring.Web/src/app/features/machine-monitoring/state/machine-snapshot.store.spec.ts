import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { MachineSnapshotApi } from '../api/machine-snapshot.api';
import { MachineSnapshot } from '../models/machine-snapshot.model';
import { MachineSnapshotStore } from './machine-snapshot.store';
import { MachineOperationChangedEvent } from '../models/machine-realtime-event.model';

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
    warnings: [],
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

  it('should expose an error when the initial request fails', () => {
    const initialError = new HttpErrorResponse({
      status: 503,
      statusText: 'Service Unavailable',
    });

    api.getByMachineId.mockReturnValue(throwError(() => initialError));

    store.load('M-001');

    expect(store.snapshot()).toBeNull();
    expect(store.loading()).toBe(false);
    expect(store.errorMessage()).toBe('Non è stato possibile caricare lo stato Live.');
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

    api.getByMachineId.mockReturnValueOnce(firstRequest).mockReturnValueOnce(of(machine2Snapshot));

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

  it('should expose active warnings and a unified notification timeline sorted by timestamp', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'alarm-1',
            code: 'AL-001',
            severity: 'Critical',
            status: 'Active',
            message: 'Door interlock is open.',
            isBlocking: true,
            raisedAt: '2026-07-20T11:59:00Z',
          },
        ],
        warnings: [
          {
            id: 'M-001:OrphanRunningOperation:operation-2',
            machineId: 'M-001',
            code: 'OrphanRunningOperation',
            severity: 'Warning',
            title: 'Operation running non assegnata',
            message: 'Operation operation-2 running non allineata.',
            detectedAt: '2026-07-20T12:01:00Z',
            resolvedAt: null,
            isActive: true,
            sourceId: 'operation-2',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(store.hasActiveWarnings()).toBe(true);
    expect(store.activeWarningCount()).toBe(1);
    expect(store.notifications()).toEqual([
      expect.objectContaining({
        kind: 'warning',
        title: 'Operation running non assegnata',
        timestamp: '2026-07-20T12:01:00Z',
      }),
      expect.objectContaining({
        kind: 'alarm',
        title: 'AL-001',
        timestamp: '2026-07-20T11:59:00Z',
      }),
    ]);
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

  it('should apply a realtime change to the current operation', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');

    const event: MachineOperationChangedEvent = {
      eventId: 'event-1',
      changeKind: 'progress',
      operationId: 'operation-1',
      workpieceId: 'workpiece-1',
      machineId: 'M-001',
      sequenceNumber: 2,
      type: 'LaserCutting',
      status: 'Running',
      progressPercentage: 80,
      currentPhase: 'Finishing',
      failureReason: null,
      createdAt: '2026-07-20T11:50:00Z',
      startedAt: '2026-07-20T11:55:00Z',
      completedAt: null,
      occurredAt: '2026-07-20T12:00:02Z',
    };

    store.applyOperationChanged(event);

    expect(store.snapshot()?.currentOperation).toEqual(
      expect.objectContaining({
        id: 'operation-1',
        status: 'Running',
        progressPercentage: 80,
        currentPhase: 'Finishing',
        startedAt: '2026-07-20T11:55:00Z',
      }),
    );

    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
  });

  it('should ignore an operation change for another machine', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');

    const event: MachineOperationChangedEvent = {
      eventId: 'event-2',
      changeKind: 'progress',
      operationId: 'operation-1',
      workpieceId: 'workpiece-1',
      machineId: 'M-002',
      sequenceNumber: 2,
      type: 'LaserCutting',
      status: 'Running',
      progressPercentage: 90,
      currentPhase: 'Finishing',
      failureReason: null,
      createdAt: '2026-07-20T11:50:00Z',
      startedAt: '2026-07-20T11:55:00Z',
      completedAt: null,
      occurredAt: '2026-07-20T12:00:03Z',
    };

    store.applyOperationChanged(event);

    expect(store.snapshot()?.currentOperation).toEqual(snapshot.currentOperation);

    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
  });

  it('should silently reload the snapshot when another operation changes', () => {
    const refreshRequest = new Subject<MachineSnapshot>();

    api.getByMachineId
      .mockReturnValueOnce(of(snapshot))
      .mockReturnValueOnce(refreshRequest.asObservable());

    store.load('M-001');

    const event: MachineOperationChangedEvent = {
      eventId: 'event-3',
      changeKind: 'status',
      operationId: 'operation-2',
      workpieceId: 'workpiece-1',
      machineId: 'M-001',
      sequenceNumber: 3,
      type: 'Drilling',
      status: 'Running',
      progressPercentage: 0,
      currentPhase: 'Preparing',
      failureReason: null,
      createdAt: '2026-07-20T12:00:00Z',
      startedAt: '2026-07-20T12:01:00Z',
      completedAt: null,
      occurredAt: '2026-07-20T12:01:01Z',
    };

    store.applyOperationChanged(event);

    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
    expect(api.getByMachineId).toHaveBeenLastCalledWith('M-001');
    expect(store.snapshot()).toEqual(snapshot);
    expect(store.refreshing()).toBe(true);

    refreshRequest.next({
      ...snapshot,
      currentOperation: {
        id: 'operation-2',
        type: 'Drilling',
        status: 'Running',
        sequenceNumber: 3,
        position: 3,
        totalOperations: 4,
        progressPercentage: 0,
        currentPhase: 'Preparing',
        startedAt: '2026-07-20T12:01:00Z',
      },
      snapshotAt: '2026-07-20T12:01:02Z',
    });
    refreshRequest.complete();

    expect(store.refreshing()).toBe(false);
    expect(store.snapshot()?.currentOperation?.id).toBe('operation-2');
  });
});
