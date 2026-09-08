import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin.service';
import { AdminUser, Paged, Role } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { PaginationComponent } from '../../shared/ui';
import { AdminNavComponent } from './dashboard.page';

/** Управление пользователями и ролями — только администратор (FR-16). */
@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminNavComponent, PaginationComponent],
  template: `
    <app-admin-nav />
    <h1>Пользователи</h1>

    <div class="card row" style="margin-bottom:16px">
      <input placeholder="Поиск по e-mail или ФИО" [(ngModel)]="search" (keyup.enter)="load(1)" style="flex:1" />
      <button type="button" class="btn" (click)="load(1)">Найти</button>
    </div>

    @if (page(); as data) {
      <div class="card table-wrapper">
        <table>
          <thead>
            <tr>
              <th>E-mail</th><th>ФИО</th><th>Телефон</th><th>Роль</th>
              <th>Заказов</th><th>Регистрация</th><th>Статус</th><th></th>
            </tr>
          </thead>
          <tbody>
            @for (user of data.items; track user.id) {
              <tr>
                <td>{{ user.email }}</td>
                <td>{{ user.fullName }}</td>
                <td class="muted">{{ user.phone }}</td>
                <td>
                  <select [ngModel]="user.role" (ngModelChange)="changeRole(user, $event)" style="width:130px">
                    <option value="Customer">Покупатель</option>
                    <option value="Manager">Менеджер</option>
                    <option value="Admin">Администратор</option>
                  </select>
                </td>
                <td>{{ user.ordersCount }}</td>
                <td>{{ user.createdAt | date: 'dd.MM.yyyy' }}</td>
                <td>
                  <span [class]="user.isActive ? 'status status--Completed' : 'status status--Cancelled'">
                    {{ user.isActive ? 'активен' : 'заблокирован' }}
                  </span>
                </td>
                <td class="text-right">
                  <button type="button" class="btn btn--sm" (click)="toggle(user)">
                    {{ user.isActive ? 'Заблокировать' : 'Разблокировать' }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <app-pagination [page]="data.page" [totalPages]="data.totalPages" (pageChange)="load($event)" />
    }
  `,
})
export class AdminUsersPage implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly toast = inject(ToastService);

  readonly page = signal<Paged<AdminUser> | null>(null);
  search = '';

  ngOnInit(): void {
    this.load(1);
  }

  load(page: number): void {
    this.admin.users(this.search, page).subscribe((result) => this.page.set(result));
  }

  changeRole(user: AdminUser, role: Role): void {
    if (role === user.role) return;

    this.admin.changeRole(user.id, role).subscribe({
      next: () => {
        this.toast.success(`Роль ${user.email} изменена`);
        this.load(this.page()?.page ?? 1);
      },
      error: () => this.load(this.page()?.page ?? 1),
    });
  }

  toggle(user: AdminUser): void {
    this.admin.toggleActive(user.id).subscribe(() => {
      this.toast.info(`Учётная запись ${user.email} обновлена`);
      this.load(this.page()?.page ?? 1);
    });
  }
}
