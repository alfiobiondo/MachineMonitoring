import { Component, computed, input } from '@angular/core';

import { AppHeaderContext } from '../../models/app-header-context.model';

type MachineStatusTone =
  'available' | 'running' | 'paused' | 'faulted' | 'maintenance' | 'offline' | 'muted';

@Component({
  selector: 'app-header-machine-status',
  templateUrl: './header-machine-status.html',
  styleUrl: './header-machine-status.scss',
})
export class HeaderMachineStatus {
  readonly context = input<AppHeaderContext | null>(null);

  readonly statusTone = computed<MachineStatusTone>(() => {
    const status = this.context()?.machine.status?.toLowerCase();

    switch (status) {
      case 'available':
        return 'available';

      case 'running':
        return 'running';

      case 'paused':
        return 'paused';

      case 'faulted':
        return 'faulted';

      case 'maintenance':
        return 'maintenance';

      case 'offline':
        return 'offline';

      default:
        return 'muted';
    }
  });
}
