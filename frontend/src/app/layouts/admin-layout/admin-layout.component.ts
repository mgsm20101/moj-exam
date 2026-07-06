import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin-shell">
      <header class="admin-header">نظام الامتحانات — لوحة التحكم</header>
      <nav class="admin-nav">
        <a routerLink="/admin/dashboard" routerLinkActive="active">الرئيسية</a>
        <a routerLink="/admin/topics" routerLinkActive="active">الموضوعات</a>
        <a routerLink="/admin/questions" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">بنك الأسئلة</a>
        <a routerLink="/admin/questions/import" routerLinkActive="active">استيراد بالجملة</a>
        <a routerLink="/admin/exams" routerLinkActive="active">الامتحانات</a>
      </nav>
      <main class="admin-content">
        <router-outlet />
      </main>
    </div>
  `
})
export class AdminLayoutComponent {}
