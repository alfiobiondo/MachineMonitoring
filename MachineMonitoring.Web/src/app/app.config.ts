import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideClientHydration } from '@angular/platform-browser';
import {
  provideRouter,
  withComponentInputBinding,
} from '@angular/router';
import { API_BASE_URL } from './core/api/api-base-url.token';

import { routes } from './app.routes';

export function resolveBrowserApiBaseUrl(): string {
  return globalThis.location?.origin ?? '';
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch()),
    provideClientHydration(),

    {
      provide: API_BASE_URL,
      useFactory: resolveBrowserApiBaseUrl,
    },
  ],
};
