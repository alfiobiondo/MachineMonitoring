import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../../core/api/api-base-url.token';

export interface ResolveMachineAlarmRequest {
  resolutionNotes: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class MachineAlarmApi {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  acknowledge(alarmId: string): Observable<void> {
    const encodedAlarmId = encodeURIComponent(alarmId);

    return this.http.post<void>(`${this.apiBaseUrl}/api/alarms/${encodedAlarmId}/acknowledge`, null);
  }

  resolve(alarmId: string, request: ResolveMachineAlarmRequest): Observable<void> {
    const encodedAlarmId = encodeURIComponent(alarmId);

    return this.http.post<void>(
      `${this.apiBaseUrl}/api/alarms/${encodedAlarmId}/resolve`,
      request,
    );
  }
}
