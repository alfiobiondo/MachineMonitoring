import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { LiveSnapshotApi } from '../api/live-snapshot.api';
import { LiveSnapshot } from '../models/live-snapshot.model';
import { LivePageStore } from './live-page.store';

describe('LivePageStore', () => {
  let store: LivePageStore;
  let api: {
    getByMachineId: ReturnType<typeof vi.fn>;
  };

  const snapshot: LiveSnapshot = {
    machine: {
      id: 'machine-1',
      name: 'Machine 1',
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
        LivePageStore,
        {
          provide: LiveSnapshotApi,
          useValue: api,
        },
      ],
    });

    store = TestBed.inject(LivePageStore);
  });

  afterEach(() => {
    store.destroy();
  });

  it('should expose the loaded snapshot', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('machine-1');

    expect(api.getByMachineId).toHaveBeenCalledWith('machine-1');
    expect(store.snapshot()).toEqual(snapshot);
    expect(store.loading()).toBe(false);
    expect(store.errorMessage()).toBeNull();
    expect(store.hasProductionContext()).toBe(true);
  });

  it('should expose loading while the request is pending', () => {
    const request = new Subject<LiveSnapshot>();

    api.getByMachineId.mockReturnValue(request.asObservable());

    store.load('machine-1');

    expect(store.loading()).toBe(true);
    expect(store.snapshot()).toBeNull();

    request.next(snapshot);
    request.complete();

    expect(store.loading()).toBe(false);
    expect(store.snapshot()).toEqual(snapshot);
  });

  it('should expose a specific message for a missing machine', () => {
    const error = new HttpErrorResponse({
      status: 404,
      statusText: 'Not Found',
    });

    api.getByMachineId.mockReturnValue(throwError(() => error));

    store.load('missing-machine');

    expect(store.snapshot()).toBeNull();
    expect(store.loading()).toBe(false);
    expect(store.errorMessage()).toBe('Macchina "missing-machine" non trovata.');
  });

  it('should report when the backend cannot be reached', () => {
    const error = new HttpErrorResponse({
      status: 0,
      statusText: 'Unknown Error',
    });

    api.getByMachineId.mockReturnValue(throwError(() => error));

    store.load('machine-1');

    expect(store.errorMessage()).toBe('Impossibile raggiungere il backend.');
  });

  it('should cancel the previous request when another machine is loaded', () => {
    let firstRequestUnsubscribed = false;

    const firstRequest = new Observable<LiveSnapshot>(() => {
      return () => {
        firstRequestUnsubscribed = true;
      };
    });

    api.getByMachineId.mockReturnValueOnce(firstRequest).mockReturnValueOnce(of(snapshot));

    store.load('machine-1');
    store.load('machine-2');

    expect(firstRequestUnsubscribed).toBe(true);
    expect(api.getByMachineId).toHaveBeenNthCalledWith(1, 'machine-1');
    expect(api.getByMachineId).toHaveBeenNthCalledWith(2, 'machine-2');
  });

  it('should not reload the same machine when a snapshot is already available', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('machine-1');
    store.load('machine-1');

    expect(api.getByMachineId).toHaveBeenCalledTimes(1);
  });

  it('should reload the same machine when force is true', () => {
    api.getByMachineId.mockReturnValue(of(snapshot));

    store.load('machine-1');
    store.load('machine-1', true);

    expect(api.getByMachineId).toHaveBeenCalledTimes(2);
  });
});
