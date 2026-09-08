import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { toParams } from './shop.service';
import {
  AdminProduct, AdminUser, Brand, Category, LowStock, OrderDetails, OrderListItem, OrderStatus,
  Paged, ProductInput, PromoCode, Review, Role, SalesByDay, SalesSummary, TopProduct,
} from './models';

export interface AdminProductQuery {
  search?: string;
  categoryId?: number;
  includeInactive?: boolean;
  lowStockOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface AdminOrderQuery {
  status?: OrderStatus;
  search?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

/** Рабочее место менеджера и администратора (раздел 9 API). */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  // ---------- Товары ----------

  products(query: AdminProductQuery): Observable<Paged<AdminProduct>> {
    return this.http.get<Paged<AdminProduct>>('/api/admin/products', { params: toParams(query) });
  }

  product(id: number): Observable<AdminProduct> {
    return this.http.get<AdminProduct>(`/api/admin/products/${id}`);
  }

  createProduct(input: ProductInput): Observable<AdminProduct> {
    return this.http.post<AdminProduct>('/api/admin/products', input);
  }

  updateProduct(id: number, input: ProductInput): Observable<AdminProduct> {
    return this.http.put<AdminProduct>(`/api/admin/products/${id}`, input);
  }

  deactivateProduct(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/products/${id}`);
  }

  changeStock(id: number, delta: number, reason: string, comment: string): Observable<AdminProduct> {
    return this.http.post<AdminProduct>(`/api/admin/products/${id}/stock`, { delta, reason, comment });
  }

  // ---------- Справочники ----------

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/admin/categories');
  }

  createCategory(body: unknown): Observable<Category> {
    return this.http.post<Category>('/api/admin/categories', body);
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/categories/${id}`);
  }

  brands(): Observable<Brand[]> {
    return this.http.get<Brand[]>('/api/admin/brands');
  }

  createBrand(body: unknown): Observable<Brand> {
    return this.http.post<Brand>('/api/admin/brands', body);
  }

  // ---------- Заказы ----------

  orders(query: AdminOrderQuery): Observable<Paged<OrderListItem>> {
    return this.http.get<Paged<OrderListItem>>('/api/admin/orders', { params: toParams(query) });
  }

  order(id: number): Observable<OrderDetails> {
    return this.http.get<OrderDetails>(`/api/admin/orders/${id}`);
  }

  changeOrderStatus(id: number, status: OrderStatus, comment: string): Observable<OrderDetails> {
    return this.http.post<OrderDetails>(`/api/admin/orders/${id}/status`, { status, comment });
  }

  exportOrders(from?: string, to?: string): Observable<Blob> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get('/api/admin/orders/export', { params, responseType: 'blob' });
  }

  // ---------- Отзывы ----------

  reviews(approved: boolean | undefined, page = 1): Observable<Paged<Review>> {
    return this.http.get<Paged<Review>>('/api/admin/reviews', {
      params: toParams({ approved, page, pageSize: 20 }),
    });
  }

  approveReview(id: number): Observable<void> {
    return this.http.post<void>(`/api/admin/reviews/${id}/approve`, {});
  }

  deleteReview(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/reviews/${id}`);
  }

  // ---------- Промокоды ----------

  promos(): Observable<PromoCode[]> {
    return this.http.get<PromoCode[]>('/api/admin/promo');
  }

  createPromo(body: unknown): Observable<PromoCode> {
    return this.http.post<PromoCode>('/api/admin/promo', body);
  }

  updatePromo(id: number, body: unknown): Observable<PromoCode> {
    return this.http.put<PromoCode>(`/api/admin/promo/${id}`, body);
  }

  deletePromo(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/promo/${id}`);
  }

  // ---------- Пользователи ----------

  users(search: string, page = 1): Observable<Paged<AdminUser>> {
    return this.http.get<Paged<AdminUser>>('/api/admin/users', {
      params: toParams({ search, page, pageSize: 20 }),
    });
  }

  changeRole(id: number, role: Role): Observable<AdminUser> {
    return this.http.put<AdminUser>(`/api/admin/users/${id}/role`, { role });
  }

  toggleActive(id: number): Observable<AdminUser> {
    return this.http.post<AdminUser>(`/api/admin/users/${id}/toggle-active`, {});
  }

  // ---------- Отчёты ----------

  summary(from?: string, to?: string): Observable<SalesSummary> {
    return this.http.get<SalesSummary>('/api/admin/reports/summary', { params: toParams({ from, to }) });
  }

  topProducts(from?: string, to?: string): Observable<TopProduct[]> {
    return this.http.get<TopProduct[]>('/api/admin/reports/top-products', { params: toParams({ from, to }) });
  }

  lowStock(threshold = 5): Observable<LowStock[]> {
    return this.http.get<LowStock[]>('/api/admin/reports/low-stock', { params: toParams({ threshold }) });
  }

  salesByDay(from?: string, to?: string): Observable<SalesByDay[]> {
    return this.http.get<SalesByDay[]>('/api/admin/reports/sales-by-day', { params: toParams({ from, to }) });
  }
}
