import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Topic } from '../../../core/services/topic.service';
import { Difficulty, QuestionInput, QuestionType } from '../../../core/services/question.service';

@Component({
  selector: 'app-question-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './question-form.component.html'
})
export class QuestionFormComponent {
  @Input() topics: Topic[] = [];
  @Output() save = new EventEmitter<QuestionInput>();
  @Output() imageFileSelected = new EventEmitter<File>();

  readonly validationError = signal<string | null>(null);

  readonly form: FormGroup = this.fb.group({
    topicId: ['', Validators.required],
    type: ['Mcq' as QuestionType, Validators.required],
    difficulty: ['Medium' as Difficulty, Validators.required],
    text: ['', Validators.required],
    imageUrl: [''],
    correctAnswerText: ['']
  });

  constructor(private readonly fb: FormBuilder) {
    this.form.addControl('options', this.fb.array([this.buildOption(), this.buildOption()]));
  }

  get options(): FormArray {
    return this.form.get('options') as FormArray;
  }

  private buildOption() {
    return this.fb.group({ text: [''], isCorrect: [false] });
  }

  onCorrectAnswerInput(rawValue: string): void {
    const normalized = rawValue.replace(/\s+/g, '').toLowerCase();
    this.form.patchValue({ correctAnswerText: normalized });
  }

  onImageFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.imageFileSelected.emit(input.files[0]);
    }
  }

  setImageUrl(url: string): void {
    this.form.patchValue({ imageUrl: url });
  }

  selectCorrectOption(index: number): void {
    this.options.controls.forEach((control, i) => control.patchValue({ isCorrect: i === index }));
  }

  submit(): void {
    const value = this.form.value;
    const type: QuestionType = value.type;

    if (!value.topicId || !value.text) {
      this.validationError.set('اختر الموضوع واكتب نص السؤال.');
      return;
    }

    if (type === 'FillBlank') {
      if (!value.correctAnswerText || !/^[a-z0-9]+$/.test(value.correctAnswerText)) {
        this.validationError.set('الإجابة يجب أن تكون كلمة واحدة بحروف إنجليزية صغيرة وأرقام فقط.');
        return;
      }
      this.validationError.set(null);
      this.save.emit({
        topicId: value.topicId,
        type,
        difficulty: value.difficulty,
        text: value.text,
        imageUrl: value.imageUrl || null,
        correctAnswerText: value.correctAnswerText
      });
      return;
    }

    const options = this.options.controls.map(c => c.value as { text: string; isCorrect: boolean });
    const filledOptions = options.filter(o => o.text?.trim());
    const correctCount = filledOptions.filter(o => o.isCorrect).length;

    if (filledOptions.length < 2 || correctCount !== 1) {
      this.validationError.set('أدخل اختيارين على الأقل وحدد إجابة صحيحة واحدة.');
      return;
    }

    this.validationError.set(null);
    this.save.emit({
      topicId: value.topicId,
      type,
      difficulty: value.difficulty,
      text: value.text,
      imageUrl: value.imageUrl || null,
      options: filledOptions
    });
  }

  resetForm(): void {
    this.form.reset({
      topicId: '',
      type: 'Mcq' as QuestionType,
      difficulty: 'Medium' as Difficulty,
      text: '',
      imageUrl: '',
      correctAnswerText: ''
    });
    this.options.clear();
    this.options.push(this.buildOption());
    this.options.push(this.buildOption());
    this.validationError.set(null);
  }
}
