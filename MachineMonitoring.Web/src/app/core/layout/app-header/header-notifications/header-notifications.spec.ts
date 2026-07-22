import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AppHeaderContext } from '../../models/app-header-context.model';
import { HeaderNotifications } from './header-notifications';

describe('HeaderNotifications', () => {
  let fixture: ComponentFixture<HeaderNotifications>;

  const context: AppHeaderContext = {
    machine: {
      id: 'M-001',
      name: 'Laser M-001',
      status: 'Running',
      lastChangedAt: '2026-07-21T08:30:00Z',
    },
    runtimeVersion: 7,
    activeAlarms: [
      {
        id: 'alarm-1',
        code: 'AL-001',
        severity: 'Critical',
        status: 'Active',
        message: 'Safety door is open.',
        isBlocking: true,
        raisedAt: '2026-07-21T08:31:00Z',
      },
    ],
    activeWarnings: [
      {
        id: 'M-001:OrphanRunningOperation:operation-2',
        machineId: 'M-001',
        code: 'OrphanRunningOperation',
        severity: 'Warning',
        title: 'Operation running non assegnata',
        message: 'Operation operation-2 running non allineata.',
        detectedAt: '2026-07-21T08:33:00Z',
        resolvedAt: null,
        isActive: true,
        sourceId: 'operation-2',
      },
    ],
    notifications: [
      {
        id: 'warning:M-001:OrphanRunningOperation:operation-2',
        machineId: 'M-001',
        kind: 'warning',
        severity: 'Warning',
        title: 'Operation running non assegnata',
        message: 'Operation operation-2 running non allineata.',
        timestamp: '2026-07-21T08:33:00Z',
        isActive: true,
        sourceId: 'operation-2',
      },
      {
        id: 'alarm:alarm-1',
        machineId: 'M-001',
        kind: 'alarm',
        severity: 'Critical',
        title: 'AL-001',
        message: 'Safety door is open.',
        timestamp: '2026-07-21T08:31:00Z',
        isActive: true,
        sourceId: 'alarm-1',
      },
    ],
    snapshotAt: '2026-07-21T08:32:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HeaderNotifications],
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderNotifications);
    fixture.componentRef.setInput('context', context);
    fixture.detectChanges();
  });

  it('should expose the renamed selector', () => {
    expect(fixture.componentInstance.constructor).toBe(HeaderNotifications);
  });

  it('should show the closed notification summary without timeline wording', () => {
    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Segnalazioni');
    expect(element.textContent).not.toContain('Timeline');
    expect(element.textContent).toContain('Allarmi');
    expect(element.textContent).toContain('Warning');
    expect(element.querySelector('.header-notifications--blocking')).not.toBeNull();
    expect(element.querySelector('.header-notifications__chevron')).not.toBeNull();
    expect(
      Array.from(element.querySelectorAll('.header-notifications__count')).map((item) =>
        item.textContent?.trim(),
      ),
    ).toEqual(['1', '1']);
  });

  it('should expand its notification timeline sorted by timestamp', () => {
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(button.getAttribute('aria-controls')).toBe('header-notifications-panel');
    expect(button.getAttribute('aria-label')).toBe('Apri segnalazioni macchina');
    expect(fixture.nativeElement.querySelector('.header-notifications__panel')).toBeNull();

    button.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(button.getAttribute('aria-label')).toBe('Chiudi segnalazioni macchina');
    const panel = fixture.nativeElement.querySelector(
      '.header-notifications__panel',
    ) as HTMLElement;
    const items = Array.from(
      fixture.nativeElement.querySelectorAll('.header-notifications__item'),
    ) as HTMLElement[];

    expect(panel).not.toBeNull();
    expect(getComputedStyle(panel).overflowY).toBe('auto');
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('Operation running non assegnata');
    expect(items[0].textContent).toContain('Warning');
    expect(items[1].textContent).toContain('Safety door is open.');
    expect(items[1].textContent).toContain('Allarme');
    expect(items[0].classList).toContain('header-notifications__item--warning');
    expect(items[1].classList).toContain('header-notifications__item--alarm');
    expect(panel.textContent).toContain('21/07/2026');
  });

  it('should show a clear empty state when there are no active notifications', () => {
    fixture.componentRef.setInput('context', {
      ...context,
      activeAlarms: [],
      activeWarnings: [],
      notifications: [],
    });
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Nessuna segnalazione attiva.');
    expect(
      Array.from(
        fixture.nativeElement.querySelectorAll(
          '.header-notifications__count',
        ) as NodeListOf<HTMLElement>,
      ).map((item) => item.textContent?.trim()),
    ).toEqual(['0', '0']);
  });
});
