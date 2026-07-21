import { Component, inject } from '@angular/core';

import { MachineSnapshotStore } from '../../features/machine-monitoring/state/machine-snapshot.store';
import { LiveProgressCard } from '../../features/live/components/live-progress-card/live-progress-card';

@Component({
  selector: 'app-live-page',
  imports: [LiveProgressCard],
  templateUrl: './live-page.html',
  styleUrl: './live-page.scss',
})
export class LivePage {
  readonly store = inject(MachineSnapshotStore);
}
