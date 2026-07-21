import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { MachineSnapshotApi } from '../api/machine-snapshot.api';
import { MachineSnapshot, MachineSnapshotAlarm } from '../models/machine-snapshot.model';
import {
  MachineAlarmChangedEvent,
  MachineRuntimeChangedEvent,
} from '../models/machine-realtime-event.model';

export interface MachineSnapshotLoadOptions {
  force?: boolean;
  silent?: boolean;
}

@Injectable()
export class MachineSnapshotStore {
  private readonly machineSnapshotApi = inject(MachineSnapshotApi);

  private readonly currentMachineIdState = signal<string | null>(null);
  private readonly snapshotState = signal<MachineSnapshot | null>(null);
  private readonly loadingState = signal(false);
  private readonly refreshingState = signal(false);
  private readonly errorMessageState = signal<string | null>(null);

  private activeRequest: Subscription | null = null;
  private activeRequestMachineId: string | null = null;
  private requestVersion = 0;

  readonly currentMachineId = this.currentMachineIdState.asReadonly();
  readonly snapshot = this.snapshotState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly refreshing = this.refreshingState.asReadonly();
  readonly errorMessage = this.errorMessageState.asReadonly();
  readonly activeAlarms = computed<readonly MachineSnapshotAlarm[]>(
    () => this.snapshotState()?.activeAlarms ?? [],
  );
  readonly activeAlarmCount = computed(() => this.activeAlarms().length);
  readonly blockingAlarms = computed(() => this.activeAlarms().filter((alarm) => alarm.isBlocking));
  readonly blockingAlarmCount = computed(() => this.blockingAlarms().length);
  readonly hasActiveAlarms = computed(() => this.activeAlarmCount() > 0);
  readonly hasBlockingAlarms = computed(() => this.blockingAlarmCount() > 0);
  readonly machineStatusLabel = computed(
    () => this.snapshotState()?.machine.status ?? 'Non inizializzato',
  );

  readonly hasProductionContext = computed(() => {
    const snapshot = this.snapshotState();

    return (
      snapshot?.productionLot !== null &&
      snapshot?.currentWorkpiece !== null &&
      snapshot?.currentOperation !== null
    );
  });

  load(machineId: string, options: MachineSnapshotLoadOptions = {}): void {
    const { force = false, silent = false } = options;
    const isSameMachine = this.currentMachineIdState() === machineId;
    const hasPendingRequest = this.loadingState() || this.refreshingState();

    if (!force && isSameMachine && (hasPendingRequest || this.snapshotState() !== null)) {
      return;
    }

    if (silent && isSameMachine && hasPendingRequest) {
      return;
    }

    this.cancelActiveRequest();

    const requestVersion = ++this.requestVersion;
    this.currentMachineIdState.set(machineId);
    this.activeRequestMachineId = machineId;

    if (silent && isSameMachine) {
      this.refreshingState.set(true);
    } else {
      this.snapshotState.set(null);
      this.loadingState.set(true);
      this.refreshingState.set(false);
      this.errorMessageState.set(null);
    }

    this.activeRequest = this.machineSnapshotApi.getByMachineId(machineId).subscribe({
      next: (snapshot) => {
        if (this.isStaleResponse(machineId, requestVersion)) {
          return;
        }

        this.snapshotState.set(this.reconcileSnapshot(snapshot));

        this.loadingState.set(false);
        this.refreshingState.set(false);
        this.errorMessageState.set(null);
        this.activeRequest = null;
        this.activeRequestMachineId = null;
      },
      error: (error: HttpErrorResponse) => {
        if (this.isStaleResponse(machineId, requestVersion)) {
          return;
        }

        if (!silent) {
          this.snapshotState.set(null);
          this.errorMessageState.set(this.getErrorMessage(error, machineId));
        }

        this.loadingState.set(false);
        this.refreshingState.set(false);
        this.activeRequest = null;
        this.activeRequestMachineId = null;
      },
    });
  }

  applyAlarmChanged(event: MachineAlarmChangedEvent): void {
    const currentMachineId = this.currentMachineIdState();
    const currentSnapshot = this.snapshotState();

    if (currentMachineId !== event.machineId || currentSnapshot === null) {
      return;
    }

    if (event.changeKind === 'resolved') {
      this.snapshotState.set({
        ...currentSnapshot,
        activeAlarms: currentSnapshot.activeAlarms.filter((alarm) => alarm.id !== event.alarmId),
      });

      return;
    }

    const alarm: MachineSnapshotAlarm = {
      id: event.alarmId,
      code: event.code,
      severity: event.severity,
      status: event.status,
      message: event.message,
      isBlocking: event.isBlocking,
      raisedAt: event.raisedAt,
    };

    const existingAlarmIndex = currentSnapshot.activeAlarms.findIndex(
      (currentAlarm) => currentAlarm.id === alarm.id,
    );

    const activeAlarms =
      existingAlarmIndex >= 0
        ? currentSnapshot.activeAlarms.map((currentAlarm) =>
            currentAlarm.id === alarm.id ? alarm : currentAlarm,
          )
        : [alarm, ...currentSnapshot.activeAlarms];

    this.snapshotState.set({
      ...currentSnapshot,
      activeAlarms,
    });
  }

  applyRuntimeChanged(event: MachineRuntimeChangedEvent): void {
    const currentMachineId = this.currentMachineIdState();
    const currentSnapshot = this.snapshotState();

    if (currentMachineId !== event.machineId || currentSnapshot === null) {
      return;
    }

    const currentVersion = currentSnapshot.runtimeVersion;

    if (currentVersion !== null && event.version <= currentVersion) {
      return;
    }

    this.snapshotState.set({
      ...currentSnapshot,
      runtimeVersion: event.version,
      machine: {
        ...currentSnapshot.machine,
        status: event.status,
        lastChangedAt: event.lastChangedAt,
      },
    });
  }

  destroy(): void {
    this.cancelActiveRequest();
    this.requestVersion++;
    this.currentMachineIdState.set(null);
    this.loadingState.set(false);
    this.refreshingState.set(false);
  }

  private cancelActiveRequest(): void {
    this.activeRequest?.unsubscribe();
    this.activeRequest = null;
    this.activeRequestMachineId = null;
  }

  private isStaleResponse(machineId: string, requestVersion: number): boolean {
    return (
      this.currentMachineIdState() !== machineId ||
      this.activeRequestMachineId !== machineId ||
      this.requestVersion !== requestVersion
    );
  }

  private getErrorMessage(error: HttpErrorResponse, machineId: string): string {
    if (error.status === 404) {
      return `Macchina "${machineId}" non trovata.`;
    }

    if (error.status === 0) {
      return 'Impossibile raggiungere il backend.';
    }

    return 'Non è stato possibile caricare lo stato Live.';
  }

  private reconcileSnapshot(incomingSnapshot: MachineSnapshot): MachineSnapshot {
    const currentSnapshot = this.snapshotState();

    if (currentSnapshot === null) {
      return incomingSnapshot;
    }

    const currentVersion = currentSnapshot.runtimeVersion;
    const incomingVersion = incomingSnapshot.runtimeVersion;

    const incomingRuntimeIsOlder =
      currentVersion !== null && incomingVersion !== null && incomingVersion < currentVersion;

    if (!incomingRuntimeIsOlder) {
      return incomingSnapshot;
    }

    return {
      ...incomingSnapshot,
      machine: currentSnapshot.machine,
      runtimeVersion: currentSnapshot.runtimeVersion,
      activeAlarms: currentSnapshot.activeAlarms,
    };
  }
}
