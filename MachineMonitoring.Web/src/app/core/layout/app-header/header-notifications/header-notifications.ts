import { DatePipe } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';

import {
  AppHeaderContext,
  AppHeaderNotification,
} from '../../models/app-header-context.model';

@Component({
  selector: 'app-header-notifications',
  imports: [DatePipe],
  templateUrl: './header-notifications.html',
  styleUrl: './header-notifications.scss',
})
export class HeaderNotifications {
  readonly context = input<AppHeaderContext | null>(null);
  readonly acknowledgeNotification = output<string>();
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
  readonly acknowledgingAlarmIds = computed(() => this.context()?.acknowledgingAlarmIds ?? []);
  readonly alarmAcknowledgeError = computed(() => this.context()?.alarmAcknowledgeError ?? null);
  readonly toggleLabel = computed(() =>
    this.expanded() ? 'Chiudi segnalazioni macchina' : 'Apri segnalazioni macchina',
  );

  toggle(): void {
    this.expanded.update((value) => !value);
  }

  acknowledge(notification: AppHeaderNotification): void {
    if (
      notification.lifecycleStatus !== 'Active' ||
      this.isAcknowledging(notification) ||
      !notification.sourceId
    ) {
      return;
    }

    this.acknowledgeNotification.emit(notification.sourceId);
  }

  handleNotificationKeydown(event: KeyboardEvent, notification: AppHeaderNotification): void {
    if (event.key !== 'Enter' && event.key !== ' ') {
      return;
    }

    event.preventDefault();
    this.acknowledge(notification);
  }

  isAcknowledging(notification: AppHeaderNotification): boolean {
    return this.acknowledgingAlarmIds().includes(notification.sourceId);
  }

  notificationLabel(notification: AppHeaderNotification): string {
    const category = notification.kind === 'alarm' ? 'allarme' : 'warning';

    if (notification.lifecycleStatus === 'Acknowledged') {
      return `${category} ${notification.title} già riconosciuto`;
    }

    return `Riconosci ${category} ${notification.title}`;
  }
}
