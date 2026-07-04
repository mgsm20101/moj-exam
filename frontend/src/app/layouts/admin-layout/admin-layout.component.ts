import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="admin-shell">
      <header class="admin-header">نظام الامتحانات — لوحة التحكم</header>
      <main class="admin-content">
        <router-outlet />
      </main>
    </div>
  `
})
export class AdminLayoutComponent {}
