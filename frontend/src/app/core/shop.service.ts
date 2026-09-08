import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Brand, Category, CreateOrderRequest, Favorite, Featured, OrderDetails, OrderListItem,
  Paged, ProductDetails, ProductListItem, ProductPage, PromoValidation, Review, User,
} from './models';

/** Параметры фильтрации каталога (FR-04, FR-05). */
export interface CatalogQuery {
  search?: string;
  categorySlug?: string;
  brandIds?: number[];
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  onlyDiscounted?: boolean;
  minRating?: number;
  sort?: 'New' | 'PriceAsc' | 'PriceDesc' | 'Rating' | 'Popular';
  page?: number;
  pageSize?: number;
}

/** Публичная часть API: каталог, отзывы, заказы, избранное, профиль. */
@Injectable({ providedIn: 'root' })
export class ShopService {
  private readonly http = inject(HttpClient);

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/catalog/categories');
  }

  brands(): Observable<Brand[]> {
    return this.http.get<Brand[]>('/api/catalog/brands');
  }

  products(query: CatalogQuery): Observable<ProductPage> {
    return this.http.get<ProductPage>('/api/catalog/products', { params: toParams(query) });
  }

  product(slug: string): Observable<ProductDetails> {
    return this.http.get<ProductDetails>(`/api/catalog/products/${slug}`);
  }

  similar(id: number): Observable<ProductListItem[]> {
    return this.http.get<ProductListItem[]>(`/api/catalog/products/${id}/similar`);
  }

  featured(): Observable<Featured> {
    return this.http.get<Featured>('/api/catalog/featured');
  }

  reviews(productId: number, page = 1): Observable<Paged<Review>> {
    return this.http.get<Paged<Review>>(`/api/reviews/product/${productId}`, {
      params: new HttpParams().set('page', page).set('pageSize', 10),
    });
  }

  createReview(productId: number, rating: number, title: string, body: string): Observable<Review> {
    return this.http.post<Review>('/api/reviews', { productId, rating, title, body });
  }

  validatePromo(code: string, itemsTotal: number): Observable<PromoValidation> {
    return this.http.post<PromoValidation>('/api/promo/validate', { code, itemsTotal });
  }

  createOrder(request: CreateOrderRequest): Observable<OrderDetails> {
    return this.http.post<OrderDetails>('/api/orders', request);
  }

  orders(page = 1): Observable<Paged<OrderListItem>> {
    return this.http.get<Paged<OrderListItem>>('/api/orders', {
      params: new HttpParams().set('page', page).set('pageSize', 10),
    });
  }

  order(id: number): Observable<OrderDetails> {
    return this.http.get<OrderDetails>(`/api/orders/${id}`);
  }

  cancelOrder(id: number, reason: string): Observable<OrderDetails> {
    return this.http.post<OrderDetails>(`/api/orders/${id}/cancel`, { reason });
  }

  payOrder(id: number): Observable<OrderDetails> {
    return this.http.post<OrderDetails>(`/api/orders/${id}/pay`, {});
  }

  favorites(): Observable<Favorite[]> {
    return this.http.get<Favorite[]>('/api/favorites');
  }

  addFavorite(productId: number): Observable<void> {
    return this.http.post<void>(`/api/favorites/${productId}`, {});
  }

  removeFavorite(productId: number): Observable<void> {
    return this.http.delete<void>(`/api/favorites/${productId}`);
  }

  profile(): Observable<User> {
    return this.http.get<User>('/api/profile');
  }

  updateProfile(fullName: string, phone: string, defaultAddress: string): Observable<User> {
    return this.http.put<User>('/api/profile', { fullName, phone, defaultAddress });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/profile/change-password', { currentPassword, newPassword });
  }
}

/** Сериализация query-объекта в HttpParams с пропуском пустых значений. */
export function toParams(source: object): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(source)) {
    if (value === undefined || value === null || value === '') continue;

    if (Array.isArray(value)) {
      for (const item of value) params = params.append(key, String(item));
    } else {
      params = params.set(key, String(value));
    }
  }

  return params;
}
