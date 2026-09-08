import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { CartService } from '../core/cart.service';
import { ToastService } from '../core/toast.service';
import { MoneyComponent } from '../shared/ui';

/** Корзина: изменение количества, удаление, переход к оформлению (FR-22 … FR-24). */
@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MoneyComponent],
  template: `
    <h1>Корзина</h1>

    @if (cart.cart().items.length === 0) {
      <div class="card empty">
        Корзина пуста. <a routerLink="/catalog">Перейти в каталог</a>
      </div>
    } @else {
      <div class="layout-cart">
        <div class="card table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Товар</th>
                <th>Цена</th>
                <th>Количество</th>
                <th>Сумма</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (item of cart.cart().items; track item.productId) {
                <tr [class.issue]="item.hasStockIssue">
                  <td>
                    <a [routerLink]="['/product', item.slug]">{{ item.name }}</a>
                    @if (item.hasStockIssue) {
                      <div class="muted">Доступно только {{ item.stockQuantity }} шт.</div>
                    }
                  </td>
                  <td><app-money [value]="item.unitPrice" /></td>
                  <td>
                    <input
                      type="number"
                      min="1"
                      [max]="item.stockQuantity"
                      [ngModel]="item.quantity"
                      (ngModelChange)="changeQuantity(item.productId, $event)"
                      style="width:84px"
                    />
                  </td>
                  <td><app-money [value]="item.lineTotal" /></td>
                  <td class="text-right">
                    <button type="button" class="btn btn--sm btn--danger" (click)="remove(item.productId)">
                      Удалить
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <aside class="card summary">
          <h3>Итого</h3>
          <div class="spread">
            <span class="muted">Товаров</span>
            <span>{{ cart.cart().itemsCount }} шт.</span>
          </div>
          <div class="spread total">
            <span>Сумма</span>
            <strong><app-money [value]="cart.cart().itemsTotal" /></strong>
          </div>

          @if (cart.cart().hasIssues) {
            <p class="muted">Уменьшите количество товаров с недостаточным остатком, чтобы оформить заказ.</p>
          }

          @if (auth.isAuthenticated()) {
            <a routerLink="/checkout" class="btn btn--primary" [class.disabled]="cart.cart().hasIssues">
              Оформить заказ
            </a>
          } @else {
            <a routerLink="/login" [queryParams]="{ returnUrl: '/checkout' }" class="btn btn--primary">
              Войти и оформить
            </a>
          }

          <button type="button" class="btn" (click)="clear()">Очистить корзину</button>
        </aside>
      </div>
    }
  `,
  styles: [
    `
      .layout-cart {
        display: grid;
        grid-template-columns: minmax(0, 1fr) 300px;
        gap: 20px;
        align-items: start;
      }

      .summary {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .total {
        font-size: 18px;
      }

      .issue {
        background: #fff8f8;
      }

      .disabled {
        pointer-events: none;
        opacity: 0.5;
      }

      @media (max-width: 900px) {
        .layout-cart {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class CartPage implements OnInit {
  readonly cart = inject(CartService);
  readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    this.cart.load().subscribe({ error: () => undefined });
  }

  changeQuantity(productId: number, quantity: number): void {
    if (!quantity || quantity < 1) return;
    this.cart.update(productId, quantity).subscribe({ error: () => this.cart.load().subscribe() });
  }

  remove(productId: number): void {
    this.cart.remove(productId).subscribe(() => this.toast.info('Товар удалён из корзины'));
  }

  clear(): void {
    this.cart.clear().subscribe(() => this.toast.info('Корзина очищена'));
  }
}
