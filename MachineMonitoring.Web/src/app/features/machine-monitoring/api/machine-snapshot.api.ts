import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../../core/api/api-base-url.token';
import { MachineSnapshot } from '../models/machine-snapshot.model';

@Injectable({
  providedIn: 'root',
})
export class MachineSnapshotApi {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getByMachineId(machineId: string): Observable<MachineSnapshot> {
    const encodedMachineId = encodeURIComponent(machineId);

    return this.http.get<MachineSnapshot>(
      `${this.apiBaseUrl}/api/machines/${encodedMachineId}/live-snapshot`,
    );
  }
}
