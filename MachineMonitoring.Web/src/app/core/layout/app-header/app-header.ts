import { Component, input, output } from '@angular/core';

import { AppHeaderContext } from '../models/app-header-context.model';
import { HeaderMachineStatus } from './header-machine-status/header-machine-status';
import { HeaderNotifications } from './header-notifications/header-notifications';
import { HeaderRuntimeSummary } from './header-runtime-summary/header-runtime-summary';

@Component({
  selector: 'app-header',
  imports: [HeaderMachineStatus, HeaderNotifications, HeaderRuntimeSummary],
  templateUrl: './app-header.html',
  styleUrl: './app-header.scss',
})
export class AppHeader {
  readonly context = input<AppHeaderContext | null>(null);
  readonly refreshing = input(false);
  readonly acknowledgeNotification = output<string>();
}
