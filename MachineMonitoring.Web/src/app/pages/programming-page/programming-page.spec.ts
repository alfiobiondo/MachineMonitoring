import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProgrammingPage } from './programming-page';

describe('ProgrammingPage', () => {
  let fixture: ComponentFixture<ProgrammingPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProgrammingPage],
    }).compileComponents();

    fixture = TestBed.createComponent(ProgrammingPage);
    fixture.detectChanges();
  });

  it('should render a minimal placeholder', () => {
    expect(fixture.nativeElement.textContent).toContain('Programmazione');
  });
});
