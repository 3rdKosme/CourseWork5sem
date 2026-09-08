import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartService } from '../core/cart.service';
import { Favorite } from '../core/models';
import { ShopService } from '../core/shop.service';
import { ToastService } from '../core/toast.service';
import { MoneyComponent } from '../shared/ui';

/** Избранные товары покупателя. */
@Component({
  selector: 'app-favorites',
  standalone: true,
  imports: [CommonModule, RouterLink, MoneyComponent],
  template: `
    <h1>Избранное</h1>

    @if (items().length === 0) {
      <div class="card empty">Список пуст. <a routerLink="/catalog">Найти товары</a></div>
    } @else {
      <div class="card table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Товар</th>
              <th>Цена</th>
              <th>Наличие</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.productId) {
              <tr>
                <td><a [routerLink]="['/product', item.slug]">{{ item.name }}</a></td>
                <td><app-money [value]="item.price" /></td>
                <td>{{ item.inStock ? 'В наличии' : 'Нет в наличии' }}</td>
                <td class="text-right">
                  <button type="button" class="btn btn--sm btn--primary" [disabled]="!item.inStock" (click)="addToCart(item.productId)">
                    В корзину
                  </button>
                  <button type="button" class="btn btn--sm btn--danger" (click)="remove(item.productId)">Удалить</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class FavoritesPage implements OnInit {
  private readonly shop = inject(ShopService);
  private readonly cart = inject(CartService);
  private readonly toast = inject(ToastService);

  readonly items = signal<Favorite[]>([]);

  ngOnInit(): void {
    this.load();
  }

  addToCart(productId: number): void {
    this.cart.add(productId).subscribe(() => this.toast.success('Товар добавлен в корзину'));
  }

  remove(productId: number): void {
    this.shop.removeFavorite(productId).subscribe(() => this.load());
  }

  private load(): void {
    this.shop.favorites().subscribe((items) => this.items.set(items));
  }
}
