import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: 'machines/:machineId/live',
    renderMode: RenderMode.Server,
  },
  {
    path: 'machines/:machineId/programming',
    renderMode: RenderMode.Server,
  },
  {
    path: 'machines/:machineId/technology-parameters',
    renderMode: RenderMode.Server,
  },
  {
    path: 'machines/:machineId',
    renderMode: RenderMode.Server,
  },
  {
    path: '',
    renderMode: RenderMode.Prerender,
  },
  {
    path: '**',
    renderMode: RenderMode.Server,
  },
];
