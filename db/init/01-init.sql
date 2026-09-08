-- Инициализация экземпляра MySQL для АИС PeriphShop.
-- Схема таблиц создаётся миграциями EF Core при старте API (см. docs/DB.md),
-- здесь задаются только параметры экземпляра и права прикладного пользователя.

ALTER DATABASE periphshop CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

SET GLOBAL time_zone = '+00:00';

-- Прикладной пользователь создаётся переменными окружения контейнера,
-- дополнительно выдаём права на создание схемы (нужны миграциям).
GRANT ALL PRIVILEGES ON periphshop.* TO 'periphshop'@'%';

-- Пользователь только для чтения метрик mysqld-exporter.
CREATE USER IF NOT EXISTS 'exporter'@'%' IDENTIFIED BY 'exporter';
GRANT PROCESS, REPLICATION CLIENT, SELECT ON *.* TO 'exporter'@'%';

FLUSH PRIVILEGES;
