import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AttemptTokenStore } from '../services/attempt-token.store';

/** Attaches the attempt token to candidate exam calls that target a specific exam id in the path. */
export const attemptTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const match = req.url.match(/\/api\/exam\/([0-9a-fA-F-]+)\//);
  if (!match) {
    return next(req);
  }
  const token = inject(AttemptTokenStore).get(match[1]);
  if (!token) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
