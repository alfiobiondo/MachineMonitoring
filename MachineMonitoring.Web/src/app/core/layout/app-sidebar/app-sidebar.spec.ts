import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AppSidebar } from './app-sidebar';

describe('AppSidebar', () => {
  let fixture: ComponentFixture<AppSidebar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppSidebar],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(AppSidebar);
    fixture.componentRef.setInput('machineId', 'M-002');
    fixture.detectChanges();
  });

  it('should render the structural navigation links', () => {
    const element: HTMLElement = fixture.nativeElement;
    const links = Array.from(element.querySelectorAll('a'));

    expect(links.some((link) => link.textContent?.includes('Live'))).toBe(true);
    expect(
      links.some((link) => link.getAttribute('href') === '/machines/M-002/live'),
    ).toBe(true);
    expect(links.some((link) => link.textContent?.includes('Programmazione'))).toBe(true);
    expect(
      links.some(
        (link) => link.getAttribute('href') === '/machines/M-002/programming',
      ),
    ).toBe(true);
    expect(
      links.some((link) => link.textContent?.includes('Parametri tecnologici')),
    ).toBe(true);
    expect(
      links.some(
        (link) =>
          link.getAttribute('href') ===
          '/machines/M-002/technology-parameters',
      ),
    ).toBe(true);
  });
});
