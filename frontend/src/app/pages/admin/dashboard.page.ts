import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AdminService } from '../../core/admin.service';
import { AuthService } from '../../core/auth.service';
import { LowStock, ORDER_STATUS_LABELS, SalesByDay, SalesSummary, TopProduct } from '../../core/models';
import { MoneyComponent } from '../../shared/ui';

/** Панель навигации рабочего места (раздел 8.1 ТЗ). */
@Component({
  selector: 'app-admin-nav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <nav class="tabs">
      <a routerLink="/admin" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Сводка</a>
      <a routerLink="/admin/products" routerLinkActive="active">Товары</a>
      <a routerLink="/admin/orders" routerLinkActive="active">Заказы</a>
      <a routerLink="/admin/reviews" routerLinkActive="active">Отзывы</a>
      <a routerLink="/admin/promo" routerLinkActive="active">Промокоды</a>
      @if (auth.isAdmin()) {
        <a routerLink="/admin/users" routerLinkActive="active">Пользователи</a>
      }
    </nav>
  `,
})
export class AdminNavComponent {
  readonly auth = inject(AuthService);
}

/** Сводка по продажам, топ товаров и низкие остатки (FR-60 … FR-63). */
@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, AdminNavComponent, MoneyComponent],
  template: `
    <app-admin-nav />
    <h1>Сводка за последние 30 дней</h1>

    @if (summary(); as data) {
      <div class="stats">
        <div class="card">
          <div class="muted">Выручка</div>
          <div class="stat__value"><app-money [value]="data.revenue" /></div>
        </div>
        <div class="card">
          <div class="muted">Заказов</div>
          <div class="stat__value">{{ data.ordersCount }}</div>
        </div>
        <div class="card">
          <div class="muted">Средний чек</div>
          <div class="stat__value"><app-money [value]="data.averageCheck" /></div>
        </div>
        <div class="card">
          <div class="muted">Новых покупателей</div>
          <div class="stat__value">{{ data.newCustomers }}</div>
        </div>
      </div>

      <h2>Заказы по статусам</h2>
      <div class="card row">
        @for (entry of statusEntries(data); track entry.key) {
          <span class="status status--{{ entry.key }}">{{ label(entry.key) }}: {{ entry.value }}</span>
        }
        @if (statusEntries(data).length === 0) {
          <span class="muted">За период заказов не было.</span>
        }
      </div>
    }

    <div class="two-columns">
      <div class="card table-wrapper">
        <h3>Топ продаваемых товаров</h3>
        <table>
          <thead>
            <tr><th>Товар</th><th>Артикул</th><th>Шт.</th><th>Выручка</th></tr>
          </thead>
          <tbody>
            @for (item of topProducts(); track item.sku) {
              <tr>
                <td>{{ item.productName }}</td>
                <td class="muted">{{ item.sku }}</td>
                <td>{{ item.quantity }}</td>
                <td><app-money [value]="item.revenue" /></td>
              </tr>
            }
            @if (topProducts().length === 0) {
              <tr><td colspan="4" class="muted">Нет данных за период.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <div class="card table-wrapper">
        <h3>Заканчивается на складе</h3>
        <table>
          <thead>
            <tr><th>Товар</th><th>Артикул</th><th>Остаток</th></tr>
          </thead>
          <tbody>
            @for (item of lowStock(); track item.id) {
              <tr>
                <td><a routerLink="/admin/products" [queryParams]="{ search: item.sku }">{{ item.name }}</a></td>
                <td class="muted">{{ item.sku }}</td>
                <td [class.danger]="item.stockQuantity === 0">{{ item.stockQuantity }}</td>
              </tr>
            }
            @if (lowStock().length === 0) {
              <tr><td colspan="3" class="muted">Все товары в достаточном количестве.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (salesByDay().length) {
      <h2>Динамика выручки по дням</h2>
      <div class="card chart">
        @for (day of salesByDay(); track day.date) {
          <div class="bar" [title]="day.date + ': ' + day.revenue + ' руб.'">
            <div class="bar__fill" [style.height.%]="barHeight(day)"></div>
            <small class="muted">{{ day.date | date: 'dd.MM' }}</small>
          </div>
        }
      </div>
    }
  `,
  styles: [
    `
      .two-columns {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 20px;
        margin-top: 24px;
        align-items: start;
      }

      .chart {
        display: flex;
        align-items: flex-end;
        gap: 6px;
        height: 200px;
        overflow-x: auto;
      }

      .bar {
        display: flex;
        flex-direction: column;
        justify-content: flex-end;
        align-items: center;
        gap: 6px;
        height: 100%;
        min-width: 34px;
      }

      .bar__fill {
        width: 100%;
        background: var(--primary);
        border-radius: 4px 4px 0 0;
        min-height: 2px;
      }

      .danger {
        color: var(--danger);
        font-weight: 600;
      }

      @media (max-width: 900px) {
        .two-columns {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class AdminDashboardPage implements OnInit {
  private readonly admin = inject(AdminService);

  readonly summary = signal<SalesSummary | null>(null);
  readonly topProducts = signal<TopProduct[]>([]);
  readonly lowStock = signal<LowStock[]>([]);
  readonly salesByDay = signal<SalesByDay[]>([]);

  ngOnInit(): void {
    this.admin.summary().subscribe((data) => this.summary.set(data));
    this.admin.topProducts().subscribe((data) => this.topProducts.set(data));
    this.admin.lowStock().subscribe((data) => this.lowStock.set(data));
    this.admin.salesByDay().subscribe((data) => this.salesByDay.set(data));
  }

  statusEntries(summary: SalesSummary): { key: string; value: number }[] {
    return Object.entries(summary.ordersByStatus).map(([key, value]) => ({ key, value }));
  }

  label(status: string): string {
    return ORDER_STATUS_LABELS[status as keyof typeof ORDER_STATUS_LABELS] ?? status;
  }

  barHeight(day: SalesByDay): number {
    const max = Math.max(...this.salesByDay().map((d) => d.revenue), 1);
    return Math.max(2, (day.revenue / max) * 100);
  }
}
