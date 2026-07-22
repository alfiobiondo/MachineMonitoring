import { Component, computed, effect, inject, input } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { AppHeader } from '../../core/layout/app-header/app-header';
import { AppSidebar } from '../../core/layout/app-sidebar/app-sidebar';
import { AppHeaderContext } from '../../core/layout/models/app-header-context.model';
import { SignalrConnectionService } from '../../core/realtime/signalr-connection.service';
import { MachineRealtimeMonitoringService } from '../../features/machine-monitoring/services/machine-realtime-monitoring.service';
import { MachineSnapshotMonitoringService } from '../../features/machine-monitoring/services/machine-snapshot-monitoring.service';
import { MachineSnapshotStore } from '../../features/machine-monitoring/state/machine-snapshot.store';

@Component({
  selector: 'app-machine-shell',
  imports: [AppHeader, AppSidebar, RouterOutlet],
  providers: [
    MachineSnapshotStore,
    MachineSnapshotMonitoringService,
    SignalrConnectionService,
    MachineRealtimeMonitoringService,
  ],
  templateUrl: './machine-shell.html',
  styleUrl: './machine-shell.scss',
})
export class MachineShell {
  readonly machineId = input('');

  readonly snapshotStore = inject(MachineSnapshotStore);

  private readonly monitoring = inject(MachineSnapshotMonitoringService);

  private readonly realtimeMonitoring = inject(MachineRealtimeMonitoringService);

  readonly headerContext = computed<AppHeaderContext | null>(() => {
    const snapshot = this.snapshotStore.snapshot();

    if (snapshot === null) {
      return null;
    }

    return {
      machine: snapshot.machine,
      runtimeVersion: snapshot.runtimeVersion,
      activeAlarms: this.snapshotStore.activeAlarms(),
      activeWarnings: this.snapshotStore.activeWarnings(),
      notifications: this.snapshotStore.notifications(),
      acknowledgingAlarmIds: this.snapshotStore.acknowledgingAlarmIds(),
      alarmAcknowledgeError: this.snapshotStore.alarmAcknowledgeError(),
      snapshotAt: snapshot.snapshotAt,
    };
  });

  constructor() {
    effect(() => {
      const machineId = this.machineId().trim();

      if (!machineId) {
        return;
      }

      this.monitoring.monitor(machineId);
      this.realtimeMonitoring.monitor(machineId);
    });
  }
}
