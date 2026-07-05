import { ComponentFixture, TestBed } from '@angular/core/testing';
import { QuestionFormComponent } from './question-form.component';
import { Topic } from '../../../core/services/topic.service';

describe('QuestionFormComponent', () => {
  let fixture: ComponentFixture<QuestionFormComponent>;
  let component: QuestionFormComponent;

  const topics: Topic[] = [{ id: 't1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 0 }];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [QuestionFormComponent] }).compileComponents();
    fixture = TestBed.createComponent(QuestionFormComponent);
    component = fixture.componentInstance;
    component.topics = topics;
    fixture.detectChanges();
  });

  it('normalizes FillBlank answers to a lowercase single word as the admin types', () => {
    component.form.patchValue({ type: 'FillBlank' });
    component.onCorrectAnswerInput('Data Base');

    expect(component.form.value.correctAnswerText).toBe('database');
  });

  it('emits a Mcq payload with the options array on save', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: 't1', type: 'Mcq', difficulty: 'Medium', text: 'Pick one' });
    component.options.at(0).patchValue({ text: 'A', isCorrect: true });
    component.options.at(1).patchValue({ text: 'B', isCorrect: false });
    component.submit();

    expect(emitted.type).toBe('Mcq');
    expect(emitted.options.length).toBe(2);
    expect(emitted.options[0]).toEqual({ text: 'A', isCorrect: true });
  });

  it('does not emit when the form is invalid', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: '', type: 'FillBlank', text: '' });
    component.submit();

    expect(emitted).toBeNull();
  });
});
