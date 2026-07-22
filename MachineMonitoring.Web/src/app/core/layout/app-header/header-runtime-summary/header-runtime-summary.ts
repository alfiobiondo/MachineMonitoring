import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { AppHeaderContext } from '../../models/app-header-context.model';

@Component({
  selector: 'app-header-runtime-summary',
  imports: [DatePipe],
  templateUrl: './header-runtime-summary.html',
  styleUrl: './header-runtime-summary.scss',
})
export class HeaderRuntimeSummary {
  readonly context = input<AppHeaderContext | null>(null);
  readonly refreshing = input(false);
}
