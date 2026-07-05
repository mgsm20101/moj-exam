import { ChangeDetectionStrategy, Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Topic, TopicService } from '../../../core/services/topic.service';

@Component({
  selector: 'app-topics-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './topics-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TopicsListComponent implements OnInit {
  topics = signal<Topic[]>([]);
  newTopicName = '';
  newTopicDisplayOrder = 1;
  errorMessage: string | null = null;
  isSubmitting = signal(false);

  constructor(private readonly topicService: TopicService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.topicService.getAll().subscribe(topics => this.topics.set(topics));
  }

  createTopic(): void {
    if (!this.newTopicName.trim()) {
      return;
    }
    if (!Number.isInteger(this.newTopicDisplayOrder) || this.newTopicDisplayOrder <= 0) {
      this.errorMessage = 'ترتيب العرض يجب أن يكون رقمًا صحيحًا موجبًا.';
      return;
    }
    this.errorMessage = null;
    this.isSubmitting.set(true);
    this.topicService.create({ name: this.newTopicName, displayOrder: this.newTopicDisplayOrder }).subscribe({
      next: () => {
        this.newTopicName = '';
        this.newTopicDisplayOrder = 1;
        this.isSubmitting.set(false);
        this.load();
      },
      error: () => {
        this.errorMessage = 'تعذّر إنشاء الموضوع.';
        this.isSubmitting.set(false);
      }
    });
  }

  deleteTopic(id: string): void {
    this.errorMessage = null;
    this.isSubmitting.set(true);
    this.topicService.delete(id).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.load();
      },
      error: () => {
        this.errorMessage = 'لا يمكن حذف موضوع يحتوي على أسئلة — عطّله بدلاً من ذلك.';
        this.isSubmitting.set(false);
      }
    });
  }
}
