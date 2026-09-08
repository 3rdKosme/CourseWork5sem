# PeriphShop — АИС интернет-магазина компьютерной техники и периферии

Курсовая работа, 5 семестр. Полный стенд: ASP.NET Core 9 + Angular 20 + MySQL 8.4,
с мониторингом (Prometheus, Grafana, Loki) и развёртыванием одной командой.

## Документация

| Документ | Содержание |
|----------|-----------|
| [`docs/SPEC.md`](docs/SPEC.md) | Полное техническое задание: цели, роли, 60+ функциональных требований, архитектура, безопасность, мониторинг, тестирование |
| [`docs/DB.md`](docs/DB.md) | Модель данных: 17 таблиц, ключи, индексы, инварианты, аналитические запросы |
| [`docs/API.md`](docs/API.md) | Справочник REST API с примерами запросов и ответов |
| [`db/README.md`](db/README.md) | Работа с БД и миграциями |

## Быстрый старт

```bash
cp .env.example .env          # при необходимости поменять пароли и Jwt-ключ
docker compose up -d --build  # поднять весь стенд
docker compose logs -f api    # дождаться строки "Application started"
```

После старта доступно:

| Адрес | Назначение | Доступ |
|-------|-----------|--------|
| http://localhost | Магазин (Angular SPA) | — |
| http://localhost:8080/swagger | Swagger UI Web API | — |
| http://localhost:8080/metrics | Метрики Prometheus | — |
| http://localhost:3000 | Grafana с готовыми дашбордами | admin / admin |
| http://localhost:9090 | Prometheus | — |
| http://localhost:8025 | Mailpit — письма о заказах | — |
| http://localhost:8081 | Adminer — веб-клиент БД | сервер `mysql`, БД `periphshop` |
| http://localhost:9001 | Консоль MinIO | из `.env` |

Демонстрационные учётные записи (создаются сидом):

| Роль | Логин | Пароль |
|------|-------|--------|
| Администратор | `admin@periphshop.local` | `Admin123!` |
| Менеджер | `manager@periphshop.local` | `Manager123!` |
| Покупатель | `user@periphshop.local` | `User123!` |

## Что реализовано

**Покупатель:** каталог с фасетным фильтром и сортировкой, карточка товара с характеристиками
и отзывами, корзина для гостя и пользователя со слиянием при входе, оформление заказа
с промокодом и выбором доставки, оплата картой (имитация шлюза), отмена заказа,
личный кабинет, избранное, отзывы с модерацией.

**Менеджер и администратор:** сводка по выручке и заказам, топ товаров, отчёт по низким остаткам,
очередь заказов со сменой статуса по графу переходов, выгрузка заказов в CSV, CRUD товаров,
движения складского остатка, модерация отзывов, промокоды, управление пользователями
и ролями, журнал аудита.

**Платформа:** JWT с ротацией refresh-токенов, RBAC, rate limiting, валидация FluentValidation,
ответы `application/problem+json`, транзакционное списание остатков, структурные логи,
метрики Prometheus (в том числе бизнес-метрики), health-checks, миграции и сид при старте.

## Структура репозитория

```
backend/                     ASP.NET Core 9, слоистая архитектура
  src/PeriphShop.Domain/       сущности, перечисления, доменные правила
  src/PeriphShop.Application/  DTO, сервисы, валидаторы
  src/PeriphShop.Infrastructure/ EF Core, миграции, сид, JWT, SMTP, аудит
  src/PeriphShop.Api/          контроллеры, middleware, метрики, Swagger
  tests/PeriphShop.Tests/      33 юнит-теста бизнес-логики
frontend/                    Angular 20 (standalone-компоненты, signals)
db/                          init-скрипты и заметки по БД
docs/                        ТЗ, модель данных, описание API
ops/                         Prometheus, Grafana, Loki, Promtail
docker-compose.yml           стенд целиком
```

## Разработка без Docker

```bash
# 1. База данных
docker compose up -d mysql redis

# 2. Backend (http://localhost:8080)
cd backend
dotnet run --project src/PeriphShop.Api

# 3. Frontend (http://localhost:4200, прокси /api → :8080)
cd frontend
npm install
npm start

# Тесты
cd backend && dotnet test
```

## Мониторинг

Дашборды Grafana создаются автоматически (папка **PeriphShop**):

- **API Overview** — RPS, доля 5xx, латентность p50/p95/p99, топ маршрутов.
- **Business** — заказы, выручка, средний чек, добавления в корзину, отказы оформления,
  переходы статусов, попытки входа.
- **Infrastructure** — CPU и память контейнеров, состояние MySQL, InnoDB buffer pool.
- **Logs** — поток структурных логов из Loki с фильтром по уровню.

Правила оповещения — в [`ops/prometheus/alerts.yml`](ops/prometheus/alerts.yml).

## Лицензии используемых компонентов

Все компоненты стенда — открытые: .NET, EF Core, Pomelo, Angular, Nginx, Redis, MySQL,
Prometheus, Grafana OSS, Loki, Promtail, MinIO, Mailpit, Adminer, cAdvisor, node-exporter.
