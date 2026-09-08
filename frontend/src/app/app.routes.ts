import { Routes } from '@angular/router';
import { adminGuard, authGuard, staffGuard } from './core/guards';

/** Карта экранов из раздела 8.1 ТЗ. Все страницы загружаются лениво. */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/home.page').then((m) => m.HomePage),
    title: 'PeriphShop — компьютерная периферия',
  },
  {
    path: 'catalog',
    loadComponent: () => import('./pages/catalog.page').then((m) => m.CatalogPage),
    title: 'Каталог — PeriphShop',
  },
  {
    path: 'product/:slug',
    loadComponent: () => import('./pages/product.page').then((m) => m.ProductPage),
  },
  {
    path: 'cart',
    loadComponent: () => import('./pages/cart.page').then((m) => m.CartPage),
    title: 'Корзина — PeriphShop',
  },
  {
    path: 'checkout',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/checkout.page').then((m) => m.CheckoutPage),
    title: 'Оформление заказа',
  },
  {
    path: 'login',
    loadComponent: () => import('./pages/login.page').then((m) => m.LoginPage),
    title: 'Вход',
  },
  {
    path: 'register',
    loadComponent: () => import('./pages/login.page').then((m) => m.RegisterPage),
    title: 'Регистрация',
  },
  {
    path: 'account/orders',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/orders.page').then((m) => m.OrdersPage),
    title: 'Мои заказы',
  },
  {
    path: 'account/orders/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/order-details.page').then((m) => m.OrderDetailsPage),
  },
  {
    path: 'account/favorites',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/favorites.page').then((m) => m.FavoritesPage),
    title: 'Избранное',
  },
  {
    path: 'account/profile',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/profile.page').then((m) => m.ProfilePage),
    title: 'Профиль',
  },
  {
    path: 'admin',
    canActivate: [authGuard, staffGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('./pages/admin/dashboard.page').then((m) => m.AdminDashboardPage),
        title: 'Рабочее место — сводка',
      },
      {
        path: 'products',
        loadComponent: () => import('./pages/admin/products.page').then((m) => m.AdminProductsPage),
        title: 'Товары',
      },
      {
        path: 'orders',
        loadComponent: () => import('./pages/admin/orders.page').then((m) => m.AdminOrdersPage),
        title: 'Заказы',
      },
      {
        path: 'reviews',
        loadComponent: () => import('./pages/admin/reviews.page').then((m) => m.AdminReviewsPage),
        title: 'Модерация отзывов',
      },
      {
        path: 'promo',
        loadComponent: () => import('./pages/admin/promo.page').then((m) => m.AdminPromoPage),
        title: 'Промокоды',
      },
      {
        path: 'users',
        canActivate: [adminGuard],
        loadComponent: () => import('./pages/admin/users.page').then((m) => m.AdminUsersPage),
        title: 'Пользователи',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
