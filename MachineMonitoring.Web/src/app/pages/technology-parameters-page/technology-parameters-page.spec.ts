import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TechnologyParametersPage } from './technology-parameters-page';

describe('TechnologyParametersPage', () => {
  let fixture: ComponentFixture<TechnologyParametersPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TechnologyParametersPage],
    }).compileComponents();

    fixture = TestBed.createComponent(TechnologyParametersPage);
    fixture.detectChanges();
  });

  it('should render a minimal placeholder', () => {
    expect(fixture.nativeElement.textContent).toContain('Parametri tecnologici');
  });
});
