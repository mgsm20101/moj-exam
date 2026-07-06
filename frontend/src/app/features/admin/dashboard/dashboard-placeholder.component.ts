import { Component } from '@angular/core';

@Component({
  selector: 'app-dashboard-placeholder',
  standalone: true,
  template: `
    <div class="page dashboard-empty">
      <div class="card dashboard-welcome">
        <h1>مرحبًا بك في لوحة التحكم</h1>
        <p>تم تسجيل الدخول بنجاح. ابدأ بإدارة الموضوعات وبنك الأسئلة، ثم كوّن الامتحانات وانشرها.</p>
        <p class="muted">لوحة المؤشرات الكاملة تُبنى في المرحلة القادمة.</p>
      </div>
    </div>
  `
})
export class DashboardPlaceholderComponent {}
