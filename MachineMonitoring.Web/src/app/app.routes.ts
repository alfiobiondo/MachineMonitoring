import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'machines/:machineId',
    loadComponent: () =>
      import('./pages/machine-shell/machine-shell').then(
        (module) => module.MachineShell,
      ),
    children: [
      {
        path: 'live',
        loadComponent: () =>
          import('./pages/live-page/live-page').then(
            (module) => module.LivePage,
          ),
      },
      {
        path: 'programming',
        loadComponent: () =>
          import('./pages/programming-page/programming-page').then(
            (module) => module.ProgrammingPage,
          ),
      },
      {
        path: 'technology-parameters',
        loadComponent: () =>
          import(
            './pages/technology-parameters-page/technology-parameters-page'
          ).then((module) => module.TechnologyParametersPage),
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'live',
      },
    ],
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'machines/M-001/live',
  },
  {
    path: '**',
    redirectTo: 'machines/M-001/live',
  },
];
