import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'exam/:examId',
    loadChildren: () => import('./features/candidate/candidate.routes').then(m => m.candidateRoutes)
  },
  {
    path: 'admin',
    canActivate: [authGuard],
    loadComponent: () => import('./layouts/admin-layout/admin-layout.component').then(m => m.AdminLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/admin/dashboard/dashboard-placeholder.component').then(m => m.DashboardPlaceholderComponent)
      },
      {
        path: 'topics',
        loadComponent: () =>
          import('./features/admin/topics/topics-list.component').then(m => m.TopicsListComponent)
      },
      {
        path: 'questions',
        loadComponent: () =>
          import('./features/admin/questions/questions-list.component').then(m => m.QuestionsListComponent)
      },
      {
        path: 'questions/import',
        loadComponent: () =>
          import('./features/admin/questions/bulk-import.component').then(m => m.BulkImportComponent)
      },
      {
        path: 'exams',
        loadComponent: () =>
          import('./features/admin/exams/exams-list.component').then(m => m.ExamsListComponent)
      }
    ]
  }
];
