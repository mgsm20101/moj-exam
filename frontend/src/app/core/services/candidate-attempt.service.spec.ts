import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CandidateAttemptService } from './candidate-attempt.service';
import { environment } from '../../../environments/environment';

describe('CandidateAttemptService', () => {
  let service: CandidateAttemptService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [CandidateAttemptService] });
    service = TestBed.inject(CandidateAttemptService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('posts an answer to the attempt answer endpoint', () => {
    const examId = 'e1';
    service.saveAnswer(examId, { attemptQuestionId: 'q1', selectedOptionId: 'o1', answerText: null, isFlagged: false })
      .subscribe();
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/exam/${examId}/attempt/answer`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.attemptQuestionId).toBe('q1');
    req.flush(null);
  });

  it('submits the attempt and returns the result', () => {
    const examId = 'e1';
    let passed: boolean | undefined;
    service.submit(examId).subscribe(r => (passed = r.passed));
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/exam/${examId}/attempt/submit`);
    req.flush({ shown: true, score: 4, totalPoints: 4, passMarkPercentage: 60, passed: true });
    expect(passed).toBeTrue();
  });
});
