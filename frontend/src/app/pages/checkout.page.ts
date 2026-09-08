import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { CartService } from '../core/cart.service';
import { CreateOrderRequest, DeliveryMethod, PaymentMethod } from '../core/models';
import { ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';
import { MoneyComponent } from '../shared/ui';

/** Оформление заказа: доставка, оплата, промокод (FR-31 … FR-33). */
@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MoneyComponent],
  template: `
    <h1>Оформление заказа</h1>

    @if (cart.cart().items.length === 0) {
      <div class="card empty">Корзина пуста. <a routerLink="/catalog">Выбрать товары</a></div>
    } @else {
      <div class="layout-checkout">
        <form class="card stack" (ngSubmit)="submit()">
          <h3>Получатель</h3>
          <label>
            <span>ФИО</span>
            <input [(ngModel)]="form.recipientName" name="recipientName" required maxlength="200" />
          </label>
          <label>
            <span>Телефон</span>
            <input [(ngModel)]="form.recipientPhone" name="recipientPhone" required maxlength="32" />
          </label>

          <h3>Доставка</h3>
          <label>
            <span>Способ доставки</span>
            <select [(ngModel)]="form.deliveryMethod" name="deliveryMethod">
              <option value="Pickup">Самовывоз — бесплатно</option>
              <option value="Courier">Курьер — 350 ₽, бесплатно от 5 000 ₽</option>
              <option value="PostMachine">Постамат — 200 ₽</option>
            </select>
          </label>

          @if (form.deliveryMethod !== 'Pickup') {
            <label>
              <span>Адрес доставки</span>
              <input [(ngModel)]="form.deliveryAddress" name="deliveryAddress" required maxlength="500" />
            </label>
          }

          <h3>Оплата</h3>
          <label>
            <span>Способ оплаты</span>
            <select [(ngModel)]="form.paymentMethod" name="paymentMethod">
              <option value="CashOnDelivery">При получении</option>
              <option value="CardOnline">Картой онлайн</option>
            </select>
          </label>

          <label>
            <span>Комментарий к заказу</span>
            <textarea rows="2" [(ngModel)]="form.comment" name="comment" maxlength="1000"></textarea>
          </label>

          <h3>Промокод</h3>
          <div class="row">
            <input [(ngModel)]="promoCode" name="promoCode" placeholder="WELCOME10" style="flex:1" />
            <button type="button" class="btn" (click)="checkPromo()">Проверить</button>
          </div>
          @if (promoMessage()) {
            <p [class]="discount() > 0 ? 'in-stock' : 'muted'">{{ promoMessage() }}</p>
          }

          <button type="submit" class="btn btn--primary" [disabled]="sending()">
            {{ sending() ? 'Оформляем…' : 'Подтвердить заказ' }}
          </button>
        </form>

        <aside class="card stack">
          <h3>Ваш заказ</h3>
          @for (item of cart.cart().items; track item.productId) {
            <div class="spread">
              <span>{{ item.name }} × {{ item.quantity }}</span>
              <app-money [value]="item.lineTotal" />
            </div>
          }

          <hr />
          <div class="spread">
            <span class="muted">Товары</span>
            <app-money [value]="cart.cart().itemsTotal" />
          </div>
          <div class="spread">
            <span class="muted">Скидка</span>
            <span>−<app-money [value]="discount()" /></span>
          </div>
          <div class="spread">
            <span class="muted">Доставка</span>
            <app-money [value]="deliveryCost()" />
          </div>
          <div class="spread" style="font-size:18px">
            <strong>Итого</strong>
            <strong><app-money [value]="total()" /></strong>
          </div>
        </aside>
      </div>
    }
  `,
  styles: [
    `
      .layout-checkout {
        display: grid;
        grid-template-columns: minmax(0, 1fr) 320px;
        gap: 20px;
        align-items: start;
      }

      .in-stock {
        color: var(--success);
      }

      hr {
        border: none;
        border-top: 1px solid var(--border);
        margin: 4px 0;
      }

      @media (max-width: 900px) {
        .layout-checkout {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class CheckoutPage implements OnInit {
  readonly cart = inject(CartService);
  private readonly shop = inject(ShopService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly sending = signal(false);
  readonly discount = signal(0);
  readonly promoMessage = signal('');

  promoCode = '';
  form: CreateOrderRequest = {
    deliveryMethod: 'Pickup' as DeliveryMethod,
    deliveryAddress: '',
    recipientName: '',
    recipientPhone: '',
    paymentMethod: 'CashOnDelivery' as PaymentMethod,
    comment: '',
    promoCode: null,
  };

  /** Тарифы совпадают с серверными (FR-32); сервер остаётся источником истины. */
  readonly deliveryCost = computed(() => {
    const itemsTotal = this.cart.cart().itemsTotal;
    switch (this.form.deliveryMethod) {
      case 'Courier':
        return itemsTotal >= 5000 ? 0 : 350;
      case 'PostMachine':
        return 200;
      default:
        return 0;
    }
  });

  readonly total = computed(() =>
    Math.max(0, this.cart.cart().itemsTotal - this.discount()) + this.deliveryCost(),
  );

  ngOnInit(): void {
    this.cart.load().subscribe({ error: () => undefined });

    const user = this.auth.user();
    if (user) {
      this.form.recipientName = user.fullName;
      this.form.recipientPhone = user.phone ?? '';
      this.form.deliveryAddress = user.defaultAddress ?? '';
    }
  }

  checkPromo(): void {
    const code = this.promoCode.trim();
    if (!code) return;

    this.shop.validatePromo(code, this.cart.cart().itemsTotal).subscribe((result) => {
      this.discount.set(result.valid ? result.discount : 0);
      this.promoMessage.set(result.message);
    });
  }

  submit(): void {
    if (!this.form.recipientName.trim() || !this.form.recipientPhone.trim()) {
      this.toast.error('Заполните ФИО и телефон получателя');
      return;
    }

    if (this.form.deliveryMethod !== 'Pickup' && !this.form.deliveryAddress?.trim()) {
      this.toast.error('Укажите адрес доставки');
      return;
    }

    this.sending.set(true);
    const request: CreateOrderRequest = {
      ...this.form,
      promoCode: this.discount() > 0 ? this.promoCode.trim() : null,
    };

    this.shop.createOrder(request).subscribe({
      next: (order) => {
        this.sending.set(false);
        this.toast.success(`Заказ ${order.number} оформлен`);
        this.cart.load().subscribe({ error: () => undefined });
        void this.router.navigate(['/account/orders', order.id]);
      },
      error: () => this.sending.set(false),
    });
  }
}
