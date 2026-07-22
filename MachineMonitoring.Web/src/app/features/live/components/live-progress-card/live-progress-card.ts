import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-live-progress-card',
  templateUrl: './live-progress-card.html',
  styleUrl: './live-progress-card.scss',
})
export class LiveProgressCard {
  readonly title = input.required<string>();
  readonly status = input.required<string>();
  readonly metaText = input<string | null>(null);
  readonly label = input.required<string>();
  readonly detailText = input<string | null>(null);
  readonly progress = input<number | null>(null);
  readonly isEmpty = input(false);

  readonly showProgress = computed(() => this.progress() !== null && !this.isEmpty());

  readonly normalizedProgress = computed(() => {
    const progress = this.progress();

    if (progress === null) {
      return 0;
    }

    return Math.min(100, Math.max(0, progress));
  });
}
