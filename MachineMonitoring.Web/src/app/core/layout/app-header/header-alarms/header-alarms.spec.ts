import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AppHeaderContext } from '../../models/app-header-context.model';
import { HeaderAlarms } from './header-alarms';

describe('HeaderAlarms', () => {
  let fixture: ComponentFixture<HeaderAlarms>;

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
    snapshotAt: '2026-07-21T08:32:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HeaderAlarms],
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderAlarms);
    fixture.componentRef.setInput('context', context);
    fixture.detectChanges();
  });

  it('should show active and blocking alarm counters', () => {
    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('1 attivo');
    expect(element.textContent).toContain('1 bloccanti');
    expect(element.querySelector('.header-alarms--blocking')).not.toBeNull();
  });

  it('should expand only its own alarm panel', () => {
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('.header-alarms__panel')).toBeNull();

    button.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('.header-alarms__panel')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Safety door is open.');
  });
});
