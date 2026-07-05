import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
    questionService = jasmine.createSpyObj('QuestionService', ['getAll', 'create', 'deactivate', 'uploadImage']);
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

  it('deactivateQuestion() calls the service and reloads the list', () => {
    fixture.detectChanges();
    questionService.deactivate.and.returnValue(of(undefined));
    questionService.getAll.calls.reset();

    component.deactivateQuestion('q1');

    expect(questionService.deactivate).toHaveBeenCalledWith('q1');
    expect(questionService.getAll).toHaveBeenCalled();
  });
});
