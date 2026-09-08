import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductListItem } from '../core/models';
import { ToastService } from '../core/toast.service';

/** Форматирование денежных сумм в рублях (NFR-10). */
@Component({
  selector: 'app-money',
  standalone: true,
  template: '{{ formatted }}',
})
export class MoneyComponent {
  @Input({ required: true }) value = 0;

  get formatted(): string {
    return new Intl.NumberFormat('ru-RU', {
      style: 'currency',
      currency: 'RUB',
      maximumFractionDigits: 0,
    }).format(this.value);
  }
}

/** Звёздный рейтинг товара. */
@Component({
  selector: 'app-rating',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="rating" [title]="value + ' из 5'">
      @for (star of stars; track $index) {
        <span [class.filled]="star">★</span>
      }
      @if (count !== null) {
        <small class="muted">({{ count }})</small>
      }
    </span>
  `,
})
export class RatingComponent {
  @Input() value = 0;
  @Input() count: number | null = null;

  get stars(): boolean[] {
    return [1, 2, 3, 4, 5].map((i) => i <= Math.round(this.value));
  }
}

/** Карточка товара в списках каталога. */
@Component({
  selector: 'app-product-card',
  standalone: true,
  imports: [CommonModule, RouterLink, MoneyComponent, RatingComponent],
  template: `
    <article class="card product-card">
      <a [routerLink]="['/product', product().slug]" class="product-card__image">
        @if (product().primaryImageUrl) {
          <img [src]="product().primaryImageUrl" [alt]="product().name" loading="lazy" />
        } @else {
          <div class="placeholder">{{ product().name.charAt(0) }}</div>
        }
        @if (product().oldPrice) {
          <span class="badge badge--sale">-{{ discountPercent() }}%</span>
        }
        @if (!product().inStock) {
          <span class="badge badge--out">Нет в наличии</span>
        }
      </a>

      <div class="product-card__body">
        <small class="muted">{{ product().brandName }}</small>
        <a [routerLink]="['/product', product().slug]" class="product-card__name">{{ product().name }}</a>
        <app-rating [value]="product().ratingAvg" [count]="product().ratingCount" />

        <div class="product-card__price">
          <strong><app-money [value]="product().price" /></strong>
          @if (product().oldPrice) {
            <s class="muted"><app-money [value]="product().oldPrice!" /></s>
          }
        </div>

        <button
          type="button"
          class="btn btn--primary"
          [disabled]="!product().inStock || busy()"
          (click)="addToCart.emit(product().id)"
        >
          {{ product().inStock ? 'В корзину' : 'Нет в наличии' }}
        </button>
      </div>
    </article>
  `,
})
export class ProductCardComponent {
  readonly product = input.required<ProductListItem>();
  readonly busy = input(false);
  readonly addToCart = output<number>();

  discountPercent(): number {
    const item = this.product();
    if (!item.oldPrice || item.oldPrice <= item.price) return 0;
    return Math.round(((item.oldPrice - item.price) / item.oldPrice) * 100);
  }
}

/** Постраничная навигация для списков (NFR-02). */
@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (totalPages > 1) {
      <nav class="pagination">
        <button type="button" class="btn" [disabled]="page <= 1" (click)="pageChange.emit(page - 1)">←</button>
        <span>Страница {{ page }} из {{ totalPages }}</span>
        <button type="button" class="btn" [disabled]="page >= totalPages" (click)="pageChange.emit(page + 1)">→</button>
      </nav>
    }
  `,
})
export class PaginationComponent {
  @Input() page = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();
}

/** Контейнер всплывающих уведомлений. */
@Component({
  selector: 'app-toasts',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toasts">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class]="'toast--' + toast.type" (click)="toasts.dismiss(toast.id)">
          {{ toast.text }}
        </div>
      }
    </div>
  `,
})
export class ToastsComponent {
  readonly toasts = inject(ToastService);
}
