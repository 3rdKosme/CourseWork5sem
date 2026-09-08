import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { CartService } from '../core/cart.service';
import { ProductAttribute, ProductDetails, ProductListItem, Review } from '../core/models';
import { ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';
import { MoneyComponent, ProductCardComponent, RatingComponent } from '../shared/ui';

/** Карточка товара: галерея, характеристики, отзывы, похожие товары (FR-02, FR-07, FR-50). */
@Component({
  selector: 'app-product',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MoneyComponent, RatingComponent, ProductCardComponent],
  template: `
    @if (product(); as item) {
      <nav class="muted" style="margin-bottom:16px">
        <a routerLink="/catalog">Каталог</a> /
        <a [routerLink]="['/catalog']" [queryParams]="{ categorySlug: item.categorySlug }">{{ item.categoryName }}</a> /
        {{ item.name }}
      </nav>

      <div class="product">
        <div class="card product__gallery">
          @if (activeImage()) {
            <img [src]="activeImage()" [alt]="item.name" />
          } @else {
            <div class="empty">{{ item.name }}</div>
          }

          @if (item.images.length > 1) {
            <div class="thumbs">
              @for (image of item.images; track image.url) {
                <img [src]="image.url" [alt]="image.alt ?? item.name" (click)="activeImage.set(image.url)" />
              }
            </div>
          }
        </div>

        <div class="card product__info">
          <small class="muted">{{ item.brandName }} · артикул {{ item.sku }}</small>
          <h1>{{ item.name }}</h1>
          <app-rating [value]="item.ratingAvg" [count]="item.ratingCount" />

          <div class="price-row">
            <strong class="price"><app-money [value]="item.price" /></strong>
            @if (item.oldPrice) {
              <s class="muted"><app-money [value]="item.oldPrice" /></s>
            }
          </div>

          <p [class]="item.inStock ? 'in-stock' : 'muted'">
            {{ item.inStock ? 'В наличии: ' + item.stockQuantity + ' шт.' : 'Нет в наличии' }}
          </p>

          <div class="row">
            <input
              type="number"
              min="1"
              [max]="item.stockQuantity"
              [(ngModel)]="quantity"
              style="width:88px"
            />
            <button type="button" class="btn btn--primary" [disabled]="!item.inStock" (click)="addToCart(item)">
              Добавить в корзину
            </button>
            @if (auth.isAuthenticated()) {
              <button type="button" class="btn" (click)="addFavorite(item.id)">В избранное</button>
            }
          </div>

          <p class="muted">Гарантия: {{ item.warrantyMonths }} мес.
            @if (item.weightGrams) { · Вес: {{ item.weightGrams }} г }
          </p>
        </div>
      </div>

      @if (item.description) {
        <h2>Описание</h2>
        <div class="card">{{ item.description }}</div>
      }

      @if (item.attributes.length) {
        <h2>Характеристики</h2>
        <div class="card">
          @for (group of attributeGroups(item.attributes); track group.name) {
            <h3>{{ group.name }}</h3>
            <table>
              <tbody>
                @for (attribute of group.items; track attribute.name) {
                  <tr>
                    <td class="muted" style="width:40%">{{ attribute.name }}</td>
                    <td>{{ attribute.value }} {{ attribute.unit }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      }

      <h2>Отзывы</h2>
      <div class="card">
        @if (reviews().length === 0) {
          <p class="muted">Отзывов пока нет.</p>
        } @else {
          @for (review of reviews(); track review.id) {
            <article class="review">
              <div class="spread">
                <strong>{{ review.authorName }}</strong>
                <app-rating [value]="review.rating" />
              </div>
              @if (review.title) { <h3>{{ review.title }}</h3> }
              <p>{{ review.body }}</p>
              <small class="muted">{{ review.createdAt | date: 'dd.MM.yyyy' }}</small>
            </article>
          }
        }

        @if (auth.isAuthenticated()) {
          <h3>Оставить отзыв</h3>
          <p class="muted">Отзыв можно оставить на полученный товар; публикуется после модерации.</p>
          <div class="row">
            <label style="width:120px">
              <span>Оценка</span>
              <select [(ngModel)]="newRating">
                @for (value of [5, 4, 3, 2, 1]; track value) {
                  <option [ngValue]="value">{{ value }}</option>
                }
              </select>
            </label>
            <label style="flex:1">
              <span>Заголовок</span>
              <input [(ngModel)]="newTitle" maxlength="200" />
            </label>
          </div>
          <label>
            <span>Текст отзыва</span>
            <textarea rows="3" [(ngModel)]="newBody" maxlength="2000"></textarea>
          </label>
          <button type="button" class="btn btn--primary" [disabled]="sending()" (click)="submitReview(item.id)">
            Отправить
          </button>
        } @else {
          <p class="muted"><a routerLink="/login">Войдите</a>, чтобы оставить отзыв.</p>
        }
      </div>

      @if (similar().length) {
        <h2>Похожие товары</h2>
        <div class="grid">
          @for (other of similar(); track other.id) {
            <app-product-card [product]="other" (addToCart)="quickAdd($event)" />
          }
        </div>
      }
    } @else if (!loading()) {
      <p class="empty">Товар не найден.</p>
    }
  `,
  styles: [
    `
      .product {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
        gap: 20px;
      }

      .product__gallery img {
        width: 100%;
        aspect-ratio: 4 / 3;
        object-fit: contain;
        background: var(--surface-2);
        border-radius: 10px;
      }

      .thumbs {
        display: flex;
        gap: 8px;
        margin-top: 10px;
      }

      .thumbs img {
        width: 72px;
        height: 72px;
        cursor: pointer;
        aspect-ratio: 1;
      }

      .price {
        font-size: 28px;
      }

      .price-row {
        display: flex;
        align-items: baseline;
        gap: 12px;
        margin: 12px 0;
      }

      .in-stock {
        color: var(--success);
      }

      .review {
        padding: 12px 0;
        border-bottom: 1px solid var(--border);
      }

      @media (max-width: 900px) {
        .product {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class ProductPage implements OnInit {
  private readonly shop = inject(ShopService);
  private readonly cart = inject(CartService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  readonly auth = inject(AuthService);

  readonly product = signal<ProductDetails | null>(null);
  readonly similar = signal<ProductListItem[]>([]);
  readonly reviews = signal<Review[]>([]);
  readonly activeImage = signal<string | null>(null);
  readonly loading = signal(true);
  readonly sending = signal(false);

  quantity = 1;
  newRating = 5;
  newTitle = '';
  newBody = '';

  ngOnInit(): void {
    this.route.params.subscribe((params) => this.load(params['slug']));
  }

  attributeGroups(attributes: ProductAttribute[]): { name: string; items: ProductAttribute[] }[] {
    const groups = new Map<string, ProductAttribute[]>();

    for (const attribute of attributes) {
      const key = attribute.groupName ?? 'Основные';
      groups.set(key, [...(groups.get(key) ?? []), attribute]);
    }

    return [...groups.entries()].map(([name, items]) => ({ name, items }));
  }

  addToCart(product: ProductDetails): void {
    this.cart.add(product.id, this.quantity).subscribe(() => this.toast.success('Товар добавлен в корзину'));
  }

  quickAdd(productId: number): void {
    this.cart.add(productId).subscribe(() => this.toast.success('Товар добавлен в корзину'));
  }

  addFavorite(productId: number): void {
    this.shop.addFavorite(productId).subscribe(() => this.toast.success('Добавлено в избранное'));
  }

  submitReview(productId: number): void {
    if (this.newBody.trim().length < 10) {
      this.toast.error('Текст отзыва должен содержать не менее 10 символов');
      return;
    }

    this.sending.set(true);
    this.shop.createReview(productId, this.newRating, this.newTitle, this.newBody).subscribe({
      next: () => {
        this.toast.success('Отзыв отправлен на модерацию');
        this.newTitle = '';
        this.newBody = '';
        this.sending.set(false);
      },
      error: () => this.sending.set(false),
    });
  }

  private load(slug: string): void {
    this.loading.set(true);

    this.shop.product(slug).subscribe({
      next: (product) => {
        this.product.set(product);
        this.activeImage.set(product.images[0]?.url ?? null);
        this.quantity = 1;
        this.loading.set(false);

        this.shop.similar(product.id).subscribe((items) => this.similar.set(items));
        this.shop.reviews(product.id).subscribe((page) => this.reviews.set(page.items));
      },
      error: () => this.loading.set(false),
    });
  }
}
