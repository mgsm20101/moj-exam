import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { ExamLandingComponent } from './exam-landing.component';
import { CandidateExamService } from '../../core/services/candidate-exam.service';

describe('ExamLandingComponent', () => {
  let fixture: ComponentFixture<ExamLandingComponent>;
  let component: ExamLandingComponent;
  const serviceStub = {
    landing: () => of({ examId: 'e1', name: 'Skills', description: null, isOpen: true, durationMinutes: 60, totalQuestionCount: 30 }),
    register: jasmine.createSpy('register').and.returnValue(of({ status: 'CanStart', candidateId: 'c1' }))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExamLandingComponent, HttpClientTestingModule],
      providers: [
        { provide: CandidateExamService, useValue: serviceStub },
        { provide: ActivatedRoute, useValue: { parent: { snapshot: { paramMap: new Map([['examId', 'e1']]) } } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ExamLandingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('marks the form invalid when the national ID is malformed', () => {
    component.form.setValue({ fullName: 'a b c d', nationalId: '123', mobileNumber: '01012345678' });
    expect(component.form.invalid).toBeTrue();
  });

  it('accepts a well-formed identity', () => {
    component.form.setValue({ fullName: 'احمد محمد علي حسن', nationalId: '29912310123404', mobileNumber: '01012345678' });
    expect(component.form.valid).toBeTrue();
  });
});
