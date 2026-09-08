# База данных

- СУБД: MySQL 8.4, кодировка `utf8mb4`, движок InnoDB.
- Схема описана в [`../docs/DB.md`](../docs/DB.md) и создаётся **миграциями EF Core**
  (`backend/src/PeriphShop.Infrastructure/Persistence/Migrations`), которые применяются
  автоматически при старте контейнера `api`.
- `init/01-init.sql` выполняется один раз при первичной инициализации тома MySQL:
  задаёт кодировку и часовой пояс, выдаёт права прикладному пользователю
  и создаёт учётную запись для `mysqld-exporter`.

## Полезные команды

```bash
# консоль MySQL внутри контейнера
docker compose exec mysql mysql -u periphshop -p periphshop

# создать новую миграцию после изменения модели
dotnet ef migrations add <Name> \
  --project backend/src/PeriphShop.Infrastructure \
  --startup-project backend/src/PeriphShop.Api \
  --output-dir Persistence/Migrations

# получить SQL-скрипт схемы без применения
dotnet ef migrations script \
  --project backend/src/PeriphShop.Infrastructure \
  --startup-project backend/src/PeriphShop.Api

# полный сброс данных стенда
docker compose down -v && docker compose up -d
```
