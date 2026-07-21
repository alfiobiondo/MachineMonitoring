import { Component, computed, input, signal } from '@angular/core';

import { AppHeaderContext } from '../../models/app-header-context.model';

@Component({
  selector: 'app-header-alarms',
  templateUrl: './header-alarms.html',
  styleUrl: './header-alarms.scss',
})
export class HeaderAlarms {
  readonly context = input<AppHeaderContext | null>(null);
  readonly expanded = signal(false);

  readonly activeAlarms = computed(() => this.context()?.activeAlarms ?? []);
  readonly blockingAlarms = computed(() =>
    this.activeAlarms().filter((alarm) => alarm.isBlocking),
  );
  readonly hasBlockingAlarms = computed(() => this.blockingAlarms().length > 0);

  toggle(): void {
    this.expanded.update((value) => !value);
  }
}
