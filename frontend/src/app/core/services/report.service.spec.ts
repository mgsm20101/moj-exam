import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ExamResultsReport, ReportService } from './report.service';
import { environment } from '../../../environments/environment';

describe('ReportService', () => {
  let service: ReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ReportService]
    });
    service = TestBed.inject(ReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getExamResults() requests the exam report with the filter query param', () => {
    const mock = { rows: [] } as unknown as ExamResultsReport;
    service.getExamResults('e1', 'passed').subscribe(res => expect(res).toEqual(mock));

    const req = httpMock.expectOne(r =>
      r.url === `${environment.apiBaseUrl}/admin/reports/exams/e1/results` && r.params.get('filter') === 'passed');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });

  it('getExamResults() defaults the filter to "all"', () => {
    service.getExamResults('e1').subscribe();

    const req = httpMock.expectOne(r =>
      r.url === `${environment.apiBaseUrl}/admin/reports/exams/e1/results` && r.params.get('filter') === 'all');
    expect(req.request.method).toBe('GET');
    req.flush({} as ExamResultsReport);
  });

  it('exportExamResults() requests a blob with the filter query param', () => {
    const blob = new Blob(['x'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    service.exportExamResults('e1', 'failed').subscribe(res => expect(res).toEqual(blob));

    const req = httpMock.expectOne(r =>
      r.url === `${environment.apiBaseUrl}/admin/reports/exams/e1/results/export` && r.params.get('filter') === 'failed');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(blob);
  });
});
