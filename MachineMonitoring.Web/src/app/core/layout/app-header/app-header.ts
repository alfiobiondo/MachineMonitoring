import { Component, input } from '@angular/core';

import { AppHeaderContext } from '../models/app-header-context.model';
import { HeaderAlarms } from './header-alarms/header-alarms';
import { HeaderMachineStatus } from './header-machine-status/header-machine-status';
import { HeaderRuntimeSummary } from './header-runtime-summary/header-runtime-summary';

@Component({
  selector: 'app-header',
  imports: [HeaderAlarms, HeaderMachineStatus, HeaderRuntimeSummary],
  templateUrl: './app-header.html',
  styleUrl: './app-header.scss',
})
export class AppHeader {
  readonly context = input<AppHeaderContext | null>(null);
  readonly refreshing = input(false);
}
