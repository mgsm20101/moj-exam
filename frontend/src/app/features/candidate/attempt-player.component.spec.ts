import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { AttemptPlayerComponent } from './attempt-player.component';
import { CandidateAttemptService, AttemptState } from '../../core/services/candidate-attempt.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

describe('AttemptPlayerComponent', () => {
  const state: AttemptState = {
    status: 'InProgress', remainingSeconds: 300, showResultImmediately: true,
    questions: [
      { attemptQuestionId: 'q1', displayOrder: 1, type: 'Mcq', text: 'Q1', imageUrl: null,
        options: [{ id: 'o1', text: 'A' }, { id: 'o2', text: 'B' }],
        selectedOptionId: null, answerText: null, isFlagged: false }
    ]
  };
  const serviceStub = {
    // fresh clone per call so mutations in one test don't leak into the next
    state: () => of(JSON.parse(JSON.stringify(state)) as AttemptState),
    saveAnswer: jasmine.createSpy('saveAnswer').and.returnValue(of(void 0)),
    submit: () => of({ shown: true, score: 2, totalPoints: 2, passMarkPercentage: 60, passed: true }),
    result: () => of({ shown: true, score: 2, totalPoints: 2, passMarkPercentage: 60, passed: true })
  };
  const tokenStub = { get: () => 'tok', set: () => {}, clear: () => {} };

  let fixture: ComponentFixture<AttemptPlayerComponent>;
  let component: AttemptPlayerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttemptPlayerComponent],
      providers: [
        { provide: CandidateAttemptService, useValue: serviceStub },
        { provide: AttemptTokenStore, useValue: tokenStub },
        { provide: ActivatedRoute, useValue: { parent: { snapshot: { paramMap: new Map([['examId', 'e1']]) } } } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(AttemptPlayerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the attempt state and shows the first question', () => {
    expect(component.state?.questions.length).toBe(1);
    expect(component.current?.attemptQuestionId).toBe('q1');
  });

  it('saves an answer immediately when an option is selected', () => {
    component.selectOption('o1');
    expect(serviceStub.saveAnswer).toHaveBeenCalled();
    expect(component.current?.selectedOptionId).toBe('o1');
  });

  it('reports the unanswered count', () => {
    expect(component.unansweredCount).toBe(1);
    component.selectOption('o1');
    expect(component.unansweredCount).toBe(0);
  });
});
