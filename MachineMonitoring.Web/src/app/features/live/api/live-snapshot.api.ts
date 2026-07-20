import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { LiveSnapshot } from '../models/live-snapshot.model';

@Injectable({
  providedIn: 'root',
})
export class LiveSnapshotApi {
  private readonly http = inject(HttpClient);

  getByMachineId(machineId: string): Observable<LiveSnapshot> {
    const encodedMachineId = encodeURIComponent(machineId);

    return this.http.get<LiveSnapshot>(
      `/api/machines/${encodedMachineId}/live-snapshot`,
    );
  }
}
