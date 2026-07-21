import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { LiveSnapshotApi } from '../api/live-snapshot.api';
import { LiveSnapshot } from '../models/live-snapshot.model';

@Injectable()
export class LivePageStore {
  private readonly liveSnapshotApi = inject(LiveSnapshotApi);

  private readonly snapshotState = signal<LiveSnapshot | null>(null);
  private readonly loadingState = signal(false);
  private readonly errorMessageState = signal<string | null>(null);

  private activeRequest: Subscription | null = null;
  private currentMachineId: string | null = null;

  readonly snapshot = this.snapshotState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly errorMessage = this.errorMessageState.asReadonly();

  readonly hasProductionContext = computed(() => {
    const snapshot = this.snapshotState();

    return (
      snapshot?.productionLot !== null &&
      snapshot?.currentWorkpiece !== null &&
      snapshot?.currentOperation !== null
    );
  });

  load(machineId: string, force = false): void {
    const isSameMachine = this.currentMachineId === machineId;

    if (!force && isSameMachine && (this.loadingState() || this.snapshotState() !== null)) {
      return;
    }

    this.activeRequest?.unsubscribe();

    this.currentMachineId = machineId;
    this.snapshotState.set(null);
    this.loadingState.set(true);
    this.errorMessageState.set(null);

    this.activeRequest = this.liveSnapshotApi.getByMachineId(machineId).subscribe({
      next: (snapshot) => {
        this.snapshotState.set(snapshot);
        this.loadingState.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.snapshotState.set(null);
        this.errorMessageState.set(this.getErrorMessage(error, machineId));
        this.loadingState.set(false);
      },
    });
  }

  destroy(): void {
    this.activeRequest?.unsubscribe();
    this.activeRequest = null;
    this.currentMachineId = null;
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
}
