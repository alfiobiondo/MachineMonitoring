import { TestBed } from '@angular/core/testing';

import { AppHeader } from './app-header';

describe('AppHeader', () => {
  it('should compose machine, runtime and alarm sections horizontally', async () => {
    await TestBed.configureTestingModule({
      imports: [AppHeader],
    }).compileComponents();

    const fixture = TestBed.createComponent(AppHeader);
    fixture.componentRef.setInput('context', {
      machine: {
        id: 'M-001',
        name: 'Laser M-001',
        status: 'Running',
        lastChangedAt: '2026-07-21T08:30:00Z',
      },
      runtimeVersion: 7,
      activeAlarms: [],
      activeWarnings: [],
      notifications: [],
      acknowledgingAlarmIds: [],
      alarmAcknowledgeError: null,
      snapshotAt: '2026-07-21T08:32:00Z',
    });
    fixture.componentRef.setInput('refreshing', true);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Laser M-001');
    expect(element.querySelector('.app-header__title')?.textContent).toContain(
      'MachineMonitoring',
    );
    expect(element.textContent).toContain('Snapshot HTTP');
    expect(element.textContent).toContain('Aggiornamento');
    expect(element.textContent).toContain('Allarmi');
    expect(element.textContent).toContain('Warning');
    expect(element.querySelector('app-header-notifications')).not.toBeNull();
    expect(element.querySelector(['app', 'header', 'alarms'].join('-'))).toBeNull();

    const machineChip = element.querySelector('.machine-status__badge') as HTMLElement;
    const runtimeChip = element.querySelector('.runtime-summary__refresh') as HTMLElement;
    const machineChipStyle = getComputedStyle(machineChip);
    const runtimeChipStyle = getComputedStyle(runtimeChip);

    expect(machineChipStyle.minHeight).toBe(runtimeChipStyle.minHeight);
    expect(machineChipStyle.fontSize).toBe(runtimeChipStyle.fontSize);
    expect(machineChipStyle.fontWeight).toBe(runtimeChipStyle.fontWeight);
    expect(machineChipStyle.lineHeight).toBe(runtimeChipStyle.lineHeight);
  });
});
