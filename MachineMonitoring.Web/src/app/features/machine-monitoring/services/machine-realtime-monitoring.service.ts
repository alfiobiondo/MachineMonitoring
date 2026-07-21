import { DestroyRef, inject, Injectable } from '@angular/core';

import { SignalrConnectionService } from '../../../core/realtime/signalr-connection.service';
import {
  MachineAlarmChangedEvent,
  MachineRuntimeChangedEvent,
} from '../models/machine-realtime-event.model';
import { MachineSnapshotStore } from '../state/machine-snapshot.store';

@Injectable()
export class MachineRealtimeMonitoringService {
  private readonly connection = inject(SignalrConnectionService);
  private readonly snapshotStore = inject(MachineSnapshotStore);
  private readonly destroyRef = inject(DestroyRef);

  private readonly removeListeners: Array<() => void> = [];

  constructor() {
    this.removeListeners.push(
      this.connection.on<MachineAlarmChangedEvent>('alarmChanged', (event) => {
        console.info('SignalR alarmChanged received.', event);

        this.snapshotStore.applyAlarmChanged(event);
      }),
    );

    this.removeListeners.push(
      this.connection.on<MachineRuntimeChangedEvent>('machineRuntimeChanged', (event) => {
        console.info('SignalR machineRuntimeChanged received.', event);

        this.snapshotStore.applyRuntimeChanged(event);
      }),
    );

    this.destroyRef.onDestroy(() => {
      for (const removeListener of this.removeListeners) {
        removeListener();
      }

      this.removeListeners.length = 0;

      void this.connection.disconnect().catch((error: unknown) => {
        console.error('Unable to disconnect the SignalR connection.', error);
      });
    });
  }

  monitor(machineId: string): void {
    const normalizedMachineId = machineId.trim();

    if (!normalizedMachineId) {
      return;
    }

    void this.connection.connect(normalizedMachineId).catch((error: unknown) => {
      console.error(`Unable to connect to SignalR for machine ${normalizedMachineId}.`, error);
    });
  }
}
