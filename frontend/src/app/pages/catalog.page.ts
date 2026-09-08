import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CartService } from '../core/cart.service';
import { Brand, Category, ProductPage as ProductPageDto } from '../core/models';
import { CatalogQuery, ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';
import { PaginationComponent, ProductCardComponent } from '../shared/ui';

/** Каталог с фасетным фильтром; состояние фильтров синхронизировано с URL (раздел 8.2 ТЗ). */
@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [CommonModule, FormsModule, ProductCardComponent, PaginationComponent],
  template: `
    <h1>Каталог</h1>

    <div class="layout">
      <aside class="card filters">
        <h3>Фильтры</h3>

        <label>
          <span>Категория</span>
          <select [(ngModel)]="query.categorySlug" (ngModelChange)="apply()">
            <option [ngValue]="undefined">Все категории</option>
            @for (category of flatCategories(); track category.slug) {
              <option [ngValue]="category.slug">{{ category.prefix }}{{ category.name }}</option>
            }
          </select>
        </label>

        <label>
          <span>Сортировка</span>
          <select [(ngModel)]="query.sort" (ngModelChange)="apply()">
            <option value="New">Сначала новые</option>
            <option value="PriceAsc">Дешевле</option>
            <option value="PriceDesc">Дороже</option>
            <option value="Rating">По рейтингу</option>
            <option value="Popular">По популярности</option>
          </select>
        </label>

        <div class="row">
          <label style="flex:1">
            <span>Цена от</span>
            <input type="number" min="0" [(ngModel)]="query.minPrice" (change)="apply()" />
          </label>
          <label style="flex:1">
            <span>до</span>
            <input type="number" min="0" [(ngModel)]="query.maxPrice" (change)="apply()" />
          </label>
        </div>

        <label class="checkbox">
          <input type="checkbox" [(ngModel)]="query.inStock" (ngModelChange)="apply()" />
          Только в наличии
        </label>

        <label class="checkbox">
          <input type="checkbox" [(ngModel)]="query.onlyDiscounted" (ngModelChange)="apply()" />
          Только со скидкой
        </label>

        <h3>Бренды</h3>
        @for (brand of brandFacets(); track brand.id) {
          <label class="checkbox">
            <input
              type="checkbox"
              [checked]="selectedBrands.has(brand.id)"
              (change)="toggleBrand(brand.id)"
            />
            {{ brand.name }} <span class="muted">({{ brand.count }})</span>
          </label>
        }

        <button type="button" class="btn" (click)="reset()">Сбросить фильтры</button>
      </aside>

      <section>
        @if (loading()) {
          <p class="empty">Загрузка товаров…</p>
        } @else if (page(); as data) {
          <div class="spread" style="margin-bottom:16px">
            <span class="muted">Найдено товаров: {{ data.totalItems }}</span>
            @if (data.facets.maxPrice > 0) {
              <span class="muted">
                Цены: {{ data.facets.minPrice | number: '1.0-0' }} – {{ data.facets.maxPrice | number: '1.0-0' }} ₽
              </span>
            }
          </div>

          @if (data.items.length === 0) {
            <p class="empty">По заданным условиям ничего не найдено. Попробуйте смягчить фильтры.</p>
          } @else {
            <div class="grid">
              @for (product of data.items; track product.id) {
                <app-product-card [product]="product" (addToCart)="addToCart($event)" />
              }
            </div>
          }

          <app-pagination
            [page]="data.page"
            [totalPages]="data.totalPages"
            (pageChange)="goToPage($event)"
          />
        }
      </section>
    </div>
  `,
  styles: [
    `
      .checkbox {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 8px;
      }

      .checkbox input {
        width: auto;
      }
    `,
  ],
})
export class CatalogPage implements OnInit {
  private readonly shop = inject(ShopService);
  private readonly cart = inject(CartService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly page = signal<ProductPageDto | null>(null);
  readonly loading = signal(true);
  readonly categories = signal<Category[]>([]);
  readonly brands = signal<Brand[]>([]);

  query: CatalogQuery = { sort: 'New', page: 1, pageSize: 12 };
  selectedBrands = new Set<number>();

  ngOnInit(): void {
    this.shop.categories().subscribe((categories) => this.categories.set(categories));
    this.shop.brands().subscribe((brands) => this.brands.set(brands));

    // URL — источник истины для фильтров: ссылку можно переслать и открыть в том же состоянии.
    this.route.queryParams.subscribe((params) => {
      this.query = {
        search: params['search'] ?? undefined,
        categorySlug: params['categorySlug'] ?? undefined,
        minPrice: params['minPrice'] ? Number(params['minPrice']) : undefined,
        maxPrice: params['maxPrice'] ? Number(params['maxPrice']) : undefined,
        inStock: params['inStock'] === 'true' ? true : undefined,
        onlyDiscounted: params['onlyDiscounted'] === 'true' ? true : undefined,
        sort: params['sort'] ?? 'New',
        page: params['page'] ? Number(params['page']) : 1,
        pageSize: 12,
      };

      this.selectedBrands = new Set(
        ([] as string[]).concat(params['brandIds'] ?? []).map((id) => Number(id)),
      );

      this.load();
    });
  }

  flatCategories(): { slug: string; name: string; prefix: string }[] {
    const result: { slug: string; name: string; prefix: string }[] = [];

    const walk = (items: Category[], depth: number): void => {
      for (const item of items) {
        result.push({ slug: item.slug, name: item.name, prefix: '— '.repeat(depth) });
        walk(item.children, depth + 1);
      }
    };

    walk(this.categories(), 0);
    return result;
  }

  brandFacets(): { id: number; name: string; count: number }[] {
    const facets = this.page()?.facets.brands ?? [];
    if (facets.length > 0) return facets;
    return this.brands().map((brand) => ({ id: brand.id, name: brand.name, count: 0 }));
  }

  toggleBrand(id: number): void {
    if (this.selectedBrands.has(id)) this.selectedBrands.delete(id);
    else this.selectedBrands.add(id);
    this.apply();
  }

  apply(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: this.query.search || null,
        categorySlug: this.query.categorySlug || null,
        minPrice: this.query.minPrice || null,
        maxPrice: this.query.maxPrice || null,
        inStock: this.query.inStock ? 'true' : null,
        onlyDiscounted: this.query.onlyDiscounted ? 'true' : null,
        sort: this.query.sort ?? 'New',
        brandIds: this.selectedBrands.size ? [...this.selectedBrands] : null,
        page: 1,
      },
    });
  }

  reset(): void {
    this.selectedBrands.clear();
    this.query = { sort: 'New', page: 1, pageSize: 12 };
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  goToPage(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page },
      queryParamsHandling: 'merge',
    });
  }

  addToCart(productId: number): void {
    this.cart.add(productId).subscribe(() => this.toast.success('Товар добавлен в корзину'));
  }

  private load(): void {
    this.loading.set(true);
    const request: CatalogQuery = { ...this.query, brandIds: [...this.selectedBrands] };

    this.shop.products(request).subscribe({
      next: (page) => {
        this.page.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
