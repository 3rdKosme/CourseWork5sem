import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { CartService } from '../core/cart.service';
import { ToastService } from '../core/toast.service';

/** Вход в систему (FR-12). */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="auth card">
      <h1>Вход</h1>

      <form (ngSubmit)="submit()">
        <label>
          <span>E-mail</span>
          <input type="email" name="email" [(ngModel)]="email" required autocomplete="username" />
        </label>
        <label>
          <span>Пароль</span>
          <input type="password" name="password" [(ngModel)]="password" required autocomplete="current-password" />
        </label>
        <button type="submit" class="btn btn--primary" [disabled]="busy()">
          {{ busy() ? 'Проверяем…' : 'Войти' }}
        </button>
      </form>

      <p class="muted">Нет аккаунта? <a routerLink="/register">Зарегистрируйтесь</a></p>
      <p class="muted demo">
        Демо-доступы: user&#64;periphshop.local / User123!, manager&#64;periphshop.local / Manager123!,
        admin&#64;periphshop.local / Admin123!
      </p>
    </div>
  `,
  styles: [
    `
      .auth {
        max-width: 460px;
        margin: 40px auto;
      }

      .demo {
        font-size: 13px;
      }
    `,
  ],
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly cart = inject(CartService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly busy = signal(false);
  email = '';
  password = '';

  submit(): void {
    if (!this.email || !this.password) return;

    this.busy.set(true);
    this.auth.login(this.email, this.password).subscribe({
      next: (response) => {
        this.busy.set(false);
        this.toast.success(`Здравствуйте, ${response.user.fullName}`);
        this.cart.load().subscribe({ error: () => undefined });

        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        void this.router.navigateByUrl(returnUrl ?? (this.auth.isStaff() ? '/admin' : '/'));
      },
      error: () => this.busy.set(false),
    });
  }
}

/** Регистрация покупателя (FR-10, FR-11). */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="auth card">
      <h1>Регистрация</h1>

      <form (ngSubmit)="submit()">
        <label>
          <span>ФИО</span>
          <input name="fullName" [(ngModel)]="fullName" required maxlength="200" />
        </label>
        <label>
          <span>E-mail</span>
          <input type="email" name="email" [(ngModel)]="email" required autocomplete="username" />
        </label>
        <label>
          <span>Телефон</span>
          <input name="phone" [(ngModel)]="phone" maxlength="32" placeholder="+7 900 000-00-00" />
        </label>
        <label>
          <span>Пароль (не менее 8 символов, буква и цифра)</span>
          <input type="password" name="password" [(ngModel)]="password" required autocomplete="new-password" />
        </label>
        <button type="submit" class="btn btn--primary" [disabled]="busy()">
          {{ busy() ? 'Создаём аккаунт…' : 'Зарегистрироваться' }}
        </button>
      </form>

      <p class="muted">Уже есть аккаунт? <a routerLink="/login">Войти</a></p>
    </div>
  `,
  styles: [
    `
      .auth {
        max-width: 460px;
        margin: 40px auto;
      }
    `,
  ],
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly cart = inject(CartService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly busy = signal(false);
  fullName = '';
  email = '';
  phone = '';
  password = '';

  submit(): void {
    this.busy.set(true);
    this.auth.register(this.email, this.password, this.fullName, this.phone).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success('Аккаунт создан');
        this.cart.merge().subscribe({ error: () => undefined });
        void this.router.navigate(['/']);
      },
      error: () => this.busy.set(false),
    });
  }
}
