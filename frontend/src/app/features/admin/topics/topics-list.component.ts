import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Topic, TopicService } from '../../../core/services/topic.service';

@Component({
  selector: 'app-topics-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './topics-list.component.html'
})
export class TopicsListComponent implements OnInit {
  topics = signal<Topic[]>([]);
  newTopicName = '';
  newTopicDisplayOrder = 1;
  errorMessage: string | null = null;

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
    this.errorMessage = null;
    this.topicService.create({ name: this.newTopicName, displayOrder: this.newTopicDisplayOrder }).subscribe({
      next: () => {
        this.newTopicName = '';
        this.newTopicDisplayOrder = 1;
        this.load();
      },
      error: () => (this.errorMessage = 'تعذّر إنشاء الموضوع.')
    });
  }

  deleteTopic(id: string): void {
    this.errorMessage = null;
    this.topicService.delete(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'لا يمكن حذف موضوع يحتوي على أسئلة — عطّله بدلاً من ذلك.')
    });
  }
}
