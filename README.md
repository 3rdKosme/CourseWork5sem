# PeriphShop

Автоматизированная информационная система интернет-магазина компьютерной техники
и периферии. Курсовая работа, 5 семестр.

Система закрывает полный цикл розничной продажи: каталог товаров, корзина,
оформление и сопровождение заказа, складской учёт, рабочее место менеджера
и наблюдаемость (метрики, логи, дашборды).

## Стек

| Часть | Технологии |
|-------|-----------|
| Backend | ASP.NET Core 9 (C#), EF Core 9, Pomelo MySQL, JWT, Serilog, prometheus-net |
| Frontend | Angular 20 (standalone-компоненты, signals), SCSS |
| База данных | MySQL 8.4 |
| Инфраструктура | Docker Compose, Nginx, Redis, MinIO, Mailpit, Adminer |
| Мониторинг | Prometheus, Grafana, Loki, Promtail, mysqld/node-exporter, cAdvisor |

Все компоненты — с открытыми лицензиями.

## Возможности

**Покупатель**
- каталог с фильтрами по категории, бренду, цене, наличию и скидке, поиск и сортировка;
- карточка товара: характеристики, изображения, отзывы, похожие товары;
- корзина (работает и для гостя, при входе сливается с корзиной пользователя);
- оформление заказа: способ доставки, оплата, промокод, комментарий;
- личный кабинет: список заказов, детали и история статусов, отмена, избранное, профиль.

**Менеджер и администратор**
- сводка: выручка, число заказов, средний чек, динамика продаж, топ товаров;
- очередь заказов: фильтры, смена статуса по заданному графу переходов, выгрузка в CSV;
- товары: создание и редактирование, движения складского остатка с историей;
- модерация отзывов, промокоды, управление пользователями и ролями, журнал аудита.

**Платформа**
- роли Guest / Customer / Manager / Admin и разграничение доступа;
- JWT с обновлением токенов, ограничение частоты запросов, валидация запросов;
- списание и возврат складских остатков в транзакции;
- метрики Prometheus (технические и бизнес-), структурные логи, health-checks;
- миграции БД и демонстрационные данные применяются при старте.

## Структура репозитория

```
backend/     ASP.NET Core: Domain / Application / Infrastructure / Api + тесты
frontend/    Angular SPA
db/          init-скрипты MySQL и заметки по работе с БД
docs/        техническое задание, модель данных, описание API
ops/         конфигурация Prometheus, Grafana, Loki, Promtail
docker-compose.yml
```

## Документация

| Файл | О чём |
|------|-------|
| [`docs/SPEC.md`](docs/SPEC.md) | Техническое задание: цели, роли, требования, архитектура, безопасность, мониторинг |
| [`docs/DB.md`](docs/DB.md) | Модель данных: таблицы, связи, индексы, инварианты |
| [`docs/API.md`](docs/API.md) | Справочник REST API с примерами |
| [`db/README.md`](db/README.md) | Работа с базой и миграциями |

## Запуск через Docker

Нужен Docker Desktop с работающим движком (на Windows — включённый WSL 2).

```bash
cp .env.example .env
docker compose up -d --build
```

| Адрес | Что это |
|-------|---------|
| http://localhost | Магазин |
| http://localhost:8080/swagger | Swagger UI |
| http://localhost:3000 | Grafana (admin / admin) |
| http://localhost:9090 | Prometheus |
| http://localhost:8025 | Mailpit — письма о заказах |
| http://localhost:8081 | Adminer — веб-клиент БД |

## Запуск без Docker

Нужны .NET SDK 9, Node.js 20+ и запущенный MySQL 8.

```bash
# 1. Создать базу и пользователя в MySQL
#    CREATE DATABASE periphshop CHARACTER SET utf8mb4;
#    CREATE USER 'periphshop'@'%' IDENTIFIED BY 'periphshop';
#    GRANT ALL PRIVILEGES ON periphshop.* TO 'periphshop'@'%';

# 2. Backend — http://localhost:8080
#    строка подключения и Jwt:Key берутся из backend/src/PeriphShop.Api/appsettings.json
cd backend
dotnet run --project src/PeriphShop.Api

# 3. Frontend — http://localhost:4200 (запросы /api проксируются на :8080)
cd frontend
npm install
npm start
```

Миграции и демонстрационные данные применяются автоматически при первом запуске API.

## Учётные записи демо-данных

| Роль | Логин | Пароль |
|------|-------|--------|
| Администратор | `admin@periphshop.local` | `Admin123!` |
| Менеджер | `manager@periphshop.local` | `Manager123!` |
| Покупатель | `user@periphshop.local` | `User123!` |

## Тесты

```bash
cd backend && dotnet test
```

Покрыта бизнес-логика заказов, корзины, склада, промокодов и отзывов.
