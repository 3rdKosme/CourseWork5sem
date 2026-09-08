// Типы, повторяющие контракты REST API (docs/API.md).

export type Role = 'Customer' | 'Manager' | 'Admin';

export interface User {
  id: number;
  email: string;
  fullName: string;
  phone?: string | null;
  defaultAddress?: string | null;
  role: Role;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface Category {
  id: number;
  name: string;
  slug: string;
  parentId: number | null;
  description?: string | null;
  productCount: number;
  children: Category[];
}

export interface Brand {
  id: number;
  name: string;
  slug: string;
  country?: string | null;
  website?: string | null;
  logoUrl?: string | null;
}

export interface ProductListItem {
  id: number;
  sku: string;
  name: string;
  slug: string;
  price: number;
  oldPrice?: number | null;
  inStock: boolean;
  stockQuantity: number;
  ratingAvg: number;
  ratingCount: number;
  brandName: string;
  categoryName: string;
  primaryImageUrl?: string | null;
}

export interface ProductImage {
  url: string;
  alt?: string | null;
  isPrimary: boolean;
  sortOrder: number;
}

export interface ProductAttribute {
  groupName?: string | null;
  name: string;
  value: string;
  unit?: string | null;
  sortOrder: number;
}

export interface ProductDetails extends ProductListItem {
  description?: string | null;
  warrantyMonths: number;
  weightGrams?: number | null;
  categoryId: number;
  categorySlug: string;
  brandId: number;
  images: ProductImage[];
  attributes: ProductAttribute[];
}

export interface CatalogFacets {
  brands: { id: number; name: string; count: number }[];
  minPrice: number;
  maxPrice: number;
}

export interface ProductPage extends Paged<ProductListItem> {
  facets: CatalogFacets;
}

export interface Featured {
  bestsellers: ProductListItem[];
  discounted: ProductListItem[];
  newest: ProductListItem[];
}

export interface CartItem {
  productId: number;
  name: string;
  slug: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  stockQuantity: number;
  imageUrl?: string | null;
  hasStockIssue: boolean;
}

export interface Cart {
  items: CartItem[];
  itemsTotal: number;
  itemsCount: number;
  hasIssues: boolean;
}

export type DeliveryMethod = 'Pickup' | 'Courier' | 'PostMachine';
export type PaymentMethod = 'CashOnDelivery' | 'CardOnline';
export type OrderStatus =
  | 'New' | 'Paid' | 'Processing' | 'Shipped' | 'Delivered' | 'Completed' | 'Cancelled' | 'Refunded';

export interface OrderListItem {
  id: number;
  number: string;
  status: OrderStatus;
  total: number;
  itemsCount: number;
  deliveryMethod: DeliveryMethod;
  paymentStatus: string;
  createdAt: string;
}

export interface OrderItem {
  productId?: number | null;
  productName: string;
  sku: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderStatusHistoryEntry {
  fromStatus?: string | null;
  toStatus: string;
  changedBy?: string | null;
  comment?: string | null;
  createdAt: string;
}

export interface OrderDetails {
  id: number;
  number: string;
  status: OrderStatus;
  itemsTotal: number;
  discountTotal: number;
  deliveryCost: number;
  total: number;
  deliveryMethod: DeliveryMethod;
  deliveryAddress?: string | null;
  recipientName: string;
  recipientPhone: string;
  comment?: string | null;
  paymentMethod: PaymentMethod;
  paymentStatus: string;
  promoCode?: string | null;
  createdAt: string;
  paidAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;
  cancelReason?: string | null;
  customerEmail?: string | null;
  customerName?: string | null;
  items: OrderItem[];
  statusHistory: OrderStatusHistoryEntry[];
  allowedNextStatuses: OrderStatus[];
}

export interface CreateOrderRequest {
  deliveryMethod: DeliveryMethod;
  deliveryAddress?: string | null;
  recipientName: string;
  recipientPhone: string;
  paymentMethod: PaymentMethod;
  comment?: string | null;
  promoCode?: string | null;
}

export interface PromoValidation {
  valid: boolean;
  discount: number;
  message: string;
}

export interface Review {
  id: number;
  productId: number;
  productName?: string | null;
  authorName: string;
  rating: number;
  title?: string | null;
  body: string;
  isApproved: boolean;
  createdAt: string;
}

export interface Favorite {
  productId: number;
  name: string;
  slug: string;
  price: number;
  imageUrl?: string | null;
  inStock: boolean;
}

// ---------- Администрирование ----------

export interface AdminProduct {
  id: number;
  sku: string;
  name: string;
  slug: string;
  categoryId: number;
  categoryName: string;
  brandId: number;
  brandName: string;
  description?: string | null;
  price: number;
  oldPrice?: number | null;
  stockQuantity: number;
  isActive: boolean;
  warrantyMonths: number;
  weightGrams?: number | null;
  ratingAvg: number;
  ratingCount: number;
  soldCount: number;
  createdAt: string;
  images: ProductImage[];
  attributes: ProductAttribute[];
}

export interface ProductInput {
  sku: string;
  name: string;
  slug?: string | null;
  categoryId: number;
  brandId: number;
  description?: string | null;
  price: number;
  oldPrice?: number | null;
  stockQuantity: number;
  isActive: boolean;
  warrantyMonths: number;
  weightGrams?: number | null;
  images?: ProductImage[];
  attributes?: ProductAttribute[];
}

export interface PromoCode {
  id: number;
  code: string;
  discountType: 'Percent' | 'Amount';
  discountValue: number;
  minOrderTotal: number;
  validFrom?: string | null;
  validTo?: string | null;
  usageLimit?: number | null;
  usedCount: number;
  isActive: boolean;
}

export interface AdminUser {
  id: number;
  email: string;
  fullName: string;
  phone?: string | null;
  role: Role;
  isActive: boolean;
  ordersCount: number;
  createdAt: string;
}

export interface SalesSummary {
  from: string;
  to: string;
  ordersCount: number;
  revenue: number;
  averageCheck: number;
  newCustomers: number;
  itemsSold: number;
  ordersByStatus: Record<string, number>;
}

export interface TopProduct {
  productId?: number | null;
  productName: string;
  sku: string;
  quantity: number;
  revenue: number;
}

export interface LowStock {
  id: number;
  sku: string;
  name: string;
  stockQuantity: number;
  isActive: boolean;
}

export interface SalesByDay {
  date: string;
  ordersCount: number;
  revenue: number;
}

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  New: 'Новый',
  Paid: 'Оплачен',
  Processing: 'В сборке',
  Shipped: 'Передан в доставку',
  Delivered: 'Доставлен',
  Completed: 'Завершён',
  Cancelled: 'Отменён',
  Refunded: 'Возврат',
};

export const DELIVERY_LABELS: Record<DeliveryMethod, string> = {
  Pickup: 'Самовывоз',
  Courier: 'Курьер',
  PostMachine: 'Постамат',
};

export const PAYMENT_LABELS: Record<PaymentMethod, string> = {
  CashOnDelivery: 'При получении',
  CardOnline: 'Картой онлайн',
};
