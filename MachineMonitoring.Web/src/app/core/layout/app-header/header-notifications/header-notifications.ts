import { DatePipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';

import { AppHeaderContext } from '../../models/app-header-context.model';

@Component({
  selector: 'app-header-notifications',
  imports: [DatePipe],
  templateUrl: './header-notifications.html',
  styleUrl: './header-notifications.scss',
})
export class HeaderNotifications {
  readonly context = input<AppHeaderContext | null>(null);
  readonly expanded = signal(false);
  readonly panelId = 'header-notifications-panel';

  readonly activeAlarms = computed(() => this.context()?.activeAlarms ?? []);
  readonly activeWarnings = computed(() => this.context()?.activeWarnings ?? []);
  readonly notifications = computed(() => this.context()?.notifications ?? []);
  readonly blockingAlarms = computed(() =>
    this.activeAlarms().filter((alarm) => alarm.isBlocking),
  );
  readonly hasBlockingAlarms = computed(() => this.blockingAlarms().length > 0);
  readonly hasWarnings = computed(() => this.activeWarnings().length > 0);
  readonly hasNotifications = computed(() => this.notifications().length > 0);
  readonly alarmCount = computed(() => this.activeAlarms().length);
  readonly warningCount = computed(() => this.activeWarnings().length);
  readonly toggleLabel = computed(() =>
    this.expanded() ? 'Chiudi segnalazioni macchina' : 'Apri segnalazioni macchina',
  );

  toggle(): void {
    this.expanded.update((value) => !value);
  }
}
