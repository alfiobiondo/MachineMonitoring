import { isPlatformBrowser } from '@angular/common';
import {
  afterNextRender,
  inject,
  Injectable,
  Injector,
  NgZone,
  OnDestroy,
  PLATFORM_ID,
} from '@angular/core';

import { MachineSnapshotStore } from '../state/machine-snapshot.store';

export const MACHINE_SNAPSHOT_POLLING_INTERVAL_MS = 15_000;

@Injectable()
export class MachineSnapshotMonitoringService implements OnDestroy {
  private readonly injector = inject(Injector);
  private readonly ngZone = inject(NgZone);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly snapshotStore = inject(MachineSnapshotStore);

  private pollingTimerId: number | null = null;
  private pollingRequested = false;
  private currentMachineId: string | null = null;

  monitor(machineId: string): void {
    if (this.currentMachineId === machineId) {
      this.startPolling();
      return;
    }

    this.currentMachineId = machineId;
    this.snapshotStore.load(machineId, { force: true });
    this.startPolling();
  }

  stop(): void {
    this.pollingRequested = false;

    if (this.pollingTimerId !== null) {
      window.clearInterval(this.pollingTimerId);
      this.pollingTimerId = null;
    }
  }

  ngOnDestroy(): void {
    this.stop();
    this.snapshotStore.destroy();
  }

  private startPolling(): void {
    if (!isPlatformBrowser(this.platformId) || this.pollingRequested) {
      return;
    }

    this.pollingRequested = true;

    afterNextRender(
      () => {
        if (!this.pollingRequested || this.pollingTimerId !== null) {
          return;
        }

        this.ngZone.runOutsideAngular(() => {
          this.pollingTimerId = window.setInterval(() => {
            this.ngZone.run(() => this.refreshCurrentMachine());
          }, MACHINE_SNAPSHOT_POLLING_INTERVAL_MS);
        });
      },
      { injector: this.injector },
    );
  }

  private refreshCurrentMachine(): void {
    const machineId = this.currentMachineId;

    if (machineId === null) {
      return;
    }

    this.snapshotStore.load(machineId, {
      force: true,
      silent: true,
    });
  }
}
