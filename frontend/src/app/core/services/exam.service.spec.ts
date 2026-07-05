import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ExamService, ExamSummary, ExamDetail, ExamInput } from './exam.service';
import { environment } from '../../../environments/environment';

describe('ExamService', () => {
  let service: ExamService;
  let httpMock: HttpTestingController;

  const sampleInput: ExamInput = {
    name: 'Excel Basics',
    description: null,
    startAtUtc: '2026-08-01T00:00:00Z',
    endAtUtc: '2026-08-08T00:00:00Z',
    durationMinutes: 60,
    mcqPoints: 2,
    trueFalsePoints: 1,
    fillBlankPoints: 5,
    passMarkPercentage: 60,
    maxAttempts: 1,
    shuffleAnswers: true,
    showResultImmediately: true,
    allowBackNavigation: true,
    topicSelections: [{ topicId: '1', displayOrder: 1, difficulty: 'Medium', type: 'Mcq', count: 25 }]
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ExamService]
    });
    service = TestBed.inject(ExamService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() fetches the exam list', () => {
    const mockExams: ExamSummary[] = [
      {
        id: '1',
        name: 'Excel Basics',
        startAtUtc: '2026-08-01T00:00:00Z',
        endAtUtc: '2026-08-08T00:00:00Z',
        durationMinutes: 60,
        status: 'Draft',
        totalQuestionCount: 30,
        totalPoints: 75
      }
    ];

    service.getAll().subscribe(exams => expect(exams).toEqual(mockExams));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams`);
    expect(req.request.method).toBe('GET');
    req.flush(mockExams);
  });

  it('getById() fetches a single exam detail', () => {
    const mockDetail: ExamDetail = { ...sampleInput, id: '1', status: 'Draft', topicSelections: [] };

    service.getById('1').subscribe(exam => expect(exam).toEqual(mockDetail));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams/1`);
    expect(req.request.method).toBe('GET');
    req.flush(mockDetail);
  });

  it('create() posts a new exam', () => {
    service.create(sampleInput).subscribe(res => expect(res.id).toBe('1'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: '1' });
  });

  it('update() puts to the exam id', () => {
    service.update('1', sampleInput).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams/1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('delete() deletes the exam id', () => {
    service.delete('1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('publish() posts to the publish action', () => {
    service.publish('1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams/1/publish`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('close() posts to the close action', () => {
    service.close('1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams/1/close`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('archive() posts to the archive action', () => {
    service.archive('1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/exams/1/archive`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });
});
