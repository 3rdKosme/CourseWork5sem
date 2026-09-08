import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin.service';
import { AdminProduct, Brand, Category, Paged, ProductInput } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { MoneyComponent, PaginationComponent } from '../../shared/ui';
import { AdminNavComponent } from './dashboard.page';

/** Управление номенклатурой и остатками (раздел 9.1 API, FR-45 … FR-47). */
@Component({
  selector: 'app-admin-products',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminNavComponent, MoneyComponent, PaginationComponent],
  template: `
    <app-admin-nav />

    <div class="spread">
      <h1>Товары</h1>
      <button type="button" class="btn btn--primary" (click)="startCreate()">Добавить товар</button>
    </div>

    <div class="card row" style="margin-bottom:16px">
      <input placeholder="Поиск по названию или артикулу" [(ngModel)]="search" (keyup.enter)="load(1)" style="flex:1" />
      <label class="inline">
        <input type="checkbox" [(ngModel)]="lowStockOnly" (ngModelChange)="load(1)" />
        Только заканчивающиеся
      </label>
      <button type="button" class="btn" (click)="load(1)">Найти</button>
    </div>

    @if (editing(); as form) {
      <form class="card stack" style="margin-bottom:20px" (ngSubmit)="save()">
        <h3>{{ editingId() ? 'Редактирование товара' : 'Новый товар' }}</h3>

        <div class="form-grid">
          <label>
            <span>Артикул (SKU)</span>
            <input [(ngModel)]="form.sku" name="sku" required maxlength="64" />
          </label>
          <label>
            <span>Наименование</span>
            <input [(ngModel)]="form.name" name="name" required maxlength="300" />
          </label>
          <label>
            <span>Категория</span>
            <select [(ngModel)]="form.categoryId" name="categoryId">
              @for (category of categories(); track category.id) {
                <option [ngValue]="category.id">{{ category.name }}</option>
              }
            </select>
          </label>
          <label>
            <span>Бренд</span>
            <select [(ngModel)]="form.brandId" name="brandId">
              @for (brand of brands(); track brand.id) {
                <option [ngValue]="brand.id">{{ brand.name }}</option>
              }
            </select>
          </label>
          <label>
            <span>Цена, ₽</span>
            <input type="number" min="0" [(ngModel)]="form.price" name="price" required />
          </label>
          <label>
            <span>Старая цена, ₽</span>
            <input type="number" min="0" [(ngModel)]="form.oldPrice" name="oldPrice" />
          </label>
          <label>
            <span>Гарантия, мес.</span>
            <input type="number" min="0" max="120" [(ngModel)]="form.warrantyMonths" name="warrantyMonths" />
          </label>
          <label>
            <span>Вес, г</span>
            <input type="number" min="0" [(ngModel)]="form.weightGrams" name="weightGrams" />
          </label>
          @if (!editingId()) {
            <label>
              <span>Начальный остаток, шт.</span>
              <input type="number" min="0" [(ngModel)]="form.stockQuantity" name="stockQuantity" />
            </label>
          }
          <label class="inline">
            <input type="checkbox" [(ngModel)]="form.isActive" name="isActive" />
            Показывать в каталоге
          </label>
        </div>

        <label>
          <span>Описание</span>
          <textarea rows="3" [(ngModel)]="form.description" name="description"></textarea>
        </label>

        <label>
          <span>Ссылка на изображение</span>
          <input [(ngModel)]="imageUrl" name="imageUrl" placeholder="/assets/products/имя.svg" />
        </label>

        <div class="row">
          <button type="submit" class="btn btn--primary" [disabled]="busy()">Сохранить</button>
          <button type="button" class="btn" (click)="editing.set(null)">Отмена</button>
        </div>
      </form>
    }

    @if (page(); as data) {
      <div class="card table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Артикул</th>
              <th>Наименование</th>
              <th>Категория</th>
              <th>Цена</th>
              <th>Остаток</th>
              <th>Продано</th>
              <th>Активен</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (product of data.items; track product.id) {
              <tr>
                <td class="muted">{{ product.sku }}</td>
                <td>{{ product.name }}</td>
                <td>{{ product.categoryName }}</td>
                <td><app-money [value]="product.price" /></td>
                <td [class.danger]="product.stockQuantity < 5">{{ product.stockQuantity }}</td>
                <td>{{ product.soldCount }}</td>
                <td>{{ product.isActive ? 'да' : 'нет' }}</td>
                <td class="text-right nowrap">
                  <button type="button" class="btn btn--sm" (click)="startEdit(product)">Изменить</button>
                  <button type="button" class="btn btn--sm" (click)="changeStock(product)">Остаток</button>
                  <button type="button" class="btn btn--sm btn--danger" (click)="deactivate(product)">Скрыть</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <app-pagination [page]="data.page" [totalPages]="data.totalPages" (pageChange)="load($event)" />
    }
  `,
  styles: [
    `
      .form-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: 0 16px;
      }

      .inline {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .inline input {
        width: auto;
      }

      .nowrap {
        white-space: nowrap;
      }

      .danger {
        color: var(--danger);
        font-weight: 600;
      }
    `,
  ],
})
export class AdminProductsPage implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly toast = inject(ToastService);

  readonly page = signal<Paged<AdminProduct> | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly brands = signal<Brand[]>([]);
  readonly editing = signal<ProductInput | null>(null);
  readonly editingId = signal<number | null>(null);
  readonly busy = signal(false);

  search = '';
  lowStockOnly = false;
  imageUrl = '';

  ngOnInit(): void {
    this.admin.categories().subscribe((categories) => this.categories.set(categories));
    this.admin.brands().subscribe((brands) => this.brands.set(brands));
    this.load(1);
  }

  load(page: number): void {
    this.admin
      .products({
        search: this.search || undefined,
        lowStockOnly: this.lowStockOnly || undefined,
        includeInactive: true,
        page,
        pageSize: 20,
      })
      .subscribe((result) => this.page.set(result));
  }

  startCreate(): void {
    this.editingId.set(null);
    this.imageUrl = '';
    this.editing.set({
      sku: '',
      name: '',
      categoryId: this.categories()[0]?.id ?? 0,
      brandId: this.brands()[0]?.id ?? 0,
      description: '',
      price: 0,
      oldPrice: null,
      stockQuantity: 0,
      isActive: true,
      warrantyMonths: 12,
      weightGrams: null,
    });
  }

  startEdit(product: AdminProduct): void {
    this.editingId.set(product.id);
    this.imageUrl = product.images[0]?.url ?? '';
    this.editing.set({
      sku: product.sku,
      name: product.name,
      slug: product.slug,
      categoryId: product.categoryId,
      brandId: product.brandId,
      description: product.description ?? '',
      price: product.price,
      oldPrice: product.oldPrice ?? null,
      stockQuantity: product.stockQuantity,
      isActive: product.isActive,
      warrantyMonths: product.warrantyMonths,
      weightGrams: product.weightGrams ?? null,
      attributes: product.attributes,
    });
  }

  save(): void {
    const form = this.editing();
    if (!form) return;

    const input: ProductInput = {
      ...form,
      images: this.imageUrl
        ? [{ url: this.imageUrl, alt: form.name, isPrimary: true, sortOrder: 0 }]
        : [],
    };

    this.busy.set(true);
    const id = this.editingId();
    const request = id ? this.admin.updateProduct(id, input) : this.admin.createProduct(input);

    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.editing.set(null);
        this.toast.success(id ? 'Товар обновлён' : 'Товар создан');
        this.load(this.page()?.page ?? 1);
      },
      error: () => this.busy.set(false),
    });
  }

  /** Остаток изменяется только движением склада с обязательным комментарием (FR-45, FR-47). */
  changeStock(product: AdminProduct): void {
    const raw = prompt(`Изменение остатка «${product.name}» (текущий ${product.stockQuantity}).\nУкажите дельту, например 10 или -2:`, '10');
    if (!raw) return;

    const delta = Number(raw);
    if (!Number.isInteger(delta) || delta === 0) {
      this.toast.error('Дельта должна быть целым числом, отличным от нуля');
      return;
    }

    const comment = prompt('Комментарий к движению остатка:', delta > 0 ? 'Поставка' : 'Списание');
    if (!comment) {
      this.toast.error('Комментарий обязателен');
      return;
    }

    const reason = delta > 0 ? 'Purchase' : 'WriteOff';
    this.admin.changeStock(product.id, delta, reason, comment).subscribe(() => {
      this.toast.success('Остаток изменён');
      this.load(this.page()?.page ?? 1);
    });
  }

  deactivate(product: AdminProduct): void {
    if (!confirm(`Скрыть товар «${product.name}» из каталога?`)) return;

    this.admin.deactivateProduct(product.id).subscribe(() => {
      this.toast.info('Товар скрыт из каталога');
      this.load(this.page()?.page ?? 1);
    });
  }
}
