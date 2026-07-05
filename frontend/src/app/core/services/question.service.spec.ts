import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { QuestionService, Question } from './question.service';
import { environment } from '../../../environments/environment';

describe('QuestionService', () => {
  let service: QuestionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [QuestionService]
    });
    service = TestBed.inject(QuestionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() builds query params from filters', () => {
    const mock: Question[] = [];
    service.getAll({ topicId: 't1', difficulty: 'Hard' }).subscribe(res => expect(res).toEqual(mock));

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/admin/questions`
      && r.params.get('topicId') === 't1' && r.params.get('difficulty') === 'Hard');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });

  it('create() posts a question payload', () => {
    service.create({ topicId: 't1', type: 'FillBlank', difficulty: 'Medium', text: 'Fill ___', correctAnswerText: 'server' })
      .subscribe(res => expect(res.id).toBe('q1'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/questions`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'q1' });
  });

  it('deactivate() deletes the question id', () => {
    service.deactivate('q1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/questions/q1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('uploadImage() posts multipart form data', () => {
    const file = new File(['x'], 'pic.png', { type: 'image/png' });
    service.uploadImage(file).subscribe(res => expect(res.url).toBe('/question-images/abc.png'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/questions/image`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBeTrue();
    req.flush({ url: '/question-images/abc.png' });
  });
});
