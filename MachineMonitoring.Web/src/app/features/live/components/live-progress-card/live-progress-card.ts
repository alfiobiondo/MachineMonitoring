import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-live-progress-card',
  templateUrl: './live-progress-card.html',
  styleUrl: './live-progress-card.scss',
})
export class LiveProgressCard {
  readonly title = input.required<string>();
  readonly label = input.required<string>();
  readonly status = input.required<string>();
  readonly progressPercentage = input.required<number>();
  readonly details = input<string | null>(null);

  readonly normalizedProgress = computed(() => {
    const progress = this.progressPercentage();

    return Math.min(100, Math.max(0, progress));
  });
}