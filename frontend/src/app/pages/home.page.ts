import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartService } from '../core/cart.service';
import { Category, Featured, ProductListItem } from '../core/models';
import { ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';
import { ProductCardComponent } from '../shared/ui';

/** Главная страница: категории и подборки товаров. */
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, ProductCardComponent],
  template: `
    <section class="hero card">
      <div>
        <h1>Периферия и комплектующие для работы и игр</h1>
        <p class="muted">
          Клавиатуры, мыши, гарнитуры, мониторы и накопители в наличии. Самовывоз в день заказа,
          курьер бесплатно от 5 000 ₽.
        </p>
        <a routerLink="/catalog" class="btn btn--primary">Перейти в каталог</a>
      </div>
    </section>

    <h2>Категории</h2>
    <div class="categories">
      @for (category of categories(); track category.id) {
        <a class="card category" [routerLink]="['/catalog']" [queryParams]="{ categorySlug: category.slug }">
          <strong>{{ category.name }}</strong>
          <small class="muted">{{ category.productCount }} товаров</small>
        </a>
      }
    </div>

    @if (loading()) {
      <p class="empty">Загрузка каталога…</p>
    } @else if (featured(); as data) {
      <h2>Хиты продаж</h2>
      <div class="grid">
        @for (product of data.bestsellers; track product.id) {
          <app-product-card [product]="product" (addToCart)="addToCart($event)" />
        }
      </div>

      @if (data.discounted.length) {
        <h2>Со скидкой</h2>
        <div class="grid">
          @for (product of data.discounted; track product.id) {
            <app-product-card [product]="product" (addToCart)="addToCart($event)" />
          }
        </div>
      }

      <h2>Новинки</h2>
      <div class="grid">
        @for (product of data.newest; track product.id) {
          <app-product-card [product]="product" (addToCart)="addToCart($event)" />
        }
      </div>
    }
  `,
  styles: [
    `
      .hero {
        padding: 36px;
        margin-bottom: 28px;
        background: linear-gradient(120deg, #eef2ff, #ffffff 65%);
      }

      .hero h1 {
        max-width: 640px;
      }

      .hero p {
        max-width: 560px;
        margin-bottom: 20px;
      }

      .categories {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        gap: 12px;
        margin-bottom: 8px;
      }

      .category {
        display: flex;
        flex-direction: column;
        gap: 4px;
        color: var(--text);
        text-decoration: none;
      }

      .category:hover {
        border-color: var(--primary);
      }
    `,
  ],
})
export class HomePage implements OnInit {
  private readonly shop = inject(ShopService);
  private readonly cartService = inject(CartService);
  private readonly toast = inject(ToastService);

  readonly categories = signal<Category[]>([]);
  readonly featured = signal<Featured | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.shop.categories().subscribe((categories) => this.categories.set(categories.slice(0, 8)));
    this.shop.featured().subscribe({
      next: (featured) => {
        this.featured.set(featured);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  addToCart(productId: number): void {
    this.cartService.add(productId).subscribe(() => this.toast.success('Товар добавлен в корзину'));
  }

  protected trackProduct(_: number, product: ProductListItem): number {
    return product.id;
  }
}
