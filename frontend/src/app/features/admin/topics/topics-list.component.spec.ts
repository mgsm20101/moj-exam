import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { TopicsListComponent } from './topics-list.component';
import { TopicService, Topic } from '../../../core/services/topic.service';

describe('TopicsListComponent', () => {
  let fixture: ComponentFixture<TopicsListComponent>;
  let component: TopicsListComponent;
  let topicService: jasmine.SpyObj<TopicService>;

  const sampleTopics: Topic[] = [
    { id: '1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 10 }
  ];

  beforeEach(async () => {
    topicService = jasmine.createSpyObj('TopicService', ['getAll', 'create', 'update', 'delete']);
    topicService.getAll.and.returnValue(of(sampleTopics));

    await TestBed.configureTestingModule({
      imports: [TopicsListComponent],
      providers: [{ provide: TopicService, useValue: topicService }]
    }).compileComponents();

    fixture = TestBed.createComponent(TopicsListComponent);
    component = fixture.componentInstance;
  });

  it('loads topics on init', () => {
    fixture.detectChanges();
    expect(component.topics()).toEqual(sampleTopics);
  });

  it('createTopic() calls the service and reloads the list', () => {
    topicService.create.and.returnValue(of({ id: '2' }));
    fixture.detectChanges();

    component.newTopicName = 'Word';
    component.newTopicDisplayOrder = 2;
    component.createTopic();

    expect(topicService.create).toHaveBeenCalledWith({ name: 'Word', displayOrder: 2 });
    expect(topicService.getAll).toHaveBeenCalledTimes(2);
  });

  it('deleteTopic() calls the service and reloads the list', () => {
    topicService.delete.and.returnValue(of(undefined));
    fixture.detectChanges();

    component.deleteTopic('1');

    expect(topicService.delete).toHaveBeenCalledWith('1');
    expect(topicService.getAll).toHaveBeenCalledTimes(2);
  });

  it('createTopic() rejects a non-positive or non-integer displayOrder without calling the service', () => {
    fixture.detectChanges();

    component.newTopicName = 'Word';
    component.newTopicDisplayOrder = 0;
    component.createTopic();

    expect(component.errorMessage).toBe('ترتيب العرض يجب أن يكون رقمًا صحيحًا موجبًا.');
    expect(topicService.create).not.toHaveBeenCalled();

    component.newTopicDisplayOrder = -1;
    component.createTopic();
    expect(topicService.create).not.toHaveBeenCalled();

    component.newTopicDisplayOrder = 1.5;
    component.createTopic();
    expect(topicService.create).not.toHaveBeenCalled();
  });

  it('createTopic() sets isSubmitting while the create call is in flight and clears it on success', () => {
    const create$ = new Subject<{ id: string }>();
    topicService.create.and.returnValue(create$);
    fixture.detectChanges();

    component.newTopicName = 'Word';
    component.newTopicDisplayOrder = 2;
    component.createTopic();

    expect(component.isSubmitting()).toBe(true);

    create$.next({ id: '2' });
    create$.complete();

    expect(component.isSubmitting()).toBe(false);
  });

  it('createTopic() clears isSubmitting on error', () => {
    const create$ = new Subject<{ id: string }>();
    topicService.create.and.returnValue(create$);
    fixture.detectChanges();

    component.newTopicName = 'Word';
    component.newTopicDisplayOrder = 2;
    component.createTopic();

    expect(component.isSubmitting()).toBe(true);

    create$.error('boom');

    expect(component.isSubmitting()).toBe(false);
  });

  it('createTopic() sets the Arabic error message when the service errors and leaves topics unchanged', () => {
    topicService.create.and.returnValue(throwError(() => new Error('boom')));
    fixture.detectChanges();

    component.newTopicName = 'Word';
    component.newTopicDisplayOrder = 2;
    component.createTopic();

    expect(component.errorMessage).toBe('تعذّر إنشاء الموضوع.');
    expect(component.isSubmitting()).toBe(false);
    expect(component.topics()).toEqual(sampleTopics);
    expect(topicService.getAll).toHaveBeenCalledTimes(1);
  });

  it('deleteTopic() sets isSubmitting while the delete call is in flight and clears it on success', () => {
    const delete$ = new Subject<void>();
    topicService.delete.and.returnValue(delete$);
    fixture.detectChanges();

    component.deleteTopic('1');

    expect(component.isSubmitting()).toBe(true);

    delete$.next();
    delete$.complete();

    expect(component.isSubmitting()).toBe(false);
  });

  it('deleteTopic() clears isSubmitting on error', () => {
    const delete$ = new Subject<void>();
    topicService.delete.and.returnValue(delete$);
    fixture.detectChanges();

    component.deleteTopic('1');

    expect(component.isSubmitting()).toBe(true);

    delete$.error('boom');

    expect(component.isSubmitting()).toBe(false);
  });

  it('deleteTopic() sets the Arabic error message when the service errors', () => {
    topicService.delete.and.returnValue(throwError(() => new Error('boom')));
    fixture.detectChanges();

    component.deleteTopic('1');

    expect(component.errorMessage).toBe('لا يمكن حذف موضوع يحتوي على أسئلة — عطّله بدلاً من ذلك.');
    expect(component.isSubmitting()).toBe(false);
    expect(topicService.getAll).toHaveBeenCalledTimes(1);
  });
});
