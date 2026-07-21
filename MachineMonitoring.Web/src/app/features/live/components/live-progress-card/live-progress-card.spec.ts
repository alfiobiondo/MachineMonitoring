import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LiveProgressCard } from './live-progress-card';

describe('LiveProgressCard', () => {
  let fixture: ComponentFixture<LiveProgressCard>;
  let component: LiveProgressCard;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LiveProgressCard],
    }).compileComponents();

    fixture = TestBed.createComponent(LiveProgressCard);
    component = fixture.componentInstance;
  });

  it('should render the progress information', () => {
    fixture.componentRef.setInput('title', 'Lotto');
    fixture.componentRef.setInput('label', 'LOT-001');
    fixture.componentRef.setInput('status', 'Running');
    fixture.componentRef.setInput('progressPercentage', 45);
    fixture.componentRef.setInput(
      'details',
      '3 operazioni completate su 8',
    );

    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;

    expect(element.textContent).toContain('Lotto');
    expect(element.textContent).toContain('LOT-001');
    expect(element.textContent).toContain('Running');
    expect(element.textContent).toContain('45%');
    expect(element.textContent).toContain(
      '3 operazioni completate su 8',
    );
  });

  it('should expose an accessible progress bar', () => {
    fixture.componentRef.setInput('title', 'Operazione');
    fixture.componentRef.setInput('label', 'Cutting');
    fixture.componentRef.setInput('status', 'Running');
    fixture.componentRef.setInput('progressPercentage', 60);

    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector(
      '[role="progressbar"]',
    ) as HTMLElement;

    expect(progressBar.getAttribute('aria-label')).toBe(
      'Operazione: Cutting',
    );
    expect(progressBar.getAttribute('aria-valuemin')).toBe('0');
    expect(progressBar.getAttribute('aria-valuemax')).toBe('100');
    expect(progressBar.getAttribute('aria-valuenow')).toBe('60');
  });

  it('should clamp progress values above 100', () => {
    fixture.componentRef.setInput('title', 'Pezzo');
    fixture.componentRef.setInput('label', 'WP-001');
    fixture.componentRef.setInput('status', 'Completed');
    fixture.componentRef.setInput('progressPercentage', 140);

    fixture.detectChanges();

    const progressBar = fixture.nativeElement.querySelector(
      '[role="progressbar"]',
    ) as HTMLElement;

    const progressFill = fixture.nativeElement.querySelector(
      '.progress-card__bar',
    ) as HTMLElement;

    expect(component.normalizedProgress()).toBe(100);
    expect(progressBar.getAttribute('aria-valuenow')).toBe('100');
    expect(progressFill.style.width).toBe('100%');
  });

  it('should clamp progress values below 0', () => {
    fixture.componentRef.setInput('title', 'Pezzo');
    fixture.componentRef.setInput('label', 'WP-001');
    fixture.componentRef.setInput('status', 'Queued');
    fixture.componentRef.setInput('progressPercentage', -20);

    fixture.detectChanges();

    expect(component.normalizedProgress()).toBe(0);
  });
});