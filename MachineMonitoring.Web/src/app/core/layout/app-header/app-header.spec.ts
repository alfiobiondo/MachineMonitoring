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
    expect(element.textContent).toContain('Aggiornamento snapshot');
    expect(element.textContent).toContain('0 attivi');
  });
});
