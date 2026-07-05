import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TopicService, Topic } from './topic.service';
import { environment } from '../../../environments/environment';

describe('TopicService', () => {
  let service: TopicService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [TopicService]
    });
    service = TestBed.inject(TopicService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() fetches the topic list', () => {
    const mockTopics: Topic[] = [{ id: '1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 5 }];

    service.getAll().subscribe(topics => expect(topics).toEqual(mockTopics));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics`);
    expect(req.request.method).toBe('GET');
    req.flush(mockTopics);
  });

  it('create() posts a new topic', () => {
    service.create({ name: 'Word', displayOrder: 2 }).subscribe(res => expect(res.id).toBe('2'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: '2' });
  });

  it('update() puts to the topic id', () => {
    service.update('1', { name: 'Excel', displayOrder: 1, isActive: false }).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics/1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('delete() deletes the topic id', () => {
    service.delete('1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
