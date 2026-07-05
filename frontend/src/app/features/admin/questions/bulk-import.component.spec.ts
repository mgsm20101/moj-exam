import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { BulkImportComponent } from './bulk-import.component';
import { QuestionService, BulkImportReport, QuestionBankSummaryRow } from '../../../core/services/question.service';

describe('BulkImportComponent', () => {
  let fixture: ComponentFixture<BulkImportComponent>;
  let component: BulkImportComponent;
  let questionService: jasmine.SpyObj<QuestionService>;

  const summary: QuestionBankSummaryRow[] = [{ topicName: 'Excel', difficulty: 'Medium', mcqCount: 50, fillBlankCount: 9 }];
  const report: BulkImportReport = { totalRows: 2, successCount: 1, failureCount: 1, errors: [{ sheet: 'FillBlank', rowNumber: 3, message: 'bad format' }] };

  beforeEach(async () => {
    questionService = jasmine.createSpyObj('QuestionService', ['bulkImport', 'getSummary']);
    questionService.getSummary.and.returnValue(of(summary));

    await TestBed.configureTestingModule({
      imports: [BulkImportComponent],
      providers: [{ provide: QuestionService, useValue: questionService }]
    }).compileComponents();

    fixture = TestBed.createComponent(BulkImportComponent);
    component = fixture.componentInstance;
  });

  it('loads the coverage summary on init', () => {
    fixture.detectChanges();
    expect(component.summary()).toEqual(summary);
  });

  it('uploads the selected file and shows the report, then refreshes the summary', () => {
    questionService.bulkImport.and.returnValue(of(report));
    fixture.detectChanges();
    questionService.getSummary.calls.reset();

    const file = new File(['x'], 'questions.xlsx');
    component.selectedFile = file;
    component.upload();

    expect(questionService.bulkImport).toHaveBeenCalledWith(file);
    expect(component.report()).toEqual(report);
    expect(questionService.getSummary).toHaveBeenCalled();
  });
});
