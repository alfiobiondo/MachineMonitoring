import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'live/:machineId',
    loadComponent: () =>
      import('./pages/live-page/live-page').then(
        (module) => module.LivePage,
      ),
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'live/machine-1',
  },
  {
    path: '**',
    redirectTo: 'live/machine-1',
  },
];
