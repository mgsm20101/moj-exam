import { Routes } from '@angular/router';

export const candidateRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./candidate-layout.component').then(m => m.CandidateLayoutComponent),
    children: [
      {
        path: '',
        loadComponent: () => import('./exam-landing.component').then(m => m.ExamLandingComponent)
      },
      {
        path: 'instructions',
        loadComponent: () => import('./instructions.component').then(m => m.InstructionsComponent)
      },
      {
        path: 'attempt',
        loadComponent: () => import('./attempt-player.component').then(m => m.AttemptPlayerComponent)
      }
    ]
  }
];
