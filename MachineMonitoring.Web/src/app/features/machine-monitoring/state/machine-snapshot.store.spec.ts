import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { MachineAlarmApi } from '../api/machine-alarm.api';
import { MachineSnapshotApi } from '../api/machine-snapshot.api';
import { MachineSnapshot } from '../models/machine-snapshot.model';
import {
  MachineOperationChangedEvent,
  MachineRuntimeChangedEvent,
} from '../models/machine-realtime-event.model';
import {
  MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS,
  MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS,
  MachineSnapshotStore,
} from './machine-snapshot.store';

describe('MachineSnapshotStore', () => {
  let store: MachineSnapshotStore;
  let api: {
    getByMachineId: ReturnType<typeof vi.fn>;
  };
  let alarmApi: {
    acknowledge: ReturnType<typeof vi.fn>;
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

  const completedSnapshot: MachineSnapshot = {
    ...snapshot,
    machine: {
      ...snapshot.machine,
      status: 'Available',
      lastChangedAt: '2026-07-20T12:00:10Z',
    },
    runtimeVersion: 4,
    productionLot: {
      ...snapshot.productionLot!,
      status: 'Completed',
      progressPercentage: 100,
      completedOperations: 1,
      totalOperations: 1,
    },
    currentWorkpiece: {
      ...snapshot.currentWorkpiece!,
      status: 'Completed',
      progressPercentage: 100,
      completedOperations: 1,
      totalOperations: 1,
    },
    currentOperation: {
      ...snapshot.currentOperation!,
      status: 'Completed',
      progressPercentage: 100,
      currentPhase: 'Finishing cut',
    },
    snapshotAt: '2026-07-20T12:00:11Z',
  };

  beforeEach(() => {
    api = {
      getByMachineId: vi.fn(),
    };
    alarmApi = {
      acknowledge: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        MachineSnapshotStore,
        {
          provide: MachineSnapshotApi,
          useValue: api,
        },
        {
          provide: MachineAlarmApi,
          useValue: alarmApi,
        },
      ],
    });

    store = TestBed.inject(MachineSnapshotStore);
  });

  afterEach(() => {
    store.destroy();
    vi.useRealTimers();
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

  it('should expose non-blocking alarms as warnings and blocking alarms as alarms', () => {
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
    expect(store.activeAlarmCount()).toBe(1);
    expect(store.hasActiveWarnings()).toBe(true);
    expect(store.activeWarningCount()).toBe(1);
    expect(store.hasBlockingAlarms()).toBe(true);
    expect(store.blockingAlarmCount()).toBe(1);
    expect(store.machineStatusLabel()).toBe('Running');
  });

  it('should classify two non-blocking machine alarms only as warnings', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'warning-alarm-1',
            code: 'SIM-WARN-TEMP',
            severity: 'Warning',
            status: 'Active',
            message: 'Temperature warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:02:00Z',
          },
          {
            id: 'warning-alarm-2',
            code: 'SIM-WARN-PRESSURE',
            severity: 'Warning',
            status: 'Active',
            message: 'Pressure warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:01:00Z',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(store.activeAlarmCount()).toBe(0);
    expect(store.activeWarningCount()).toBe(2);
    expect(store.notifications().map((notification) => notification.category)).toEqual([
      'warning',
      'warning',
    ]);
  });

  it('should classify two blocking machine alarms only as alarms', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'blocking-alarm-1',
            code: 'SIM-FAULT-DOOR',
            severity: 'Critical',
            status: 'Active',
            message: 'Door fault.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:02:00Z',
          },
          {
            id: 'blocking-alarm-2',
            code: 'SIM-FAULT-AXIS',
            severity: 'Critical',
            status: 'Active',
            message: 'Axis fault.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:01:00Z',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(store.activeAlarmCount()).toBe(2);
    expect(store.activeWarningCount()).toBe(0);
    expect(store.notifications().map((notification) => notification.category)).toEqual([
      'alarm',
      'alarm',
    ]);
  });

  it('should keep a mixed blocking and non-blocking alarm in mutually exclusive categories', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'blocking-alarm-1',
            code: 'SIM-FAULT-DOOR',
            severity: 'Critical',
            status: 'Active',
            message: 'Door fault.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:02:00Z',
          },
          {
            id: 'warning-alarm-1',
            code: 'SIM-WARN-TEMP',
            severity: 'Warning',
            status: 'Active',
            message: 'Temperature warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:01:00Z',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(store.activeAlarmCount()).toBe(1);
    expect(store.activeWarningCount()).toBe(1);

    const notificationCategoriesBySource = new Map(
      store.notifications().map((notification) => [notification.sourceId, notification.category]),
    );

    expect(notificationCategoriesBySource.get('blocking-alarm-1')).toBe('alarm');
    expect(notificationCategoriesBySource.get('warning-alarm-1')).toBe('warning');
  });

  it('should add snapshot warnings without duplicating machine alarms with the same source', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'warning-alarm-1',
            code: 'SIM-WARN-TEMP',
            severity: 'Warning',
            status: 'Active',
            message: 'Temperature warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:02:00Z',
          },
        ],
        warnings: [
          {
            id: 'warning-alarm-1',
            machineId: 'M-001',
            code: 'SIM-WARN-TEMP',
            severity: 'Warning',
            title: 'Temperature warning.',
            message: 'Duplicate projected warning.',
            detectedAt: '2026-07-20T12:02:00Z',
            resolvedAt: null,
            isActive: true,
            sourceId: 'warning-alarm-1',
          },
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

    expect(store.activeWarningCount()).toBe(2);
    expect(store.notifications().map((notification) => notification.sourceId)).toEqual([
      'warning-alarm-1',
      'operation-2',
    ]);
  });

  it('should not expose resolved alarms as active notifications', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'resolved-alarm-1',
            code: 'AL-RESOLVED',
            severity: 'Critical',
            status: 'Resolved',
            message: 'Resolved alarm.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:02:00Z',
          },
          {
            id: 'resolved-warning-1',
            code: 'WARN-RESOLVED',
            severity: 'Warning',
            status: 'Resolved',
            message: 'Resolved warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:01:00Z',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(store.activeAlarmCount()).toBe(0);
    expect(store.activeWarningCount()).toBe(0);
    expect(store.notifications()).toEqual([]);
  });

  it('should expose active warnings and a unified notification timeline sorted by timestamp', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'alarm-0',
            code: 'SIM-WARN-TEMP',
            severity: 'Warning',
            status: 'Active',
            message: 'Temperature warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:02:00Z',
          },
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
    expect(store.activeWarningCount()).toBe(2);
    expect(store.notifications()).toEqual([
      expect.objectContaining({
        kind: 'warning',
        title: 'SIM-WARN-TEMP',
        timestamp: '2026-07-20T12:02:00Z',
      }),
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

  it('should sort active notifications before acknowledged ones and then by raised time', () => {
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'acknowledged-newer',
            code: 'AL-ACK',
            severity: 'Critical',
            status: 'Acknowledged',
            message: 'Acknowledged alarm.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:05:00Z',
            acknowledgedAt: '2026-07-20T12:06:00Z',
          },
          {
            id: 'active-older',
            code: 'AL-ACTIVE-OLDER',
            severity: 'Critical',
            status: 'Active',
            message: 'Older active alarm.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:01:00Z',
          },
          {
            id: 'active-newer',
            code: 'WARN-ACTIVE-NEWER',
            severity: 'Warning',
            status: 'Active',
            message: 'Newer active warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:03:00Z',
          },
        ],
      }),
    );

    store.load('M-001');

    expect(
      store.notifications().map((notification) => ({
        sourceId: notification.sourceId,
        lifecycleStatus: notification.lifecycleStatus,
      })),
    ).toEqual([
      { sourceId: 'active-newer', lifecycleStatus: 'Active' },
      { sourceId: 'active-older', lifecycleStatus: 'Active' },
      { sourceId: 'acknowledged-newer', lifecycleStatus: 'Acknowledged' },
    ]);
  });

  it('should acknowledge an active notification without removing it from counts', () => {
    alarmApi.acknowledge.mockReturnValue(of(undefined));
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'warning-alarm-1',
            code: 'SIM-WARN-TEMP',
            severity: 'Warning',
            status: 'Active',
            message: 'Temperature warning.',
            isBlocking: false,
            raisedAt: '2026-07-20T12:02:00Z',
          },
        ],
      }),
    );

    store.load('M-001');
    store.acknowledgeAlarm('warning-alarm-1');

    expect(alarmApi.acknowledge).toHaveBeenCalledTimes(1);
    expect(alarmApi.acknowledge).toHaveBeenCalledWith('warning-alarm-1');
    expect(store.activeWarningCount()).toBe(1);
    expect(store.activeAlarmCount()).toBe(0);
    expect(store.notifications()[0]).toEqual(
      expect.objectContaining({
        sourceId: 'warning-alarm-1',
        lifecycleStatus: 'Acknowledged',
      }),
    );
    expect(store.loading()).toBe(false);
  });

  it('should not send duplicate acknowledge requests while one is pending', () => {
    const request = new Subject<void>();
    alarmApi.acknowledge.mockReturnValue(request.asObservable());
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
            raisedAt: '2026-07-20T12:02:00Z',
          },
        ],
      }),
    );

    store.load('M-001');
    store.acknowledgeAlarm('alarm-1');
    store.acknowledgeAlarm('alarm-1');

    expect(alarmApi.acknowledge).toHaveBeenCalledTimes(1);
    expect(store.acknowledgingAlarmIds()).toEqual(['alarm-1']);

    request.next();
    request.complete();

    expect(store.acknowledgingAlarmIds()).toEqual([]);
  });

  it('should not acknowledge an already acknowledged notification', () => {
    alarmApi.acknowledge.mockReturnValue(of(undefined));
    api.getByMachineId.mockReturnValue(
      of({
        ...snapshot,
        activeAlarms: [
          {
            id: 'alarm-1',
            code: 'AL-001',
            severity: 'Critical',
            status: 'Acknowledged',
            message: 'Door interlock is open.',
            isBlocking: true,
            raisedAt: '2026-07-20T12:02:00Z',
          },
        ],
      }),
    );

    store.load('M-001');
    store.acknowledgeAlarm('alarm-1');

    expect(alarmApi.acknowledge).not.toHaveBeenCalled();
    expect(store.notifications()[0].lifecycleStatus).toBe('Acknowledged');
  });

  it('should keep an active notification and avoid global loading when acknowledge fails', () => {
    alarmApi.acknowledge.mockReturnValue(throwError(() => new Error('network')));
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
            raisedAt: '2026-07-20T12:02:00Z',
          },
        ],
      }),
    );

    store.load('M-001');
    store.acknowledgeAlarm('alarm-1');

    expect(store.activeAlarmCount()).toBe(1);
    expect(store.notifications()[0].lifecycleStatus).toBe('Active');
    expect(store.alarmAcknowledgeError()).toBe(
      'Non è stato possibile riconoscere la segnalazione.',
    );
    expect(store.loading()).toBe(false);
    expect(store.refreshing()).toBe(false);
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

  it('should update non-terminal progress without forcing an immediate refresh', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 80,
      }),
    );

    expect(store.snapshot()?.currentOperation?.progressPercentage).toBe(80);
    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
  });

  it('should reconcile aggregate progress from a throttled silent refresh', () => {
    vi.useFakeTimers();
    api.getByMachineId
      .mockReturnValueOnce(of(snapshot))
      .mockReturnValueOnce(
        of({
          ...snapshot,
          productionLot: {
            ...snapshot.productionLot!,
            progressPercentage: 80,
          },
          currentWorkpiece: {
            ...snapshot.currentWorkpiece!,
            progressPercentage: 80,
          },
          currentOperation: {
            ...snapshot.currentOperation!,
            progressPercentage: 80,
          },
        }),
      );

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 80,
      }),
    );

    expect(store.snapshot()?.currentOperation?.progressPercentage).toBe(80);
    expect(store.snapshot()?.currentWorkpiece?.progressPercentage).toBe(50);
    expect(store.snapshot()?.productionLot?.progressPercentage).toBe(50);

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS);

    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
    expect(store.snapshot()?.currentOperation?.progressPercentage).toBe(80);
    expect(store.snapshot()?.currentWorkpiece?.progressPercentage).toBe(80);
    expect(store.snapshot()?.productionLot?.progressPercentage).toBe(80);
  });

  it('should reconcile progress within three hundred milliseconds', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 70,
      }),
    );

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS - 1);
    expect(api.getByMachineId).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(1);
    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
    expect(api.getByMachineId).toHaveBeenLastCalledWith('M-001');
  });

  it('should not request a progress reconciliation for every realtime increment in the same window', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 70,
      }),
    );
    vi.advanceTimersByTime(100);

    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 80,
      }),
    );
    vi.advanceTimersByTime(100);
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 90,
      }),
    );

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS - 200);

    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
  });

  it('should not postpone the pending progress reconciliation when new progress events arrive', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 70,
      }),
    );

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS - 100);
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 80,
      }),
    );

    vi.advanceTimersByTime(99);
    expect(api.getByMachineId).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(1);
    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
  });

  it('should allow a new progress reconciliation window after the previous GET', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 70,
      }),
    );
    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS);

    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 80,
      }),
    );

    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS);

    expect(api.getByMachineId).toHaveBeenCalledTimes(3);
  });

  it('should cancel a pending progress refresh when a terminal operation event arrives', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'progress',
        status: 'Running',
        progressPercentage: 80,
      }),
    );
    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS - 50);

    store.applyOperationChanged(
      createOperationEvent({
        changeKind: 'status',
        status: 'Completed',
        progressPercentage: 100,
      }),
    );

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);
    expect(api.getByMachineId).toHaveBeenCalledTimes(2);

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_PROGRESS_RECONCILIATION_DELAY_MS);
    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
  });

  it.each(['Completed', 'Failed', 'Cancelled', 'Skipped'])(
    'should schedule a silent refresh when operation status becomes %s',
    (status) => {
      vi.useFakeTimers();
      api.getByMachineId.mockReturnValue(of(snapshot));

      store.load('M-001');
      store.applyOperationChanged(createOperationEvent({ status }));

      expect(api.getByMachineId).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);

      expect(api.getByMachineId).toHaveBeenCalledTimes(2);
      expect(api.getByMachineId).toHaveBeenLastCalledWith('M-001');
      expect(store.loading()).toBe(false);
    },
  );

  it.each(['Available', 'Faulted', 'Paused'])(
    'should schedule a silent refresh when runtime status becomes %s',
    (status) => {
      vi.useFakeTimers();
      api.getByMachineId.mockReturnValue(of(snapshot));

      store.load('M-001');
      store.applyRuntimeChanged(createRuntimeEvent({ status, version: 4 }));

      expect(store.snapshot()?.machine.status).toBe(status);
      expect(api.getByMachineId).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);

      expect(api.getByMachineId).toHaveBeenCalledTimes(2);
      expect(api.getByMachineId).toHaveBeenLastCalledWith('M-001');
      expect(store.loading()).toBe(false);
    },
  );

  it('should debounce multiple realtime reconciliation requests into one silent refresh', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('M-001');
    store.applyOperationChanged(createOperationEvent({ status: 'Completed', progressPercentage: 100 }));
    vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS - 1);
    store.applyRuntimeChanged(createRuntimeEvent({ status: 'Available', version: 4 }));
    vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);

    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
    expect(api.getByMachineId).toHaveBeenLastCalledWith('M-001');
  });

  it('should keep the snapshot visible without global error or loading when a debounced silent refresh fails', () => {
    vi.useFakeTimers();
    const refreshError = new HttpErrorResponse({
      status: 503,
      statusText: 'Service Unavailable',
    });

    api.getByMachineId
      .mockReturnValueOnce(of(snapshot))
      .mockReturnValueOnce(throwError(() => refreshError));

    store.load('M-001');
    store.applyOperationChanged(createOperationEvent({ status: 'Completed', progressPercentage: 100 }));
    vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);

    expect(store.snapshot()).toEqual({
      ...snapshot,
      currentOperation: expect.objectContaining({
        status: 'Completed',
        progressPercentage: 100,
      }),
    });
    expect(store.errorMessage()).toBeNull();
    expect(store.loading()).toBe(false);
    expect(store.refreshing()).toBe(false);
  });

  it('should reconcile operation, workpiece, and production lot percentages from the next snapshot', () => {
    vi.useFakeTimers();
    api.getByMachineId.mockReturnValueOnce(of(snapshot)).mockReturnValueOnce(of(completedSnapshot));

    store.load('M-001');
    store.applyOperationChanged(createOperationEvent({ status: 'Completed', progressPercentage: 100 }));

    expect(store.snapshot()?.currentOperation?.progressPercentage).toBe(100);
    expect(store.snapshot()?.currentWorkpiece?.progressPercentage).toBe(50);
    expect(store.snapshot()?.productionLot?.progressPercentage).toBe(50);

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);

    expect(store.snapshot()?.currentOperation?.progressPercentage).toBe(100);
    expect(store.snapshot()?.currentWorkpiece?.progressPercentage).toBe(100);
    expect(store.snapshot()?.productionLot?.progressPercentage).toBe(100);
    expect(store.snapshot()?.currentWorkpiece?.status).toBe('Completed');
    expect(store.snapshot()?.productionLot?.status).toBe('Completed');
    expect(store.loading()).toBe(false);
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
    vi.useFakeTimers();
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

    expect(api.getByMachineId).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);

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

  function createOperationEvent(
    overrides: Partial<MachineOperationChangedEvent> = {},
  ): MachineOperationChangedEvent {
    return {
      eventId: 'event-operation',
      changeKind: 'status',
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
      ...overrides,
    };
  }

  function createRuntimeEvent(
    overrides: Partial<MachineRuntimeChangedEvent> = {},
  ): MachineRuntimeChangedEvent {
    return {
      eventId: 'event-runtime',
      machineId: 'M-001',
      status: 'Running',
      currentOperationId: 'operation-1',
      lastChangedAt: '2026-07-20T12:00:03Z',
      failureReason: null,
      activeAlarmId: null,
      version: 4,
      occurredAt: '2026-07-20T12:00:03Z',
      ...overrides,
    };
  }
});
