import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CandidateExamService } from './candidate-exam.service';
import { environment } from '../../../environments/environment';

describe('CandidateExamService', () => {
  let service: CandidateExamService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [CandidateExamService] });
    service = TestBed.inject(CandidateExamService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts identity to the start endpoint and returns the attempt token', () => {
    const examId = 'abc';
    let result: string | null | undefined;
    service.start(examId, { fullName: 'a b c d', nationalId: '29912310123404', mobileNumber: '01012345678' })
      .subscribe(r => (result = r.attemptToken));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/exam/${examId}/start`);
    expect(req.request.method).toBe('POST');
    req.flush({ outcome: 'Started', attemptId: 'id1', attemptToken: 'tok1', expiresAtUtc: '2026-07-10T10:00:00Z', queuePosition: null });

    expect(result).toBe('tok1');
  });
});
