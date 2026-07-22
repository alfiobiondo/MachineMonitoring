import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

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
        category: 'warning',
        lifecycleStatus: 'Active',
        severity: 'Warning',
        title: 'Operation running non assegnata',
        message: 'Operation operation-2 running non allineata.',
        timestamp: '2026-07-21T08:33:00Z',
        raisedAt: '2026-07-21T08:33:00Z',
        isBlocking: false,
        isActive: true,
        sourceId: 'operation-2',
      },
      {
        id: 'alarm:alarm-1',
        machineId: 'M-001',
        kind: 'alarm',
        category: 'alarm',
        lifecycleStatus: 'Acknowledged',
        severity: 'Critical',
        title: 'AL-001',
        message: 'Safety door is open.',
        timestamp: '2026-07-21T08:31:00Z',
        raisedAt: '2026-07-21T08:31:00Z',
        acknowledgedAt: '2026-07-21T08:31:30Z',
        isBlocking: true,
        isActive: true,
        sourceId: 'alarm-1',
      },
    ],
    acknowledgingAlarmIds: [],
    alarmAcknowledgeError: null,
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
    expect(items[0].textContent).toContain('Da riconoscere');
    expect(items[1].textContent).toContain('Safety door is open.');
    expect(items[1].textContent).toContain('Allarme');
    expect(items[1].textContent).toContain('Riconosciuto');
    expect(items[0].classList).toContain('header-notifications__item--warning');
    expect(items[0].classList).toContain('header-notifications__item--active');
    expect(items[1].classList).toContain('header-notifications__item--alarm');
    expect(items[1].classList).toContain('header-notifications__item--acknowledged');
    expect(panel.textContent).toContain('21/07/2026');
  });

  it('should use highlighted orange and red surfaces for active warning and alarm notifications', () => {
    fixture.componentRef.setInput('context', {
      ...context,
      notifications: [
        {
          id: 'warning:warning-alarm-1',
          machineId: 'M-001',
          kind: 'warning',
          category: 'warning',
          lifecycleStatus: 'Active',
          severity: 'Warning',
          title: 'SIM-WARN-TEMP',
          message: 'Temperature warning.',
          timestamp: '2026-07-21T08:33:00Z',
          raisedAt: '2026-07-21T08:33:00Z',
          isBlocking: false,
          isActive: true,
          sourceId: 'warning-alarm-1',
        },
        {
          id: 'alarm:alarm-1',
          machineId: 'M-001',
          kind: 'alarm',
          category: 'alarm',
          lifecycleStatus: 'Active',
          severity: 'Critical',
          title: 'AL-001',
          message: 'Safety door is open.',
          timestamp: '2026-07-21T08:31:00Z',
          raisedAt: '2026-07-21T08:31:00Z',
          isBlocking: true,
          isActive: true,
          sourceId: 'alarm-1',
        },
      ],
    });
    fixture.detectChanges();

    openPanel();
    const items = getNotificationItems();

    expect(items[0].classList).toContain('header-notifications__item--warning');
    expect(items[0].classList).toContain('header-notifications__item--active');
    expect(items[0].textContent).toContain('Da riconoscere');
    expect(items[1].classList).toContain('header-notifications__item--alarm');
    expect(items[1].classList).toContain('header-notifications__item--active');
    expect(items[1].textContent).toContain('Da riconoscere');
  });

  it('should use attenuated surfaces for acknowledged warning and alarm notifications', () => {
    fixture.componentRef.setInput('context', {
      ...context,
      notifications: [
        {
          id: 'warning:warning-alarm-1',
          machineId: 'M-001',
          kind: 'warning',
          category: 'warning',
          lifecycleStatus: 'Acknowledged',
          severity: 'Warning',
          title: 'SIM-WARN-TEMP',
          message: 'Temperature warning.',
          timestamp: '2026-07-21T08:33:00Z',
          raisedAt: '2026-07-21T08:33:00Z',
          acknowledgedAt: '2026-07-21T08:34:00Z',
          isBlocking: false,
          isActive: true,
          sourceId: 'warning-alarm-1',
        },
        {
          id: 'alarm:alarm-1',
          machineId: 'M-001',
          kind: 'alarm',
          category: 'alarm',
          lifecycleStatus: 'Acknowledged',
          severity: 'Critical',
          title: 'AL-001',
          message: 'Safety door is open.',
          timestamp: '2026-07-21T08:31:00Z',
          raisedAt: '2026-07-21T08:31:00Z',
          acknowledgedAt: '2026-07-21T08:31:30Z',
          isBlocking: true,
          isActive: true,
          sourceId: 'alarm-1',
        },
      ],
    });
    fixture.detectChanges();

    openPanel();
    const items = getNotificationItems();

    expect(items[0].classList).toContain('header-notifications__item--acknowledged');
    expect(items[0].textContent).toContain('Riconosciuto');
    expect(items[1].classList).toContain('header-notifications__item--acknowledged');
    expect(items[1].textContent).toContain('Riconosciuto');
  });

  it('should render metadata on the left and lifecycle status on the right', () => {
    openPanel();

    const meta = fixture.nativeElement.querySelector('.header-notifications__meta') as HTMLElement;

    expect(meta).not.toBeNull();
    expect(meta.querySelector('.header-notifications__details')?.textContent).toContain('Warning');
    expect(meta.querySelector('.header-notifications__status')?.textContent).toContain(
      'Da riconoscere',
    );
    expect(getComputedStyle(meta).justifyContent).toBe('space-between');
    expect(getComputedStyle(meta).flexWrap).toBe('wrap');
  });

  it('should emit acknowledge once when an active notification is clicked', () => {
    const acknowledge = vi.fn();
    fixture.componentInstance.acknowledgeNotification.subscribe(acknowledge);

    openPanel();
    getNotificationItems()[0].click();

    expect(acknowledge).toHaveBeenCalledTimes(1);
    expect(acknowledge).toHaveBeenCalledWith('operation-2');
  });

  it('should not emit duplicate acknowledge requests while pending', () => {
    const acknowledge = vi.fn();
    fixture.componentInstance.acknowledgeNotification.subscribe(acknowledge);
    fixture.componentRef.setInput('context', {
      ...context,
      acknowledgingAlarmIds: ['operation-2'],
    });
    fixture.detectChanges();

    openPanel();
    getNotificationItems()[0].click();

    expect(acknowledge).not.toHaveBeenCalled();
    expect(getNotificationItems()[0].textContent).toContain('Riconoscimento...');
  });

  it('should not emit acknowledge for an acknowledged notification', () => {
    const acknowledge = vi.fn();
    fixture.componentInstance.acknowledgeNotification.subscribe(acknowledge);

    openPanel();
    getNotificationItems()[1].click();

    expect(acknowledge).not.toHaveBeenCalled();
  });

  it('should acknowledge active notifications from Enter and Space', () => {
    const acknowledge = vi.fn();
    fixture.componentInstance.acknowledgeNotification.subscribe(acknowledge);

    openPanel();
    const item = getNotificationItems()[0];

    item.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    item.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(acknowledge).toHaveBeenCalledTimes(2);
    expect(acknowledge).toHaveBeenNthCalledWith(1, 'operation-2');
    expect(acknowledge).toHaveBeenNthCalledWith(2, 'operation-2');
  });

  it('should expose an accessible acknowledge label for active notifications', () => {
    openPanel();

    expect(getNotificationItems()[0].getAttribute('aria-label')).toBe(
      'Riconosci warning Operation running non assegnata',
    );
  });

  it('should count acknowledged notifications until they are resolved upstream', () => {
    fixture.componentRef.setInput('context', {
      ...context,
      activeAlarms: [
        {
          id: 'alarm-1',
          code: 'AL-001',
          severity: 'Critical',
          status: 'Acknowledged',
          message: 'Safety door is open.',
          isBlocking: true,
          raisedAt: '2026-07-21T08:31:00Z',
          acknowledgedAt: '2026-07-21T08:31:30Z',
        },
      ],
      activeWarnings: [
        {
          id: 'warning-alarm-1',
          machineId: 'M-001',
          code: 'SIM-WARN-TEMP',
          severity: 'Warning',
          title: 'SIM-WARN-TEMP',
          message: 'Temperature warning.',
          detectedAt: '2026-07-21T08:30:00Z',
          resolvedAt: null,
          isActive: true,
          sourceId: 'warning-alarm-1',
        },
      ],
    });
    fixture.detectChanges();

    expect(
      Array.from(
        fixture.nativeElement.querySelectorAll(
          '.header-notifications__count',
        ) as NodeListOf<HTMLElement>,
      ).map((item) => item.textContent?.trim()),
    ).toEqual(['1', '1']);
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

  function openPanel(): void {
    const button = fixture.nativeElement.querySelector(
      '.header-notifications__button',
    ) as HTMLButtonElement;

    if (button.getAttribute('aria-expanded') !== 'true') {
      button.click();
      fixture.detectChanges();
    }
  }

  function getNotificationItems(): HTMLButtonElement[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll(
        '.header-notifications__item',
      ) as NodeListOf<HTMLButtonElement>,
    );
  }
});
