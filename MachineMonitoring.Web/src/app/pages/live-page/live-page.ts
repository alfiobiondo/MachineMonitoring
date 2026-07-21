import {
  Component,
  computed,
  effect,
  inject,
  input,
  OnDestroy,
} from '@angular/core';
import { DatePipe } from '@angular/common';

import { LivePageStore } from '../../features/live/state/live-page.store';

import { LiveProgressCard } from '../../features/live/components/live-progress-card/live-progress-card';

@Component({
  selector: 'app-live-page',
  imports: [DatePipe, LiveProgressCard],
  providers: [LivePageStore],
  templateUrl: './live-page.html',
  styleUrl: './live-page.scss',
})
export class LivePage implements OnDestroy {
  readonly machineId = input.required<string>();

  readonly store = inject(LivePageStore);
  readonly runtimePanelClass = computed(() => {
    if (this.store.hasBlockingAlarms()) {
      return 'live-page__runtime-panel live-page__runtime-panel--critical';
    }

    if (this.store.hasActiveAlarms()) {
      return 'live-page__runtime-panel live-page__runtime-panel--warning';
    }

    return 'live-page__runtime-panel';
  });
  readonly runtimeHeadline = computed(() => {
    if (this.store.hasBlockingAlarms()) {
      return 'Macchina con allarme bloccante';
    }

    if (this.store.hasActiveAlarms()) {
      return 'Macchina con allarmi attivi';
    }

    return 'Macchina pronta al monitoraggio';
  });
  readonly runtimeSummary = computed(() => {
    const machineStatus = this.store.machineStatusLabel();

    if (this.store.hasBlockingAlarms()) {
      return `${machineStatus} · intervento richiesto sulla macchina`;
    }

    if (this.store.hasActiveAlarms()) {
      return `${machineStatus} · verificare gli allarmi attivi`;
    }

    return `${machineStatus} · nessun allarme attivo`;
  });
  readonly alarmSummary = computed(() => {
    const activeCount = this.store.activeAlarmCount();

    if (activeCount === 0) {
      return 'Nessun allarme attivo';
    }

    if (this.store.hasBlockingAlarms()) {
      return `${this.store.blockingAlarmCount()} allarme bloccante su ${activeCount} attivi`;
    }

    return `${activeCount} allarmi attivi non bloccanti`;
  });

  constructor() {
    effect(() => {
      this.store.load(this.machineId());
    });
  }

  ngOnDestroy(): void {
    this.store.destroy();
  }
}
