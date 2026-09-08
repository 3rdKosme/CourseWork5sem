import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../core/auth.service';
import { ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';

/** Профиль покупателя и смена пароля (FR-15). */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h1>Профиль</h1>

    <div class="layout-profile">
      <form class="card stack" (ngSubmit)="save()">
        <h3>Личные данные</h3>
        <label>
          <span>E-mail</span>
          <input [value]="auth.user()?.email" disabled />
        </label>
        <label>
          <span>ФИО</span>
          <input name="fullName" [(ngModel)]="fullName" maxlength="200" required />
        </label>
        <label>
          <span>Телефон</span>
          <input name="phone" [(ngModel)]="phone" maxlength="32" />
        </label>
        <label>
          <span>Адрес доставки по умолчанию</span>
          <input name="address" [(ngModel)]="address" maxlength="500" />
        </label>
        <button type="submit" class="btn btn--primary" [disabled]="busy()">Сохранить</button>
      </form>

      <form class="card stack" (ngSubmit)="changePassword()">
        <h3>Смена пароля</h3>
        <label>
          <span>Текущий пароль</span>
          <input type="password" name="current" [(ngModel)]="currentPassword" required />
        </label>
        <label>
          <span>Новый пароль</span>
          <input type="password" name="new" [(ngModel)]="newPassword" required />
        </label>
        <p class="muted">После смены пароля все активные сессии завершаются.</p>
        <button type="submit" class="btn" [disabled]="busy()">Изменить пароль</button>
      </form>
    </div>
  `,
  styles: [
    `
      .layout-profile {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 20px;
        align-items: start;
      }

      @media (max-width: 900px) {
        .layout-profile {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class ProfilePage implements OnInit {
  readonly auth = inject(AuthService);
  private readonly shop = inject(ShopService);
  private readonly toast = inject(ToastService);

  readonly busy = signal(false);
  fullName = '';
  phone = '';
  address = '';
  currentPassword = '';
  newPassword = '';

  ngOnInit(): void {
    this.shop.profile().subscribe((user) => {
      this.auth.updateUser(user);
      this.fullName = user.fullName;
      this.phone = user.phone ?? '';
      this.address = user.defaultAddress ?? '';
    });
  }

  save(): void {
    this.busy.set(true);
    this.shop.updateProfile(this.fullName, this.phone, this.address).subscribe({
      next: (user) => {
        this.auth.updateUser(user);
        this.busy.set(false);
        this.toast.success('Профиль сохранён');
      },
      error: () => this.busy.set(false),
    });
  }

  changePassword(): void {
    this.busy.set(true);
    this.shop.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.busy.set(false);
        this.currentPassword = '';
        this.newPassword = '';
        this.toast.success('Пароль изменён, войдите заново');
        this.auth.logout();
      },
      error: () => this.busy.set(false),
    });
  }
}
