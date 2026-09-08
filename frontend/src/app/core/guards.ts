import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { ToastService } from './toast.service';

/** Доступ только аутентифицированным пользователям (раздел 2.2 ТЗ). */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) return true;

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Доступ сотрудникам магазина: Manager или Admin. */
export const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);

  if (auth.isStaff()) return true;

  toast.error('Раздел доступен только сотрудникам магазина');
  return router.createUrlTree(['/']);
};

/** Доступ только администратору. */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);

  if (auth.isAdmin()) return true;

  toast.error('Раздел доступен только администратору');
  return router.createUrlTree(['/admin']);
};
