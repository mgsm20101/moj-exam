import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
});
