import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ExamResultsReportComponent } from './exam-results-report.component';
import { ExamService } from '../../../core/services/exam.service';
import { ExamResultRow, ExamResultsReport, ReportService } from '../../../core/services/report.service';

function row(fullName: string, nationalId: string, score: number): ExamResultRow {
  return {
    fullName, nationalId, mobileNumber: '01000000000', score, totalPoints: 100,
    scorePercentage: score, passed: score >= 60, submittedAtUtc: null, governorateCode: 1,
    tabSwitchCount: 0, hasActiveRetakeGrant: false, attemptId: nationalId
  };
}

describe('ExamResultsReportComponent (search & sort)', () => {
  let fixture: ComponentFixture<ExamResultsReportComponent>;
  let component: ExamResultsReportComponent;

  const report: ExamResultsReport = {
    examId: 'e1', examName: 'Exam', totalPoints: 100, passMarkPercentage: 60, passMarkPoints: 60,
    filter: 'all',
    summary: { totalCandidates: 3, passedCount: 2, failedCount: 1, passRatePercentage: 66.67 },
    rows: [row('محمد', '300', 90), row('أحمد', '100', 55), row('خالد', '200', 70)]
  };

  beforeEach(async () => {
    const examService = jasmine.createSpyObj('ExamService', ['getAll']);
    examService.getAll.and.returnValue(of([]));
    const reportService = jasmine.createSpyObj('ReportService', ['getExamResults', 'exportExamResults', 'grantRetake', 'getAttemptReview']);

    await TestBed.configureTestingModule({
      imports: [ExamResultsReportComponent],
      providers: [
        { provide: ExamService, useValue: examService },
        { provide: ReportService, useValue: reportService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ExamResultsReportComponent);
    component = fixture.componentInstance;
    component.report.set(report);
  });

  it('defaults to sorting by score descending', () => {
    expect(component.sortedRows().map(r => r.score)).toEqual([90, 70, 55]);
  });

  it('sorts by name ascending then descending on repeat clicks', () => {
    component.setSort('fullName');
    expect(component.sortedRows().map(r => r.fullName)).toEqual(['أحمد', 'خالد', 'محمد']);

    component.setSort('fullName');
    expect(component.sortedRows().map(r => r.fullName)).toEqual(['محمد', 'خالد', 'أحمد']);
  });

  it('sorts by national id numerically ascending', () => {
    component.setSort('nationalId');
    expect(component.sortedRows().map(r => r.nationalId)).toEqual(['100', '200', '300']);
  });

  it('applies the name search filter before sorting', () => {
    component.nameQuery.set('أحمد');
    component.setSort('fullName');
    expect(component.sortedRows().map(r => r.fullName)).toEqual(['أحمد']);
  });
});
