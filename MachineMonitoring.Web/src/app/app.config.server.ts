import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_BASE_URL } from './core/api/api-base-url.token';
import { HTTP_TRANSFER_CACHE_ORIGIN_MAP } from '@angular/common/http';

const apiBaseUrl =
  process.env['API_BASE_URL'] ?? 'http://localhost:5221';

const browserOrigin =
  process.env['BROWSER_ORIGIN'] ?? 'http://localhost:4000';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    {
      provide: API_BASE_URL,
      useValue: apiBaseUrl,
    },
    {
      provide: HTTP_TRANSFER_CACHE_ORIGIN_MAP,
      useValue: {
        [apiBaseUrl]: browserOrigin,
      },
    },
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
