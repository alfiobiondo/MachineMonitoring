import {
  Component,
  effect,
  inject,
  input,
  OnDestroy,
} from '@angular/core';

import { LivePageStore } from '../../features/live/state/live-page.store';

import { LiveProgressCard } from '../../features/live/components/live-progress-card/live-progress-card';

@Component({
  selector: 'app-live-page',
  imports: [LiveProgressCard],
  providers: [LivePageStore],
  templateUrl: './live-page.html',
  styleUrl: './live-page.scss',
})
export class LivePage implements OnDestroy {
  readonly machineId = input.required<string>();

  readonly store = inject(LivePageStore);

  constructor() {
    effect(() => {
      this.store.load(this.machineId());
    });
  }

  ngOnDestroy(): void {
    this.store.destroy();
  }
}