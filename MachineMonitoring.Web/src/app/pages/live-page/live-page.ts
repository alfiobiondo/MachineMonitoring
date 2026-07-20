import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { JsonPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';

import { LiveSnapshotApi } from '../../features/live/api/live-snapshot.api';
import { LiveSnapshot } from '../../features/live/models/live-snapshot.model';

@Component({
  selector: 'app-live-page',
  imports: [JsonPipe],
  templateUrl: './live-page.html',
  styleUrl: './live-page.scss',
})
export class LivePage {
  readonly machineId = input.required<string>();

  private readonly liveSnapshotApi = inject(LiveSnapshotApi);

  readonly snapshot = signal<LiveSnapshot | null>(null);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly hasProductionContext = computed(
    () =>
      this.snapshot()?.productionLot !== null &&
      this.snapshot()?.currentWorkpiece !== null &&
      this.snapshot()?.currentOperation !== null,
  );

  constructor() {
    effect((onCleanup) => {
      const machineId = this.machineId();

      this.loading.set(true);
      this.errorMessage.set(null);

      const subscription = this.liveSnapshotApi
        .getByMachineId(machineId)
        .subscribe({
          next: (snapshot) => {
            this.snapshot.set(snapshot);
            this.loading.set(false);
          },
          error: (error: HttpErrorResponse) => {
            this.snapshot.set(null);
            this.errorMessage.set(this.getErrorMessage(error));
            this.loading.set(false);
          },
        });

      onCleanup(() => subscription.unsubscribe());
    });
  }

  private getErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 404) {
      return `Macchina "${this.machineId()}" non trovata.`;
    }

    if (error.status === 0) {
      return 'Impossibile raggiungere il backend.';
    }

    return 'Non è stato possibile caricare lo stato Live.';
  }
}
