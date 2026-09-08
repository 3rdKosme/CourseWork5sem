import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DELIVERY_LABELS, ORDER_STATUS_LABELS, OrderListItem, Paged } from '../core/models';
import { ShopService } from '../core/shop.service';
import { MoneyComponent, PaginationComponent } from '../shared/ui';

/** Список заказов покупателя. */
@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, RouterLink, MoneyComponent, PaginationComponent],
  template: `
    <h1>Мои заказы</h1>

    @if (loading()) {
      <p class="empty">Загрузка…</p>
    } @else if (page(); as data) {
      @if (data.items.length === 0) {
        <div class="card empty">Заказов пока нет. <a routerLink="/catalog">Перейти в каталог</a></div>
      } @else {
        <div class="card table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Номер</th>
                <th>Дата</th>
                <th>Статус</th>
                <th>Доставка</th>
                <th>Позиции</th>
                <th>Сумма</th>
              </tr>
            </thead>
            <tbody>
              @for (order of data.items; track order.id) {
                <tr>
                  <td><a [routerLink]="['/account/orders', order.id]">{{ order.number }}</a></td>
                  <td>{{ order.createdAt | date: 'dd.MM.yyyy HH:mm' }}</td>
                  <td><span class="status status--{{ order.status }}">{{ statusLabel(order) }}</span></td>
                  <td>{{ deliveryLabel(order) }}</td>
                  <td>{{ order.itemsCount }} шт.</td>
                  <td><app-money [value]="order.total" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <app-pagination [page]="data.page" [totalPages]="data.totalPages" (pageChange)="load($event)" />
      }
    }
  `,
})
export class OrdersPage implements OnInit {
  private readonly shop = inject(ShopService);

  readonly page = signal<Paged<OrderListItem> | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.load(1);
  }

  load(page: number): void {
    this.loading.set(true);
    this.shop.orders(page).subscribe({
      next: (result) => {
        this.page.set(result);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  statusLabel(order: OrderListItem): string {
    return ORDER_STATUS_LABELS[order.status] ?? order.status;
  }

  deliveryLabel(order: OrderListItem): string {
    return DELIVERY_LABELS[order.deliveryMethod] ?? order.deliveryMethod;
  }
}
