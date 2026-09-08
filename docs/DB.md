# Модель данных PeriphShop (MySQL 8.4)

Кодировка всех таблиц — `utf8mb4`, сравнение — `utf8mb4_0900_ai_ci`, движок — `InnoDB`.
Денежные величины — `DECIMAL(12,2)`. Даты хранятся в UTC (`DATETIME(6)`).
Схема создаётся миграциями EF Core (`backend/src/PeriphShop.Infrastructure/Migrations`);
приведённый ниже DDL — документация результата, а не отдельный источник истины.

## 1. Перечисления

| Перечисление | Значения (хранятся как `TINYINT`) |
|--------------|-----------------------------------|
| `UserRole` | 0 `Customer`, 1 `Manager`, 2 `Admin` |
| `OrderStatus` | 0 `New`, 1 `Paid`, 2 `Processing`, 3 `Shipped`, 4 `Delivered`, 5 `Completed`, 6 `Cancelled`, 7 `Refunded` |
| `DeliveryMethod` | 0 `Pickup`, 1 `Courier`, 2 `PostMachine` |
| `PaymentMethod` | 0 `CashOnDelivery`, 1 `CardOnline` |
| `PaymentStatus` | 0 `Pending`, 1 `Paid`, 2 `Refunded` |
| `StockMovementReason` | 0 `Purchase`, 1 `Sale`, 2 `Cancellation`, 3 `Correction`, 4 `WriteOff` |
| `DiscountType` | 0 `Percent`, 1 `Amount` |

## 2. Таблицы

### 2.1. `users` — пользователи

| Поле | Тип | Ограничения | Описание |
|------|-----|-------------|----------|
| `id` | INT | PK, AI | Идентификатор |
| `email` | VARCHAR(256) | NOT NULL, UNIQUE | Логин |
| `password_hash` | VARCHAR(256) | NOT NULL | BCrypt-хэш |
| `full_name` | VARCHAR(200) | NOT NULL | ФИО |
| `phone` | VARCHAR(32) | NULL | Телефон |
| `default_address` | VARCHAR(500) | NULL | Адрес доставки по умолчанию |
| `role` | TINYINT | NOT NULL, DEFAULT 0 | Роль (`UserRole`) |
| `is_active` | TINYINT(1) | NOT NULL, DEFAULT 1 | Признак активности |
| `created_at` | DATETIME(6) | NOT NULL | Дата регистрации |
| `updated_at` | DATETIME(6) | NULL | Дата изменения |

Индексы: `UX_users_email (email)`.

### 2.2. `refresh_tokens` — токены обновления

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `user_id` | INT | FK → `users.id`, ON DELETE CASCADE |
| `token` | VARCHAR(128) | NOT NULL, UNIQUE |
| `expires_at` | DATETIME(6) | NOT NULL |
| `created_at` | DATETIME(6) | NOT NULL |
| `revoked_at` | DATETIME(6) | NULL |
| `created_by_ip` | VARCHAR(64) | NULL |

Индексы: `UX_refresh_tokens_token`, `IX_refresh_tokens_user_id`.

### 2.3. `categories` — категории каталога

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `name` | VARCHAR(150) | NOT NULL |
| `slug` | VARCHAR(160) | NOT NULL, UNIQUE |
| `parent_id` | INT | NULL, FK → `categories.id`, ON DELETE RESTRICT |
| `description` | VARCHAR(1000) | NULL |
| `sort_order` | INT | NOT NULL, DEFAULT 0 |
| `is_active` | TINYINT(1) | NOT NULL, DEFAULT 1 |

Иерархия — до трёх уровней; удаление категории с потомками или товарами запрещено.

### 2.4. `brands` — производители

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `name` | VARCHAR(150) | NOT NULL |
| `slug` | VARCHAR(160) | NOT NULL, UNIQUE |
| `country` | VARCHAR(100) | NULL |
| `website` | VARCHAR(300) | NULL |
| `logo_url` | VARCHAR(500) | NULL |

### 2.5. `products` — товары

| Поле | Тип | Ограничения | Описание |
|------|-----|-------------|----------|
| `id` | INT | PK, AI | |
| `sku` | VARCHAR(64) | NOT NULL, UNIQUE | Артикул |
| `name` | VARCHAR(300) | NOT NULL | Наименование |
| `slug` | VARCHAR(320) | NOT NULL, UNIQUE | ЧПУ-адрес |
| `category_id` | INT | NOT NULL, FK → `categories.id`, RESTRICT | |
| `brand_id` | INT | NOT NULL, FK → `brands.id`, RESTRICT | |
| `description` | TEXT | NULL | Описание |
| `price` | DECIMAL(12,2) | NOT NULL, CHECK ≥ 0 | Текущая цена |
| `old_price` | DECIMAL(12,2) | NULL | Цена до скидки |
| `stock_quantity` | INT | NOT NULL, DEFAULT 0, CHECK ≥ 0 | Остаток |
| `is_active` | TINYINT(1) | NOT NULL, DEFAULT 1 | Показывать в каталоге |
| `rating_avg` | DECIMAL(3,2) | NOT NULL, DEFAULT 0 | Средний рейтинг (денормализация) |
| `rating_count` | INT | NOT NULL, DEFAULT 0 | Число опубликованных отзывов |
| `sold_count` | INT | NOT NULL, DEFAULT 0 | Продано штук (для сортировки по популярности) |
| `view_count` | INT | NOT NULL, DEFAULT 0 | Просмотры карточки |
| `warranty_months` | INT | NOT NULL, DEFAULT 12 | Гарантия |
| `weight_grams` | INT | NULL | Вес |
| `created_at` | DATETIME(6) | NOT NULL | |
| `updated_at` | DATETIME(6) | NULL | |

Индексы: `UX_products_sku`, `UX_products_slug`, `IX_products_category_id`,
`IX_products_brand_id`, `IX_products_price`, `IX_products_is_active_created_at`,
полнотекстовый `FT_products_name_description (name, description)`.

### 2.6. `product_images` — изображения

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `product_id` | INT | FK → `products.id`, CASCADE |
| `url` | VARCHAR(500) | NOT NULL |
| `alt` | VARCHAR(300) | NULL |
| `sort_order` | INT | NOT NULL, DEFAULT 0 |
| `is_primary` | TINYINT(1) | NOT NULL, DEFAULT 0 |

### 2.7. `product_attributes` — характеристики

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `product_id` | INT | FK → `products.id`, CASCADE |
| `group_name` | VARCHAR(100) | NULL (например «Электропитание») |
| `name` | VARCHAR(150) | NOT NULL (например «Тип переключателей») |
| `value` | VARCHAR(300) | NOT NULL (например «Cherry MX Red») |
| `unit` | VARCHAR(30) | NULL (например «мм») |
| `sort_order` | INT | NOT NULL, DEFAULT 0 |

Индекс: `IX_product_attributes_product_id`.

### 2.8. `reviews` — отзывы

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `product_id` | INT | FK → `products.id`, CASCADE |
| `user_id` | INT | FK → `users.id`, CASCADE |
| `rating` | TINYINT | NOT NULL, CHECK 1..5 |
| `title` | VARCHAR(200) | NULL |
| `body` | VARCHAR(2000) | NOT NULL |
| `is_approved` | TINYINT(1) | NOT NULL, DEFAULT 0 |
| `created_at` | DATETIME(6) | NOT NULL |

Индексы: `UX_reviews_product_user (product_id, user_id)`, `IX_reviews_is_approved`.

### 2.9. `carts` и `cart_items` — корзины

`carts`

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `user_id` | INT | NULL, FK → `users.id`, CASCADE |
| `anonymous_id` | CHAR(36) | NULL (GUID гостя) |
| `created_at` | DATETIME(6) | NOT NULL |
| `updated_at` | DATETIME(6) | NOT NULL |

Индексы: `UX_carts_user_id (user_id)`, `UX_carts_anonymous_id (anonymous_id)`.

`cart_items`

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `cart_id` | INT | FK → `carts.id`, CASCADE |
| `product_id` | INT | FK → `products.id`, CASCADE |
| `quantity` | INT | NOT NULL, CHECK 1..99 |
| `added_at` | DATETIME(6) | NOT NULL |

Индекс: `UX_cart_items_cart_product (cart_id, product_id)`.

### 2.10. `orders` — заказы

| Поле | Тип | Ограничения | Описание |
|------|-----|-------------|----------|
| `id` | INT | PK, AI | |
| `number` | VARCHAR(32) | NOT NULL, UNIQUE | `ORD-ГГГГММДД-NNNN` |
| `user_id` | INT | NOT NULL, FK → `users.id`, RESTRICT | Покупатель |
| `status` | TINYINT | NOT NULL | `OrderStatus` |
| `items_total` | DECIMAL(12,2) | NOT NULL | Сумма позиций |
| `discount_total` | DECIMAL(12,2) | NOT NULL, DEFAULT 0 | Скидка |
| `delivery_cost` | DECIMAL(12,2) | NOT NULL, DEFAULT 0 | Доставка |
| `total` | DECIMAL(12,2) | NOT NULL | Итог |
| `promo_code_id` | INT | NULL, FK → `promo_codes.id`, SET NULL | |
| `delivery_method` | TINYINT | NOT NULL | `DeliveryMethod` |
| `delivery_address` | VARCHAR(500) | NULL | Требуется для курьера и постамата |
| `recipient_name` | VARCHAR(200) | NOT NULL | |
| `recipient_phone` | VARCHAR(32) | NOT NULL | |
| `comment` | VARCHAR(1000) | NULL | |
| `payment_method` | TINYINT | NOT NULL | `PaymentMethod` |
| `payment_status` | TINYINT | NOT NULL, DEFAULT 0 | `PaymentStatus` |
| `created_at` | DATETIME(6) | NOT NULL | |
| `paid_at` | DATETIME(6) | NULL | |
| `completed_at` | DATETIME(6) | NULL | |
| `cancelled_at` | DATETIME(6) | NULL | |
| `cancel_reason` | VARCHAR(500) | NULL | |

Индексы: `UX_orders_number`, `IX_orders_user_id`, `IX_orders_status`, `IX_orders_created_at`.

### 2.11. `order_items` — позиции заказа

| Поле | Тип | Ограничения | Описание |
|------|-----|-------------|----------|
| `id` | INT | PK, AI | |
| `order_id` | INT | FK → `orders.id`, CASCADE | |
| `product_id` | INT | NULL, FK → `products.id`, SET NULL | Товар мог быть удалён |
| `product_name` | VARCHAR(300) | NOT NULL | Снимок наименования |
| `sku` | VARCHAR(64) | NOT NULL | Снимок артикула |
| `unit_price` | DECIMAL(12,2) | NOT NULL | Зафиксированная цена |
| `quantity` | INT | NOT NULL, CHECK ≥ 1 | |
| `line_total` | DECIMAL(12,2) | NOT NULL | `unit_price * quantity` |

Позиция хранит снимок наименования, артикула и цены: изменение карточки товара
не изменяет ранее оформленные заказы.

### 2.12. `order_status_history` — история статусов

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `order_id` | INT | FK → `orders.id`, CASCADE |
| `from_status` | TINYINT | NULL |
| `to_status` | TINYINT | NOT NULL |
| `changed_by_user_id` | INT | NULL, FK → `users.id`, SET NULL |
| `comment` | VARCHAR(500) | NULL |
| `created_at` | DATETIME(6) | NOT NULL |

### 2.13. `promo_codes` — промокоды

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `code` | VARCHAR(40) | NOT NULL, UNIQUE |
| `discount_type` | TINYINT | NOT NULL (`DiscountType`) |
| `discount_value` | DECIMAL(12,2) | NOT NULL, CHECK > 0 |
| `min_order_total` | DECIMAL(12,2) | NOT NULL, DEFAULT 0 |
| `valid_from` | DATETIME(6) | NULL |
| `valid_to` | DATETIME(6) | NULL |
| `usage_limit` | INT | NULL |
| `used_count` | INT | NOT NULL, DEFAULT 0 |
| `is_active` | TINYINT(1) | NOT NULL, DEFAULT 1 |

### 2.14. `stock_movements` — движения остатков

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | INT | PK, AI |
| `product_id` | INT | FK → `products.id`, CASCADE |
| `delta` | INT | NOT NULL (отрицательное — списание) |
| `reason` | TINYINT | NOT NULL (`StockMovementReason`) |
| `order_id` | INT | NULL, FK → `orders.id`, SET NULL |
| `comment` | VARCHAR(500) | NULL |
| `created_by_user_id` | INT | NULL, FK → `users.id`, SET NULL |
| `created_at` | DATETIME(6) | NOT NULL |

Индексы: `IX_stock_movements_product_id`, `IX_stock_movements_created_at`.

### 2.15. `favorites` — избранное

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `user_id` | INT | PK (составной), FK → `users.id`, CASCADE |
| `product_id` | INT | PK (составной), FK → `products.id`, CASCADE |
| `created_at` | DATETIME(6) | NOT NULL |

### 2.16. `audit_logs` — журнал аудита

| Поле | Тип | Ограничения |
|------|-----|-------------|
| `id` | BIGINT | PK, AI |
| `user_id` | INT | NULL, FK → `users.id`, SET NULL |
| `action` | VARCHAR(100) | NOT NULL (`ProductUpdated`, `OrderStatusChanged`, …) |
| `entity` | VARCHAR(100) | NOT NULL |
| `entity_id` | VARCHAR(64) | NULL |
| `payload` | JSON | NULL |
| `ip` | VARCHAR(64) | NULL |
| `created_at` | DATETIME(6) | NOT NULL |

Индексы: `IX_audit_logs_created_at`, `IX_audit_logs_entity_entity_id`.

## 3. Инварианты целостности

1. `products.stock_quantity ≥ 0` — обеспечивается проверкой в транзакции и CHECK-ограничением.
2. `orders.total = items_total − discount_total + delivery_cost` — вычисляется сервисом заказа.
3. `order_items.line_total = unit_price × quantity`.
4. Сумма `stock_movements.delta` по товару соответствует изменениям `products.stock_quantity`
   с момента начального прихода (сверяется отчётом).
5. `promo_codes.used_count ≤ usage_limit` при заданном лимите.
6. `reviews` уникальны по паре (`product_id`, `user_id`).
7. `carts` имеет либо `user_id`, либо `anonymous_id` (одно из двух не NULL).

## 4. Стратегия миграций

- Изменения схемы вносятся только миграциями EF Core: `dotnet ef migrations add <Name>`.
- Миграции применяются автоматически при старте API (`db.Database.Migrate()`), что делает
  развёртывание идемпотентным.
- Сид-данные (`DbSeeder`) выполняются после миграций и вставляют записи только при пустой таблице:
  3 пользователя, 4 бренда, 9 категорий, 20 товаров с изображениями и характеристиками, 2 промокода.
- Откат в стенде выполняется пересозданием тома: `docker compose down -v && docker compose up -d`.

## 5. Примеры аналитических запросов

Выручка по дням за период:

```sql
SELECT DATE(created_at) AS d, COUNT(*) AS orders, SUM(total) AS revenue
FROM orders
WHERE status NOT IN (6, 7) AND created_at >= ? AND created_at < ?
GROUP BY DATE(created_at)
ORDER BY d;
```

Топ-10 товаров по продажам:

```sql
SELECT oi.product_id, oi.product_name, SUM(oi.quantity) AS qty, SUM(oi.line_total) AS revenue
FROM order_items oi
JOIN orders o ON o.id = oi.order_id
WHERE o.status NOT IN (6, 7) AND o.created_at >= ?
GROUP BY oi.product_id, oi.product_name
ORDER BY qty DESC
LIMIT 10;
```

Товары, заканчивающиеся на складе:

```sql
SELECT id, sku, name, stock_quantity
FROM products
WHERE is_active = 1 AND stock_quantity < 5
ORDER BY stock_quantity ASC;
```
