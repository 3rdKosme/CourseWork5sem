import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { getOrCreateCartId } from './cart.service';
import { ToastService } from './toast.service';

/** Подставляет JWT и идентификатор анонимной корзины в каждый запрос к API. */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api')) return next(req);

  const auth = inject(AuthService);
  const headers: Record<string, string> = { 'X-Cart-Id': getOrCreateCartId() };

  const token = auth.accessToken;
  if (token && !req.url.includes('/api/auth/refresh')) headers['Authorization'] = `Bearer ${token}`;

  return next(req.clone({ setHeaders: headers }));
};

/**
 * Единая обработка ошибок: одна попытка обновления токена при 401,
 * читаемые сообщения из problem+json — в toast (раздел 7 ТЗ).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toast = inject(ToastService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthCall = req.url.includes('/api/auth/');

      if (error.status === 401 && !isAuthCall && auth.refreshToken) {
        return auth.refresh().pipe(
          switchMap(() =>
            next(req.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken ?? ''}` } })),
          ),
          catchError(() => {
            auth.logout(false);
            void router.navigate(['/login']);
            toast.error('Сессия истекла, войдите заново');
            return throwError(() => error);
          }),
        );
      }

      if (error.status === 401 && !isAuthCall) {
        auth.logout(false);
        void router.navigate(['/login']);
      }

      toast.error(describe(error));
      return throwError(() => error);
    }),
  );
};

/** Человекочитаемое описание ответа RFC 7807. */
export function describe(error: HttpErrorResponse): string {
  if (error.status === 0) return 'Сервер недоступен, проверьте подключение';

  const problem = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
  if (problem?.errors) {
    const fields = Object.values(problem.errors).flat();
    if (fields.length > 0) return fields.join('; ');
  }

  if (problem?.title) return problem.detail ? `${problem.title}: ${problem.detail}` : problem.title;

  return `Ошибка ${error.status}`;
}
