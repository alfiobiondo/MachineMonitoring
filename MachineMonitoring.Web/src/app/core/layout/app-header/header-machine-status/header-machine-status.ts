import { Component, input } from '@angular/core';

import { AppHeaderContext } from '../../models/app-header-context.model';

@Component({
  selector: 'app-header-machine-status',
  templateUrl: './header-machine-status.html',
  styleUrl: './header-machine-status.scss',
})
export class HeaderMachineStatus {
  readonly context = input<AppHeaderContext | null>(null);
}
