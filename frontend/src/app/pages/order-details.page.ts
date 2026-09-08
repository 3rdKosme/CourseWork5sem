import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  DELIVERY_LABELS, ORDER_STATUS_LABELS, OrderDetails, PAYMENT_LABELS,
} from '../core/models';
import { ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';
import { MoneyComponent } from '../shared/ui';

/** Детали заказа покупателя: состав, история статусов, отмена и оплата. */
@Component({
  selector: 'app-order-details',
  standalone: true,
  imports: [CommonModule, RouterLink, MoneyComponent],
  template: `
    @if (order(); as data) {
      <div class="spread">
        <h1>Заказ {{ data.number }}</h1>
        <span class="status status--{{ data.status }}">{{ label(data.status) }}</span>
      </div>

      <div class="layout-order">
        <div class="stack">
          <div class="card table-wrapper">
            <h3>Состав заказа</h3>
            <table>
              <thead>
                <tr>
                  <th>Товар</th>
                  <th>Артикул</th>
                  <th>Цена</th>
                  <th>Кол-во</th>
                  <th>Сумма</th>
                </tr>
              </thead>
              <tbody>
                @for (item of data.items; track item.sku) {
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

          <div class="card">
            <h3>История статусов</h3>
            @for (entry of data.statusHistory; track entry.createdAt) {
              <div class="history">
                <span class="muted">{{ entry.createdAt | date: 'dd.MM.yyyy HH:mm' }}</span>
                <span>
                  {{ entry.fromStatus ? label(entry.fromStatus) + ' → ' : '' }}{{ label(entry.toStatus) }}
                </span>
                <span class="muted">{{ entry.changedBy }} {{ entry.comment ? '· ' + entry.comment : '' }}</span>
              </div>
            }
          </div>
        </div>

        <aside class="card stack">
          <h3>Информация</h3>
          <div class="spread"><span class="muted">Оформлен</span><span>{{ data.createdAt | date: 'dd.MM.yyyy HH:mm' }}</span></div>
          <div class="spread"><span class="muted">Доставка</span><span>{{ deliveryLabel(data) }}</span></div>
          @if (data.deliveryAddress) {
            <div class="spread"><span class="muted">Адрес</span><span class="text-right">{{ data.deliveryAddress }}</span></div>
          }
          <div class="spread"><span class="muted">Получатель</span><span>{{ data.recipientName }}</span></div>
          <div class="spread"><span class="muted">Телефон</span><span>{{ data.recipientPhone }}</span></div>
          <div class="spread"><span class="muted">Оплата</span><span>{{ paymentLabel(data) }}</span></div>
          @if (data.promoCode) {
            <div class="spread"><span class="muted">Промокод</span><span>{{ data.promoCode }}</span></div>
          }

          <hr />
          <div class="spread"><span class="muted">Товары</span><app-money [value]="data.itemsTotal" /></div>
          <div class="spread"><span class="muted">Скидка</span><span>−<app-money [value]="data.discountTotal" /></span></div>
          <div class="spread"><span class="muted">Доставка</span><app-money [value]="data.deliveryCost" /></div>
          <div class="spread" style="font-size:18px"><strong>Итого</strong><strong><app-money [value]="data.total" /></strong></div>

          @if (canPay(data)) {
            <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="pay(data.id)">
              Оплатить картой
            </button>
          }
          @if (canCancel(data)) {
            <button type="button" class="btn btn--danger" [disabled]="busy()" (click)="cancel(data.id)">
              Отменить заказ
            </button>
          }

          <a routerLink="/account/orders" class="btn">К списку заказов</a>
        </aside>
      </div>
    } @else if (!loading()) {
      <p class="empty">Заказ не найден.</p>
    }
  `,
  styles: [
    `
      .layout-order {
        display: grid;
        grid-template-columns: minmax(0, 1fr) 320px;
        gap: 20px;
        align-items: start;
      }

      .history {
        display: grid;
        grid-template-columns: 150px 220px 1fr;
        gap: 10px;
        padding: 8px 0;
        border-bottom: 1px solid var(--border);
      }

      hr {
        border: none;
        border-top: 1px solid var(--border);
        margin: 4px 0;
      }

      @media (max-width: 900px) {
        .layout-order,
        .history {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class OrderDetailsPage implements OnInit {
  private readonly shop = inject(ShopService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly order = signal<OrderDetails | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.shop.order(id).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  label(status: string): string {
    return ORDER_STATUS_LABELS[status as keyof typeof ORDER_STATUS_LABELS] ?? status;
  }

  deliveryLabel(order: OrderDetails): string {
    return DELIVERY_LABELS[order.deliveryMethod] ?? order.deliveryMethod;
  }

  paymentLabel(order: OrderDetails): string {
    return `${PAYMENT_LABELS[order.paymentMethod] ?? order.paymentMethod} (${order.paymentStatus})`;
  }

  canPay(order: OrderDetails): boolean {
    return order.paymentMethod === 'CardOnline' && order.status === 'New';
  }

  canCancel(order: OrderDetails): boolean {
    return order.status === 'New' || order.status === 'Paid';
  }

  pay(id: number): void {
    this.busy.set(true);
    this.shop.payOrder(id).subscribe({
      next: (order) => {
        this.order.set(order);
        this.busy.set(false);
        this.toast.success('Заказ оплачен');
      },
      error: () => this.busy.set(false),
    });
  }

  cancel(id: number): void {
    const reason = prompt('Укажите причину отмены', 'Передумал') ?? 'Отменён покупателем';

    this.busy.set(true);
    this.shop.cancelOrder(id, reason).subscribe({
      next: (order) => {
        this.order.set(order);
        this.busy.set(false);
        this.toast.info('Заказ отменён, остатки возвращены на склад');
      },
      error: () => this.busy.set(false),
    });
  }
}
