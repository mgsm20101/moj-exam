import { ComponentFixture, TestBed } from '@angular/core/testing';
import { QuestionFormComponent } from './question-form.component';
import { Topic } from '../../../core/services/topic.service';
import { Question } from '../../../core/services/question.service';

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

  it('sets validationError and does not emit when topicId/text are missing', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: '', type: 'FillBlank', text: '' });
    component.submit();

    expect(component.validationError()).toBe('اختر الموضوع واكتب نص السؤال.');
    expect(emitted).toBeNull();
  });

  it('sets validationError and does not emit when FillBlank answer is malformed', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: 't1', type: 'FillBlank', text: 'Fill ___', correctAnswerText: 'Not Valid!' });
    component.submit();

    expect(component.validationError()).toBe('الإجابة يجب أن تكون كلمة واحدة بحروف إنجليزية صغيرة وأرقام فقط.');
    expect(emitted).toBeNull();
  });

  it('sets validationError and does not emit when Mcq options are insufficient', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: 't1', type: 'Mcq', difficulty: 'Medium', text: 'Pick one' });
    component.options.at(0).patchValue({ text: 'A', isCorrect: false });
    component.options.at(1).patchValue({ text: '', isCorrect: false });
    component.submit();

    expect(component.validationError()).toBe('أدخل اختيارين على الأقل وحدد إجابة صحيحة واحدة.');
    expect(emitted).toBeNull();
  });

  it('clears validationError on a successful submit', () => {
    component.validationError.set('خطأ سابق');
    component.form.patchValue({ topicId: 't1', type: 'Mcq', difficulty: 'Medium', text: 'Pick one' });
    component.options.at(0).patchValue({ text: 'A', isCorrect: true });
    component.options.at(1).patchValue({ text: 'B', isCorrect: false });

    component.submit();

    expect(component.validationError()).toBeNull();
  });

  it('populates the form and rebuilds the options array in edit mode', () => {
    const question: Question = {
      id: 'q1', topicId: 't1', topicName: 'Excel', type: 'Mcq', difficulty: 'Hard',
      text: 'Which cell?', imageUrl: null, correctAnswerText: null, pointsOverride: null, isActive: true,
      options: [
        { id: 'o2', text: 'B', isCorrect: true, displayOrder: 2 },
        { id: 'o1', text: 'A', isCorrect: false, displayOrder: 1 }
      ]
    };

    component.question = question;

    expect(component.editing()).toBeTrue();
    expect(component.form.value.topicId).toBe('t1');
    expect(component.form.value.difficulty).toBe('Hard');
    expect(component.form.value.text).toBe('Which cell?');
    // Options are rebuilt sorted by displayOrder.
    expect(component.options.length).toBe(2);
    expect(component.options.at(0).value).toEqual({ text: 'A', isCorrect: false });
    expect(component.options.at(1).value).toEqual({ text: 'B', isCorrect: true });
  });

  it('emits cancelEdit when cancel() is called', () => {
    let cancelled = false;
    component.cancelEdit.subscribe(() => (cancelled = true));

    component.cancel();

    expect(cancelled).toBeTrue();
  });

  it('resetForm() restores default values and a two-option FormArray', () => {
    component.form.patchValue({ topicId: 't1', type: 'FillBlank', difficulty: 'Hard', text: 'Something', imageUrl: 'x.png', correctAnswerText: 'abc' });
    component.options.at(0).patchValue({ text: 'A', isCorrect: true });
    component.validationError.set('خطأ');

    component.resetForm();

    expect(component.form.value.topicId).toBe('');
    expect(component.form.value.type).toBe('Mcq');
    expect(component.form.value.difficulty).toBe('Medium');
    expect(component.form.value.text).toBe('');
    expect(component.form.value.imageUrl).toBe('');
    expect(component.form.value.correctAnswerText).toBe('');
    expect(component.options.length).toBe(2);
    expect(component.options.at(0).value).toEqual({ text: '', isCorrect: false });
    expect(component.options.at(1).value).toEqual({ text: '', isCorrect: false });
    expect(component.validationError()).toBeNull();
  });
});
