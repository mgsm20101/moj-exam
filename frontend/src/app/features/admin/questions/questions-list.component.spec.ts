import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { QuestionsListComponent } from './questions-list.component';
import { QuestionService, Question } from '../../../core/services/question.service';
import { TopicService, Topic } from '../../../core/services/topic.service';

describe('QuestionsListComponent', () => {
  let fixture: ComponentFixture<QuestionsListComponent>;
  let component: QuestionsListComponent;
  let questionService: jasmine.SpyObj<QuestionService>;
  let topicService: jasmine.SpyObj<TopicService>;

  const topics: Topic[] = [{ id: 't1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 1 }];
  const questions: Question[] = [{
    id: 'q1', topicId: 't1', topicName: 'Excel', type: 'FillBlank', difficulty: 'Medium',
    text: 'Fill ___', imageUrl: null, correctAnswerText: 'server', pointsOverride: null, isActive: true, options: []
  }];

  beforeEach(async () => {
    questionService = jasmine.createSpyObj('QuestionService', ['getAll', 'create', 'update', 'deactivate', 'uploadImage']);
    topicService = jasmine.createSpyObj('TopicService', ['getAll']);
    questionService.getAll.and.returnValue(of(questions));
    topicService.getAll.and.returnValue(of(topics));

    await TestBed.configureTestingModule({
      imports: [QuestionsListComponent],
      providers: [
        { provide: QuestionService, useValue: questionService },
        { provide: TopicService, useValue: topicService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(QuestionsListComponent);
    component = fixture.componentInstance;
  });

  it('loads topics and questions on init', () => {
    fixture.detectChanges();

    expect(component.topics()).toEqual(topics);
    expect(component.questions()).toEqual(questions);
  });

  it('refetches questions when the topic filter changes', () => {
    fixture.detectChanges();
    questionService.getAll.calls.reset();

    component.selectedTopicId = 't1';
    component.applyFilters();

    expect(questionService.getAll).toHaveBeenCalledWith({ topicId: 't1', difficulty: undefined });
  });

  it('onQuestionSave() creates the question and reloads the list', () => {
    fixture.detectChanges();
    questionService.create.and.returnValue(of({ id: 'q2' }));
    questionService.getAll.calls.reset();

    component.onQuestionSave({ topicId: 't1', type: 'FillBlank', difficulty: 'Medium', text: 'Fill ___', correctAnswerText: 'server' });

    expect(questionService.create).toHaveBeenCalled();
    expect(questionService.getAll).toHaveBeenCalled();
  });

  it('startEdit() puts the form into edit mode with the selected question', () => {
    fixture.detectChanges();

    component.startEdit(questions[0]);

    expect(component.editingQuestion()).toBe(questions[0]);
  });

  it('onQuestionSave() updates (not creates) when editing, then clears edit mode', () => {
    fixture.detectChanges();
    questionService.update.and.returnValue(of(undefined));
    questionService.getAll.calls.reset();

    component.startEdit(questions[0]);
    component.onQuestionSave({ topicId: 't1', type: 'FillBlank', difficulty: 'Medium', text: 'Fill ___', correctAnswerText: 'client' });

    expect(questionService.update).toHaveBeenCalledWith('q1', jasmine.objectContaining({ isActive: true, correctAnswerText: 'client' }));
    expect(questionService.create).not.toHaveBeenCalled();
    expect(component.editingQuestion()).toBeNull();
    expect(questionService.getAll).toHaveBeenCalled();
  });

  it('toggleExpand() toggles the answers detail row for a question', () => {
    fixture.detectChanges();

    component.toggleExpand('q1');
    expect(component.expandedId()).toBe('q1');
    component.toggleExpand('q1');
    expect(component.expandedId()).toBeNull();
  });

  it('pages the question list client-side and clamps navigation to valid pages', () => {
    const many: Question[] = Array.from({ length: 23 }, (_, i) => ({
      ...questions[0], id: `q${i}`, text: `Q${i}`
    }));
    questionService.getAll.and.returnValue(of(many));
    fixture.detectChanges();

    expect(component.totalPages()).toBe(3);           // 23 / 10 → 3 pages
    expect(component.pagedQuestions().length).toBe(10);

    component.goToPage(3);
    expect(component.pagedQuestions().length).toBe(3); // last page has the remaining 3

    component.goToPage(99);                             // clamps to the last page
    expect(component.currentPage()).toBe(3);

    component.goToPage(0);                              // clamps to the first page
    expect(component.currentPage()).toBe(1);
  });

  it('applyFilters() resets pagination back to the first page', () => {
    const many: Question[] = Array.from({ length: 23 }, (_, i) => ({ ...questions[0], id: `q${i}` }));
    questionService.getAll.and.returnValue(of(many));
    fixture.detectChanges();
    component.goToPage(3);

    component.applyFilters();

    expect(component.currentPage()).toBe(1);
  });

  it('deactivateQuestion() calls the service and reloads the list', () => {
    fixture.detectChanges();
    questionService.deactivate.and.returnValue(of(undefined));
    questionService.getAll.calls.reset();

    component.deactivateQuestion('q1');

    expect(questionService.deactivate).toHaveBeenCalledWith('q1');
    expect(questionService.getAll).toHaveBeenCalled();
  });

  it('onImageFileSelected() uploads the file and sets the child form imageUrl on success', () => {
    fixture.detectChanges();
    component.openCreateForm();
    fixture.detectChanges();
    const file = new File(['x'], 'photo.png', { type: 'image/png' });
    questionService.uploadImage.and.returnValue(of({ url: 'https://cdn/photo.png' }));
    const setImageUrlSpy = spyOn(component.questionForm!, 'setImageUrl');

    component.onImageFileSelected(file);

    expect(questionService.uploadImage).toHaveBeenCalledWith(file);
    expect(setImageUrlSpy).toHaveBeenCalledWith('https://cdn/photo.png');
  });

  it('sets errorMessage when the image upload fails', () => {
    fixture.detectChanges();
    const file = new File(['x'], 'photo.png', { type: 'image/png' });
    questionService.uploadImage.and.returnValue(throwError(() => new Error('fail')));

    component.onImageFileSelected(file);

    expect(component.errorMessage).toBe('تعذّر رفع الصورة.');
  });

  it('emitting imageFileSelected from the form template triggers the upload', () => {
    fixture.detectChanges();
    component.openCreateForm();
    fixture.detectChanges();
    const file = new File(['x'], 'photo.png', { type: 'image/png' });
    questionService.uploadImage.and.returnValue(of({ url: 'https://cdn/photo.png' }));
    spyOn(component, 'onImageFileSelected').and.callThrough();

    component.questionForm!.imageFileSelected.emit(file);

    expect(component.onImageFileSelected).toHaveBeenCalledWith(file);
    expect(questionService.uploadImage).toHaveBeenCalledWith(file);
  });

  it('onQuestionSave() resets the child form after a successful save', () => {
    fixture.detectChanges();
    component.openCreateForm();
    fixture.detectChanges();
    questionService.create.and.returnValue(of({ id: 'q2' }));
    const resetFormSpy = spyOn(component.questionForm!, 'resetForm');

    component.onQuestionSave({ topicId: 't1', type: 'FillBlank', difficulty: 'Medium', text: 'Fill ___', correctAnswerText: 'server' });

    expect(resetFormSpy).toHaveBeenCalled();
  });
});
