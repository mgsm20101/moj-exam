import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  template: `
    <div class="admin-shell">
      <header class="admin-header">نظام الامتحانات — لوحة التحكم</header>
      <nav class="admin-nav">
        <a routerLink="/admin/dashboard">الرئيسية</a>
        <a routerLink="/admin/topics">الموضوعات</a>
        <a routerLink="/admin/questions">بنك الأسئلة</a>
        <a routerLink="/admin/questions/import">استيراد بالجملة</a>
      </nav>
      <main class="admin-content">
        <router-outlet />
      </main>
    </div>
  `
})
export class AdminLayoutComponent {}
