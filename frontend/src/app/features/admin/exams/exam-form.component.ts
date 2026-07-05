import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Topic } from '../../../core/services/topic.service';
import { Difficulty, ExamDetail, ExamInput, ExamTopicSelectionInput, QuestionType } from '../../../core/services/exam.service';

interface MatrixCell {
  control: string;
  difficulty: Difficulty;
  type: QuestionType;
  label: string;
}

const MATRIX_CELLS: MatrixCell[] = [
  { control: 'easyMcq', difficulty: 'Easy', type: 'Mcq', label: 'سهل / اختيار' },
  { control: 'easyFill', difficulty: 'Easy', type: 'FillBlank', label: 'سهل / أكمل' },
  { control: 'mediumMcq', difficulty: 'Medium', type: 'Mcq', label: 'متوسط / اختيار' },
  { control: 'mediumFill', difficulty: 'Medium', type: 'FillBlank', label: 'متوسط / أكمل' },
  { control: 'hardMcq', difficulty: 'Hard', type: 'Mcq', label: 'متقدم / اختيار' },
  { control: 'hardFill', difficulty: 'Hard', type: 'FillBlank', label: 'متقدم / أكمل' }
];

const DEFAULT_FORM_VALUES = {
  name: '',
  description: '',
  startAtUtc: '',
  endAtUtc: '',
  durationMinutes: 60,
  mcqPoints: 2,
  trueFalsePoints: 1,
  fillBlankPoints: 5,
  passMarkPercentage: 60,
  maxAttempts: 1,
  shuffleAnswers: true,
  showResultImmediately: true,
  allowBackNavigation: true
};

@Component({
  selector: 'app-exam-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './exam-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExamFormComponent implements OnInit, OnChanges {
  @Input() topics: Topic[] = [];
  @Input() initialValue: ExamDetail | null = null;
  @Output() save = new EventEmitter<ExamInput>();

  readonly matrixCells = MATRIX_CELLS;
  readonly validationError = signal<string | null>(null);

  readonly form: FormGroup = this.fb.group({
    ...DEFAULT_FORM_VALUES,
    topicRows: this.fb.array([])
  });

  constructor(private readonly fb: FormBuilder) {}

  ngOnInit(): void {
    this.rebuildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['topics'] || changes['initialValue']) {
      this.rebuildForm();
    }
  }

  get topicRows(): FormArray {
    return this.form.get('topicRows') as FormArray;
  }

  private rebuildForm(): void {
    const rows = this.topics.map(topic => {
      const group: Record<string, unknown> = {};
      for (const cell of MATRIX_CELLS) {
        const match = this.initialValue?.topicSelections.find(
          s => s.topicId === topic.id && s.difficulty === cell.difficulty && s.type === cell.type
        );
        group[cell.control] = [match?.count ?? 0];
      }
      return this.fb.group(group);
    });
    this.form.setControl('topicRows', this.fb.array(rows));

    if (this.initialValue) {
      this.form.patchValue({
        name: this.initialValue.name,
        description: this.initialValue.description,
        startAtUtc: this.toLocalInputValue(this.initialValue.startAtUtc),
        endAtUtc: this.toLocalInputValue(this.initialValue.endAtUtc),
        durationMinutes: this.initialValue.durationMinutes,
        mcqPoints: this.initialValue.mcqPoints,
        trueFalsePoints: this.initialValue.trueFalsePoints,
        fillBlankPoints: this.initialValue.fillBlankPoints,
        passMarkPercentage: this.initialValue.passMarkPercentage,
        maxAttempts: this.initialValue.maxAttempts,
        shuffleAnswers: this.initialValue.shuffleAnswers,
        showResultImmediately: this.initialValue.showResultImmediately,
        allowBackNavigation: this.initialValue.allowBackNavigation
      });
    } else {
      this.form.patchValue(DEFAULT_FORM_VALUES);
    }
  }

  // datetime-local renders/parses as browser-local wall-clock time with no timezone info,
  // so this must build the string from local getters -- slicing the UTC ISO string would
  // silently shift the displayed and resubmitted time by the admin's UTC offset.
  private toLocalInputValue(isoUtc: string): string {
    const date = new Date(isoUtc);
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  submit(): void {
    const value = this.form.value;

    if (!value.name || !value.startAtUtc || !value.endAtUtc) {
      this.validationError.set('الاسم وتاريخ البداية والنهاية مطلوبة.');
      return;
    }

    if (new Date(value.endAtUtc) <= new Date(value.startAtUtc)) {
      this.validationError.set('تاريخ النهاية يجب أن يكون بعد تاريخ البداية.');
      return;
    }

    const topicSelections: ExamTopicSelectionInput[] = [];
    this.topics.forEach((topic, index) => {
      const row = value.topicRows[index];
      MATRIX_CELLS.forEach(cell => {
        const count = Number(row[cell.control]);
        if (count > 0) {
          topicSelections.push({ topicId: topic.id, displayOrder: index + 1, difficulty: cell.difficulty, type: cell.type, count });
        }
      });
    });

    if (topicSelections.length === 0) {
      this.validationError.set('حدد عدد أسئلة واحد على الأقل من موضوع ومستوى صعوبة.');
      return;
    }

    this.validationError.set(null);
    this.save.emit({
      name: value.name,
      description: value.description || null,
      startAtUtc: new Date(value.startAtUtc).toISOString(),
      endAtUtc: new Date(value.endAtUtc).toISOString(),
      durationMinutes: Number(value.durationMinutes),
      mcqPoints: Number(value.mcqPoints),
      trueFalsePoints: Number(value.trueFalsePoints),
      fillBlankPoints: Number(value.fillBlankPoints),
      passMarkPercentage: Number(value.passMarkPercentage),
      maxAttempts: Number(value.maxAttempts),
      shuffleAnswers: value.shuffleAnswers,
      showResultImmediately: value.showResultImmediately,
      allowBackNavigation: value.allowBackNavigation,
      topicSelections
    });
  }
}
