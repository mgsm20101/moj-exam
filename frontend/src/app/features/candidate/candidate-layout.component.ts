import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-candidate-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="candidate-surface">
      <router-outlet />
    </div>
  `
})
export class CandidateLayoutComponent {}
