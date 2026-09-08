import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { Cart } from './models';

export const CART_ID_KEY = 'periphshop.cartId';

/** Идентификатор анонимной корзины (FR-20), передаётся в заголовке X-Cart-Id. */
export function getOrCreateCartId(): string {
  let id = localStorage.getItem(CART_ID_KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(CART_ID_KEY, id);
  }
  return id;
}

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);

  readonly cart = signal<Cart>({ items: [], itemsTotal: 0, itemsCount: 0, hasIssues: false });
  readonly count = computed(() => this.cart().itemsCount);
  readonly total = computed(() => this.cart().itemsTotal);

  load(): Observable<Cart> {
    return this.http.get<Cart>('/api/cart').pipe(tap((cart) => this.cart.set(cart)));
  }

  add(productId: number, quantity = 1): Observable<Cart> {
    return this.http
      .post<Cart>('/api/cart/items', { productId, quantity })
      .pipe(tap((cart) => this.cart.set(cart)));
  }

  update(productId: number, quantity: number): Observable<Cart> {
    return this.http
      .put<Cart>(`/api/cart/items/${productId}`, { quantity })
      .pipe(tap((cart) => this.cart.set(cart)));
  }

  remove(productId: number): Observable<Cart> {
    return this.http.delete<Cart>(`/api/cart/items/${productId}`).pipe(tap((cart) => this.cart.set(cart)));
  }

  clear(): Observable<Cart> {
    return this.http.delete<Cart>('/api/cart').pipe(tap((cart) => this.cart.set(cart)));
  }

  /** Слияние гостевой корзины с корзиной пользователя после входа (FR-21). */
  merge(): Observable<Cart> {
    return this.http.post<Cart>('/api/cart/merge', {}).pipe(tap((cart) => this.cart.set(cart)));
  }
}
