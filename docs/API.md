# REST API PeriphShop

Базовый адрес стенда: `http://localhost/api` (через Nginx) или `http://localhost:8080/api` (напрямую).
Интерактивная документация: `http://localhost:8080/swagger`.

Общие правила:

- Формат — JSON, кодировка UTF-8, даты — ISO 8601 в UTC (`2026-09-08T12:30:00Z`).
- Аутентификация — заголовок `Authorization: Bearer <accessToken>`.
- Анонимная корзина — заголовок `X-Cart-Id: <guid>`, генерируется клиентом и хранится в `localStorage`.
- Ошибки — RFC 7807:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Недостаточно товара на складе",
  "status": 409,
  "detail": "Клавиатура Keychron K8: доступно 2, запрошено 5",
  "traceId": "00-6f1b...-01",
  "errors": { "quantity": ["Значение должно быть от 1 до 99"] }
}
```

- Постраничные ответы:

```json
{ "items": [], "page": 1, "pageSize": 12, "totalItems": 137, "totalPages": 12 }
```

---

## 1. Аутентификация — `/api/auth`

| Метод | Путь | Доступ | Описание |
|-------|------|--------|----------|
| POST | `/api/auth/register` | все | Регистрация покупателя |
| POST | `/api/auth/login` | все | Вход (лимит 10 запросов/мин) |
| POST | `/api/auth/refresh` | все | Обновление пары токенов с ротацией |
| POST | `/api/auth/logout` | все | Отзыв refresh-токена |
| GET | `/api/auth/me` | авторизован | Текущий пользователь |

**POST `/api/auth/register`**

```json
{ "email": "user@example.com", "password": "Passw0rd", "fullName": "Иванов Иван", "phone": "+7 900 000-00-00" }
```

Ответ `201 Created`:

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "6c1f...",
  "expiresAt": "2026-09-08T13:00:00Z",
  "user": { "id": 12, "email": "user@example.com", "fullName": "Иванов Иван", "role": "Customer" }
}
```

Ошибки: `400` — слабый пароль/невалидный e-mail; `409` — e-mail занят.

**POST `/api/auth/login`** → тело `{ "email", "password" }`, ответ как выше.
Ошибки: `401` — неверные учётные данные; `403` — учётная запись заблокирована; `429` — лимит попыток.

**POST `/api/auth/refresh`** → `{ "refreshToken": "…" }`. Старый токен отзывается, выдаётся новая пара.

---

## 2. Каталог — `/api/catalog`

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/catalog/categories` | Дерево категорий с количеством товаров |
| GET | `/api/catalog/brands` | Список брендов |
| GET | `/api/catalog/products` | Поиск и фильтрация товаров |
| GET | `/api/catalog/products/{slug}` | Карточка товара (инкрементирует просмотры) |
| GET | `/api/catalog/products/{id}/similar` | До 8 похожих товаров |
| GET | `/api/catalog/featured` | Подборки для главной: хиты, скидки, новинки |

**GET `/api/catalog/products`** — параметры запроса:

| Параметр | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `search` | string | — | Поиск по названию, SKU, описанию |
| `categorySlug` | string | — | Категория (включая подкатегории) |
| `brandIds` | int[] | — | `brandIds=1&brandIds=4` |
| `minPrice`, `maxPrice` | decimal | — | Диапазон цены |
| `inStock` | bool | — | Только в наличии |
| `onlyDiscounted` | bool | — | Только со скидкой |
| `minRating` | decimal | — | Минимальный рейтинг |
| `sort` | enum | `new` | `new`, `price_asc`, `price_desc`, `rating`, `popular` |
| `page` | int | 1 | Номер страницы |
| `pageSize` | int | 12 | Размер страницы, максимум 100 |

Ответ `200 OK`:

```json
{
  "items": [
    {
      "id": 3, "sku": "KB-KEY-K8", "name": "Keychron K8 Pro", "slug": "keychron-k8-pro",
      "price": 9990.00, "oldPrice": 11990.00, "inStock": true, "stockQuantity": 14,
      "ratingAvg": 4.7, "ratingCount": 23, "brandName": "Keychron",
      "categoryName": "Клавиатуры", "primaryImageUrl": "/img/keychron-k8.jpg"
    }
  ],
  "page": 1, "pageSize": 12, "totalItems": 37, "totalPages": 4,
  "facets": {
    "brands": [ { "id": 2, "name": "Keychron", "count": 6 } ],
    "minPrice": 490.00, "maxPrice": 129990.00
  }
}
```

**GET `/api/catalog/products/{slug}`** — дополнительно возвращает `description`, `images[]`,
`attributes[]` (сгруппированные), `warrantyMonths`, `weightGrams`, `reviewsSummary`.

---

## 3. Отзывы — `/api/reviews`

| Метод | Путь | Доступ | Описание |
|-------|------|--------|----------|
| GET | `/api/reviews/product/{productId}?page=&pageSize=` | все | Опубликованные отзывы |
| POST | `/api/reviews` | Customer | Создать отзыв (уходит на модерацию) |

```json
{ "productId": 3, "rating": 5, "title": "Отличная клавиатура", "body": "Работает второй месяц…" }
```

Ошибки: `403` — нет завершённого заказа с этим товаром; `409` — отзыв уже оставлен.

---

## 4. Корзина — `/api/cart`

Работает и для гостя (заголовок `X-Cart-Id`), и для авторизованного пользователя.

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/cart` | Содержимое корзины с пересчётом цен |
| POST | `/api/cart/items` | Добавить товар `{ "productId": 3, "quantity": 1 }` |
| PUT | `/api/cart/items/{productId}` | Изменить количество `{ "quantity": 2 }` |
| DELETE | `/api/cart/items/{productId}` | Удалить позицию |
| DELETE | `/api/cart` | Очистить корзину |
| POST | `/api/cart/merge` | Слить анонимную корзину с корзиной пользователя (авторизован) |

Ответ:

```json
{
  "items": [
    { "productId": 3, "name": "Keychron K8 Pro", "slug": "keychron-k8-pro",
      "unitPrice": 9990.00, "quantity": 2, "lineTotal": 19980.00,
      "stockQuantity": 14, "imageUrl": "/img/keychron-k8.jpg", "hasStockIssue": false }
  ],
  "itemsTotal": 19980.00, "itemsCount": 2, "hasIssues": false
}
```

---

## 5. Промокоды — `/api/promo`

| Метод | Путь | Доступ | Описание |
|-------|------|--------|----------|
| POST | `/api/promo/validate` | Customer | Проверка применимости кода |

Запрос `{ "code": "WELCOME10", "itemsTotal": 19980.00 }`,
ответ `{ "valid": true, "discount": 1998.00, "message": "Скидка 10%" }`.

---

## 6. Заказы — `/api/orders` (Customer)

| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/api/orders` | Оформить заказ из корзины |
| GET | `/api/orders?page=&pageSize=` | Мои заказы |
| GET | `/api/orders/{id}` | Детали заказа с историей статусов |
| POST | `/api/orders/{id}/cancel` | Отмена заказа `{ "reason": "передумал" }` |
| POST | `/api/orders/{id}/pay` | Имитация онлайн-оплаты (`New → Paid`) |

**POST `/api/orders`**

```json
{
  "deliveryMethod": "Courier",
  "deliveryAddress": "Москва, ул. Ленина, 1, кв. 2",
  "recipientName": "Иванов Иван",
  "recipientPhone": "+7 900 000-00-00",
  "paymentMethod": "CardOnline",
  "comment": "Позвонить за час",
  "promoCode": "WELCOME10"
}
```

Ответ `201 Created`:

```json
{
  "id": 42, "number": "ORD-20260908-0007", "status": "New",
  "itemsTotal": 19980.00, "discountTotal": 1998.00, "deliveryCost": 0.00, "total": 17982.00,
  "createdAt": "2026-09-08T12:30:00Z",
  "items": [ { "productName": "Keychron K8 Pro", "sku": "KB-KEY-K8", "unitPrice": 9990.00, "quantity": 2, "lineTotal": 19980.00 } ]
}
```

Ошибки: `400` — корзина пуста или не указан адрес для курьера;
`409` — недостаточно остатка (в `detail` перечислены позиции);
`422` — промокод неприменим.

---

## 7. Избранное — `/api/favorites` (Customer)

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/favorites` | Список избранных товаров |
| POST | `/api/favorites/{productId}` | Добавить |
| DELETE | `/api/favorites/{productId}` | Удалить |

## 8. Профиль — `/api/profile` (авторизован)

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/profile` | Данные профиля |
| PUT | `/api/profile` | Изменить ФИО, телефон, адрес по умолчанию |
| POST | `/api/profile/change-password` | `{ "currentPassword", "newPassword" }` |

---

## 9. Администрирование — `/api/admin` (Manager, Admin)

### 9.1. Товары

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/admin/products?search=&page=&pageSize=&includeInactive=true` | Список для таблицы |
| GET | `/api/admin/products/{id}` | Карточка для редактирования |
| POST | `/api/admin/products` | Создать |
| PUT | `/api/admin/products/{id}` | Изменить |
| DELETE | `/api/admin/products/{id}` | Деактивировать (мягкое удаление) |
| POST | `/api/admin/products/{id}/stock` | Движение остатка `{ "delta": 10, "reason": "Purchase", "comment": "Поставка №14" }` |

Тело создания/изменения товара:

```json
{
  "sku": "MS-LOG-G502", "name": "Logitech G502 X", "slug": "logitech-g502-x",
  "categoryId": 4, "brandId": 1, "description": "Игровая мышь…",
  "price": 7490.00, "oldPrice": 8990.00, "stockQuantity": 25,
  "isActive": true, "warrantyMonths": 24, "weightGrams": 89,
  "images": [ { "url": "/img/g502.jpg", "alt": "G502", "isPrimary": true, "sortOrder": 0 } ],
  "attributes": [ { "groupName": "Сенсор", "name": "Разрешение", "value": "25600", "unit": "DPI", "sortOrder": 0 } ]
}
```

### 9.2. Справочники

| Метод | Путь | Описание |
|-------|------|----------|
| GET/POST | `/api/admin/categories` | Список / создание |
| PUT/DELETE | `/api/admin/categories/{id}` | Изменение / удаление (запрещено при наличии товаров) |
| GET/POST | `/api/admin/brands` | Список / создание |
| PUT/DELETE | `/api/admin/brands/{id}` | Изменение / удаление |

### 9.3. Заказы

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/admin/orders?status=&search=&from=&to=&page=` | Очередь заказов |
| GET | `/api/admin/orders/{id}` | Детали с историей и покупателем |
| POST | `/api/admin/orders/{id}/status` | Смена статуса `{ "status": "Processing", "comment": "Собран" }` |
| GET | `/api/admin/orders/export?from=&to=` | Выгрузка CSV |

Недопустимый переход статуса → `409 Conflict` с перечнем разрешённых переходов.

### 9.4. Модерация отзывов

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/admin/reviews?approved=false&page=` | Очередь модерации |
| POST | `/api/admin/reviews/{id}/approve` | Опубликовать (пересчёт рейтинга) |
| DELETE | `/api/admin/reviews/{id}` | Отклонить и удалить |

### 9.5. Промокоды

| Метод | Путь | Описание |
|-------|------|----------|
| GET/POST | `/api/admin/promo` | Список / создание |
| PUT/DELETE | `/api/admin/promo/{id}` | Изменение / удаление |

### 9.6. Пользователи (только Admin)

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/admin/users?search=&page=` | Список пользователей |
| PUT | `/api/admin/users/{id}/role` | `{ "role": "Manager" }` |
| POST | `/api/admin/users/{id}/toggle-active` | Блокировка / разблокировка |

### 9.7. Отчёты и аудит

| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/api/admin/reports/summary?from=&to=` | Выручка, число заказов, средний чек, новые покупатели |
| GET | `/api/admin/reports/top-products?from=&to=&limit=10` | Топ продаваемых товаров |
| GET | `/api/admin/reports/low-stock?threshold=5` | Заканчивающиеся товары |
| GET | `/api/admin/reports/sales-by-day?from=&to=` | Динамика выручки по дням |
| GET | `/api/admin/audit?entity=&userId=&from=&to=&page=` | Журнал аудита (только Admin) |

---

## 10. Служебные endpoints

| Путь | Описание |
|------|----------|
| `GET /health/live` | Живость процесса |
| `GET /health/ready` | Готовность: MySQL, Redis |
| `GET /metrics` | Метрики в формате Prometheus |
| `GET /swagger` | Swagger UI (вне Production по умолчанию, включается `Swagger__Enabled=true`) |

---

## 11. Примеры вызовов (curl)

```bash
# вход
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"user@periphshop.local","password":"User123!"}' | jq -r .accessToken)

# каталог с фильтром
curl -s 'http://localhost:8080/api/catalog/products?categorySlug=klaviatury&sort=price_asc&page=1' | jq

# добавление в корзину гостем
curl -s -X POST http://localhost:8080/api/cart/items \
  -H 'Content-Type: application/json' -H 'X-Cart-Id: 6f2f0a1c-0000-4000-8000-000000000001' \
  -d '{"productId":3,"quantity":2}' | jq

# оформление заказа
curl -s -X POST http://localhost:8080/api/orders \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"deliveryMethod":"Pickup","recipientName":"Иванов Иван","recipientPhone":"+79000000000","paymentMethod":"CashOnDelivery"}' | jq
```
