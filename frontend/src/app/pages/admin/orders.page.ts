import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin.service';
import {
  DELIVERY_LABELS, ORDER_STATUS_LABELS, OrderDetails, OrderListItem, OrderStatus, Paged,
} from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { MoneyComponent, PaginationComponent } from '../../shared/ui';
import { AdminNavComponent } from './dashboard.page';

/** Очередь заказов: фильтры, смена статуса, выгрузка CSV (FR-36, FR-39, FR-64). */
@Component({
  selector: 'app-admin-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminNavComponent, MoneyComponent, PaginationComponent],
  template: `
    <app-admin-nav />
    <h1>Заказы</h1>

    <div class="card row" style="margin-bottom:16px">
      <input placeholder="Номер, покупатель или телефон" [(ngModel)]="search" (keyup.enter)="load(1)" style="flex:1" />
      <select [(ngModel)]="status" (ngModelChange)="load(1)" style="width:200px">
        <option [ngValue]="undefined">Все статусы</option>
        @for (option of statuses; track option) {
          <option [ngValue]="option">{{ label(option) }}</option>
        }
      </select>
      <button type="button" class="btn" (click)="load(1)">Найти</button>
      <button type="button" class="btn" (click)="exportCsv()">Выгрузить CSV</button>
    </div>

    @if (page(); as data) {
      <div class="card table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Номер</th><th>Дата</th><th>Статус</th><th>Доставка</th>
              <th>Позиции</th><th>Сумма</th><th></th>
            </tr>
          </thead>
          <tbody>
            @for (order of data.items; track order.id) {
              <tr>
                <td>{{ order.number }}</td>
                <td>{{ order.createdAt | date: 'dd.MM.yyyy HH:mm' }}</td>
                <td><span class="status status--{{ order.status }}">{{ label(order.status) }}</span></td>
                <td>{{ deliveryLabel(order) }}</td>
                <td>{{ order.itemsCount }}</td>
                <td><app-money [value]="order.total" /></td>
                <td class="text-right">
                  <button type="button" class="btn btn--sm" (click)="open(order.id)">Открыть</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <app-pagination [page]="data.page" [totalPages]="data.totalPages" (pageChange)="load($event)" />
    }

    @if (selected(); as order) {
      <div class="card stack" style="margin-top:20px">
        <div class="spread">
          <h2>Заказ {{ order.number }}</h2>
          <button type="button" class="btn btn--sm" (click)="selected.set(null)">Закрыть</button>
        </div>

        <div class="two-columns">
          <div>
            <h3>Покупатель</h3>
            <p>{{ order.customerName }} · {{ order.customerEmail }}</p>
            <p>{{ order.recipientName }}, {{ order.recipientPhone }}</p>
            <p class="muted">{{ deliveryLabel(order) }}{{ order.deliveryAddress ? ', ' + order.deliveryAddress : '' }}</p>
            @if (order.comment) { <p class="muted">Комментарий: {{ order.comment }}</p> }
          </div>

          <div>
            <h3>Смена статуса</h3>
            @if (order.allowedNextStatuses.length === 0) {
              <p class="muted">Заказ в конечном статусе.</p>
            } @else {
              <div class="row">
                <select [(ngModel)]="nextStatus" style="width:200px">
                  @for (option of order.allowedNextStatuses; track option) {
                    <option [ngValue]="option">{{ label(option) }}</option>
                  }
                </select>
                <input [(ngModel)]="statusComment" placeholder="Комментарий" style="flex:1" />
                <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="changeStatus(order)">
                  Применить
                </button>
              </div>
            }
          </div>
        </div>

        <div class="table-wrapper">
          <table>
            <thead>
              <tr><th>Товар</th><th>Артикул</th><th>Цена</th><th>Кол-во</th><th>Сумма</th></tr>
            </thead>
            <tbody>
              @for (item of order.items; track item.sku) {
                <tr>
                  <td>{{ item.productName }}</td>
                  <td class="muted">{{ item.sku }}</td>
                  <td><app-money [value]="item.unitPrice" /></td>
                  <td>{{ item.quantity }}</td>
                  <td><app-money [value]="item.lineTotal" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="spread" style="font-size:17px">
          <span class="muted">Товары {{ order.itemsTotal }} − скидка {{ order.discountTotal }} + доставка {{ order.deliveryCost }}</span>
          <strong>Итого: <app-money [value]="order.total" /></strong>
        </div>

        <h3>История статусов</h3>
        @for (entry of order.statusHistory; track entry.createdAt) {
          <div class="muted">
            {{ entry.createdAt | date: 'dd.MM.yyyy HH:mm' }} —
            {{ entry.fromStatus ? label(entry.fromStatus) + ' → ' : '' }}{{ label(entry.toStatus) }}
            {{ entry.changedBy ? '(' + entry.changedBy + ')' : '' }}
            {{ entry.comment ? '· ' + entry.comment : '' }}
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
      }

      @media (max-width: 900px) {
        .two-columns {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class AdminOrdersPage implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly toast = inject(ToastService);

  readonly statuses: OrderStatus[] = [
    'New', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Completed', 'Cancelled', 'Refunded',
  ];

  readonly page = signal<Paged<OrderListItem> | null>(null);
  readonly selected = signal<OrderDetails | null>(null);
  readonly busy = signal(false);

  search = '';
  status: OrderStatus | undefined;
  nextStatus: OrderStatus = 'Processing';
  statusComment = '';

  ngOnInit(): void {
    this.load(1);
  }

  load(page: number): void {
    this.admin
      .orders({ search: this.search || undefined, status: this.status, page, pageSize: 20 })
      .subscribe((result) => this.page.set(result));
  }

  open(id: number): void {
    this.admin.order(id).subscribe((order) => {
      this.selected.set(order);
      this.nextStatus = order.allowedNextStatuses[0] ?? 'Processing';
      this.statusComment = '';
    });
  }

  changeStatus(order: OrderDetails): void {
    this.busy.set(true);
    this.admin.changeOrderStatus(order.id, this.nextStatus, this.statusComment).subscribe({
      next: (updated) => {
        this.selected.set(updated);
        this.nextStatus = updated.allowedNextStatuses[0] ?? 'Processing';
        this.busy.set(false);
        this.toast.success(`Статус заказа ${updated.number}: ${this.label(updated.status)}`);
        this.load(this.page()?.page ?? 1);
      },
      error: () => this.busy.set(false),
    });
  }

  exportCsv(): void {
    this.admin.exportOrders().subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `orders-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  label(status: string): string {
    return ORDER_STATUS_LABELS[status as keyof typeof ORDER_STATUS_LABELS] ?? status;
  }

  deliveryLabel(order: { deliveryMethod: keyof typeof DELIVERY_LABELS }): string {
    return DELIVERY_LABELS[order.deliveryMethod] ?? order.deliveryMethod;
  }
}
