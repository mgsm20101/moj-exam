import { Component } from '@angular/core';

@Component({
  selector: 'app-attempt-shell',
  standalone: true,
  template: `
    <div class="candidate-card">
      <div class="candidate-state">
        <h1 style="font-size: var(--fs-headline);">تم بدء المحاولة</h1>
        <p class="muted">مُشغّل الأسئلة يُبنى في المرحلة التالية (Slice 1b).</p>
      </div>
    </div>
  `
})
export class AttemptShellComponent {}
