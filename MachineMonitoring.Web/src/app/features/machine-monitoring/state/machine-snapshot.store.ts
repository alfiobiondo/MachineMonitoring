import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { MachineSnapshotApi } from '../api/machine-snapshot.api';
import {
  MachineNotificationItem,
  MachineSnapshot,
  MachineSnapshotAlarm,
  MachineSnapshotWarning,
} from '../models/machine-snapshot.model';
import {
  MachineOperationChangedEvent,
  MachineAlarmChangedEvent,
  MachineRuntimeChangedEvent,
} from '../models/machine-realtime-event.model';

export const MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS = 250;

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
  private silentRefreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly currentMachineId = this.currentMachineIdState.asReadonly();
  readonly snapshot = this.snapshotState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly refreshing = this.refreshingState.asReadonly();
  readonly errorMessage = this.errorMessageState.asReadonly();
  readonly activeAlarms = computed<readonly MachineSnapshotAlarm[]>(
    () => this.snapshotState()?.activeAlarms ?? [],
  );
  readonly activeWarnings = computed<readonly MachineSnapshotWarning[]>(() =>
    (this.snapshotState()?.warnings ?? []).filter((warning) => warning.isActive),
  );
  readonly activeAlarmCount = computed(() => this.activeAlarms().length);
  readonly activeWarningCount = computed(() => this.activeWarnings().length);
  readonly blockingAlarms = computed(() => this.activeAlarms().filter((alarm) => alarm.isBlocking));
  readonly blockingAlarmCount = computed(() => this.blockingAlarms().length);
  readonly hasActiveAlarms = computed(() => this.activeAlarmCount() > 0);
  readonly hasActiveWarnings = computed(() => this.activeWarningCount() > 0);
  readonly hasBlockingAlarms = computed(() => this.blockingAlarmCount() > 0);
  readonly notifications = computed<readonly MachineNotificationItem[]>(() => {
    const snapshot = this.snapshotState();

    if (snapshot === null) {
      return [];
    }

    return [
      ...snapshot.activeAlarms.map<MachineNotificationItem>((alarm) => ({
        id: `alarm:${alarm.id}`,
        machineId: snapshot.machine.id,
        kind: 'alarm',
        severity: alarm.severity,
        title: alarm.code,
        message: alarm.message,
        timestamp: alarm.raisedAt,
        isActive: true,
        sourceId: alarm.id,
      })),
      ...snapshot.warnings
        .filter((warning) => warning.isActive)
        .map<MachineNotificationItem>((warning) => ({
          id: `warning:${warning.id}`,
          machineId: warning.machineId,
          kind: 'warning',
          severity: warning.severity,
          title: warning.title,
          message: warning.message,
          timestamp: warning.detectedAt,
          isActive: warning.isActive,
          sourceId: warning.sourceId ?? warning.id,
        })),
    ].sort((left, right) => right.timestamp.localeCompare(left.timestamp));
  });
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

    if (shouldRefreshAfterRuntimeStatus(event.status)) {
      this.scheduleSilentRefresh();
    }
  }

  applyOperationChanged(event: MachineOperationChangedEvent): void {
    const currentMachineId = this.currentMachineIdState();
    const currentSnapshot = this.snapshotState();

    if (currentMachineId !== event.machineId || currentSnapshot === null) {
      return;
    }

    const currentOperation = currentSnapshot.currentOperation;

    if (currentOperation === null || currentOperation.id !== event.operationId) {
      this.scheduleSilentRefresh();

      return;
    }

    this.snapshotState.set({
      ...currentSnapshot,
      currentOperation: {
        ...currentOperation,
        status: event.status,
        progressPercentage: event.progressPercentage,
        currentPhase: event.currentPhase,
        startedAt: event.startedAt,
      },
    });

    if (shouldRefreshAfterOperationStatus(event.status)) {
      this.scheduleSilentRefresh();
    }
  }

  destroy(): void {
    this.cancelActiveRequest();
    this.cancelScheduledSilentRefresh();
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

  private scheduleSilentRefresh(): void {
    const machineId = this.currentMachineIdState();

    if (machineId === null) {
      return;
    }

    if (this.silentRefreshTimer !== null) {
      clearTimeout(this.silentRefreshTimer);
    }

    this.silentRefreshTimer = setTimeout(() => {
      this.silentRefreshTimer = null;

      if (this.currentMachineIdState() !== machineId) {
        return;
      }

      this.load(machineId, {
        force: true,
        silent: true,
      });
    }, MACHINE_SNAPSHOT_REALTIME_RECONCILIATION_DELAY_MS);
  }

  private cancelScheduledSilentRefresh(): void {
    if (this.silentRefreshTimer === null) {
      return;
    }

    clearTimeout(this.silentRefreshTimer);
    this.silentRefreshTimer = null;
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
      warnings: currentSnapshot.warnings,
    };
  }
}

function shouldRefreshAfterOperationStatus(status: string): boolean {
  return ['Completed', 'Failed', 'Cancelled', 'Skipped'].includes(status);
}

function shouldRefreshAfterRuntimeStatus(status: string): boolean {
  return ['Available', 'Faulted', 'Paused'].includes(status);
}
