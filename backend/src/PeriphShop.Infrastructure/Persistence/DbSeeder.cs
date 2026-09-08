using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeriphShop.Application.Abstractions;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;

namespace PeriphShop.Infrastructure.Persistence;

/// <summary>
/// Идемпотентное наполнение БД демонстрационными данными (раздел 11.3 ТЗ).
/// Каждый блок выполняется только при пустой таблице, поэтому повторный запуск безопасен.
/// </summary>
public class DbSeeder(AppDbContext db, IPasswordHasher hasher, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedUsersAsync(ct);
        await SeedCatalogAsync(ct);
        await SeedPromoCodesAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        db.Users.AddRange(
            new User
            {
                Email = "admin@periphshop.local",
                PasswordHash = hasher.Hash("Admin123!"),
                FullName = "Администратор системы",
                Phone = "+7 900 000-00-01",
                Role = UserRole.Admin
            },
            new User
            {
                Email = "manager@periphshop.local",
                PasswordHash = hasher.Hash("Manager123!"),
                FullName = "Петров Пётр (менеджер)",
                Phone = "+7 900 000-00-02",
                Role = UserRole.Manager
            },
            new User
            {
                Email = "user@periphshop.local",
                PasswordHash = hasher.Hash("User123!"),
                FullName = "Иванов Иван",
                Phone = "+7 900 000-00-03",
                DefaultAddress = "г. Москва, ул. Ленина, д. 1, кв. 2",
                Role = UserRole.Customer
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Сид: созданы учётные записи по умолчанию");
    }

    private async Task SeedPromoCodesAsync(CancellationToken ct)
    {
        if (await db.PromoCodes.AnyAsync(ct)) return;

        db.PromoCodes.AddRange(
            new PromoCode
            {
                Code = "WELCOME10",
                DiscountType = DiscountType.Percent,
                DiscountValue = 10m,
                MinOrderTotal = 3000m,
                ValidTo = DateTime.UtcNow.AddYears(1),
                UsageLimit = 1000
            },
            new PromoCode
            {
                Code = "PERIPH500",
                DiscountType = DiscountType.Amount,
                DiscountValue = 500m,
                MinOrderTotal = 7000m,
                ValidTo = DateTime.UtcNow.AddMonths(6),
                UsageLimit = 200
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Сид: созданы промокоды WELCOME10 и PERIPH500");
    }

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        if (await db.Products.AnyAsync(ct)) return;

        var peripherals = new Category { Name = "Периферия", Slug = "periferiya", SortOrder = 10 };
        var keyboards = new Category { Name = "Клавиатуры", Slug = "klaviatury", Parent = peripherals, SortOrder = 11 };
        var mice = new Category { Name = "Мыши", Slug = "myshi", Parent = peripherals, SortOrder = 12 };
        var headsets = new Category { Name = "Гарнитуры", Slug = "garnitury", Parent = peripherals, SortOrder = 13 };
        var pads = new Category { Name = "Коврики", Slug = "kovriki", Parent = peripherals, SortOrder = 14 };
        var monitors = new Category { Name = "Мониторы", Slug = "monitory", SortOrder = 20 };
        var components = new Category { Name = "Комплектующие", Slug = "komplektuyushchie", SortOrder = 30 };
        var storage = new Category { Name = "Накопители", Slug = "nakopiteli", Parent = components, SortOrder = 31 };
        var gpus = new Category { Name = "Видеокарты", Slug = "videokarty", Parent = components, SortOrder = 32 };

        db.Categories.AddRange(peripherals, keyboards, mice, headsets, pads, monitors, components, storage, gpus);

        var logitech = new Brand { Name = "Logitech", Slug = "logitech", Country = "Швейцария", Website = "https://logitech.com" };
        var keychron = new Brand { Name = "Keychron", Slug = "keychron", Country = "Китай", Website = "https://keychron.com" };
        var hyperx = new Brand { Name = "HyperX", Slug = "hyperx", Country = "США", Website = "https://hyperx.com" };
        var razer = new Brand { Name = "Razer", Slug = "razer", Country = "Сингапур", Website = "https://razer.com" };
        var samsung = new Brand { Name = "Samsung", Slug = "samsung", Country = "Республика Корея", Website = "https://samsung.com" };
        var asus = new Brand { Name = "ASUS", Slug = "asus", Country = "Тайвань", Website = "https://asus.com" };

        db.Brands.AddRange(logitech, keychron, hyperx, razer, samsung, asus);

        var now = DateTime.UtcNow;
        var products = new List<Product>
        {
            Make("KB-KEY-K8", "Keychron K8 Pro", keyboards, keychron, 9990m, 11990m, 14, 24, 810,
                "Беспроводная механическая клавиатура формата TKL с горячей заменой переключателей и алюминиевым корпусом.",
                [("Тип", "Форм-фактор", "TKL (87 клавиш)", null),
                 ("Тип", "Переключатели", "Gateron G Pro Red", null),
                 ("Подключение", "Интерфейс", "Bluetooth 5.1 / USB-C", null),
                 ("Питание", "Аккумулятор", "4000", "мАч")], 41, 4.7m),

            Make("KB-KEY-K2", "Keychron K2 V2", keyboards, keychron, 7990m, null, 22, 24, 750,
                "Компактная механическая клавиатура 75% с RGB-подсветкой и поддержкой трёх устройств.",
                [("Тип", "Форм-фактор", "75%", null),
                 ("Тип", "Переключатели", "Gateron Brown", null),
                 ("Подключение", "Интерфейс", "Bluetooth / USB-C", null)], 63, 4.5m),

            Make("KB-LOG-MX", "Logitech MX Keys S", keyboards, logitech, 12490m, 13990m, 9, 24, 810,
                "Тихая мембранная клавиатура для работы с подсветкой и переключением между тремя устройствами.",
                [("Тип", "Форм-фактор", "Полноразмерная", null),
                 ("Подключение", "Интерфейс", "Bluetooth / Logi Bolt", null),
                 ("Питание", "Автономность", "10", "дней")], 88, 4.8m),

            Make("KB-RAZ-HUN", "Razer Huntsman Mini", keyboards, razer, 8990m, null, 4, 24, 480,
                "Компактная клавиатура 60% с оптическими переключателями и временем отклика 0.2 мс.",
                [("Тип", "Форм-фактор", "60%", null),
                 ("Тип", "Переключатели", "Razer Optical Linear", null)], 37, 4.4m),

            Make("MS-LOG-G502", "Logitech G502 X", mice, logitech, 7490m, 8990m, 25, 24, 89,
                "Игровая мышь с сенсором HERO 25K, гибридными переключателями и 13 программируемыми кнопками.",
                [("Сенсор", "Разрешение", "25600", "DPI"),
                 ("Сенсор", "Модель", "HERO 25K", null),
                 ("Конструкция", "Кнопки", "13", "шт"),
                 ("Конструкция", "Вес", "89", "г")], 154, 4.8m),

            Make("MS-LOG-MX3", "Logitech MX Master 3S", mice, logitech, 10990m, 12490m, 12, 24, 141,
                "Офисная мышь с бесшумными кнопками, колесом MagSpeed и сенсором 8000 DPI.",
                [("Сенсор", "Разрешение", "8000", "DPI"),
                 ("Подключение", "Интерфейс", "Bluetooth / Logi Bolt", null),
                 ("Питание", "Автономность", "70", "дней")], 121, 4.9m),

            Make("MS-RAZ-VIP", "Razer Viper V3 Pro", mice, razer, 14990m, null, 6, 24, 54,
                "Сверхлёгкая беспроводная киберспортивная мышь с частотой опроса 8000 Гц.",
                [("Сенсор", "Разрешение", "35000", "DPI"),
                 ("Конструкция", "Вес", "54", "г"),
                 ("Подключение", "Опрос", "8000", "Гц")], 64, 4.6m),

            Make("MS-HYP-PUL", "HyperX Pulsefire Haste 2", mice, hyperx, 5490m, 6490m, 31, 24, 61,
                "Лёгкая игровая мышь с сенсором HyperX 26K и переключателями с ресурсом 100 млн нажатий.",
                [("Сенсор", "Разрешение", "26000", "DPI"),
                 ("Конструкция", "Вес", "61", "г")], 98, 4.5m),

            Make("HS-HYP-CL2", "HyperX Cloud II", headsets, hyperx, 8490m, 9990m, 18, 24, 320,
                "Проводная игровая гарнитура с виртуальным звуком 7.1 и съёмным микрофоном.",
                [("Звук", "Излучатели", "53", "мм"),
                 ("Звук", "Объёмный звук", "7.1 виртуальный", null),
                 ("Подключение", "Интерфейс", "USB / 3.5 мм", null)], 143, 4.7m),

            Make("HS-RAZ-BLK", "Razer BlackShark V2 X", headsets, razer, 5990m, null, 27, 24, 240,
                "Лёгкая гарнитура с шумоизоляцией и кардиоидным микрофоном.",
                [("Звук", "Излучатели", "50", "мм"),
                 ("Подключение", "Интерфейс", "3.5 мм", null)], 76, 4.3m),

            Make("HS-LOG-G335", "Logitech G335", headsets, logitech, 6490m, 7490m, 3, 24, 240,
                "Компактная гарнитура с подвесным оголовьем и микрофоном с флип-мьютом.",
                [("Звук", "Излучатели", "40", "мм"),
                 ("Конструкция", "Вес", "240", "г")], 44, 4.2m),

            Make("PD-RAZ-GIG", "Razer Gigantus V2 XXL", pads, razer, 2490m, null, 40, 12, 620,
                "Тканевый коврик размера XXL с прорезиненным основанием и прошитыми краями.",
                [("Размеры", "Габариты", "940x410x4", "мм"),
                 ("Материал", "Поверхность", "Микротекстурная ткань", null)], 210, 4.6m),

            Make("PD-HYP-FUR", "HyperX Fury S Pro L", pads, hyperx, 1590m, 1990m, 55, 12, 320,
                "Игровой коврик среднего размера с оптимизированной для сенсоров поверхностью.",
                [("Размеры", "Габариты", "450x400x4", "мм")], 180, 4.4m),

            Make("MN-SAM-G5", "Samsung Odyssey G5 27", monitors, samsung, 32990m, 37990m, 7, 36, 5400,
                "Изогнутый игровой монитор 27 дюймов, 2560x1440, 165 Гц, 1 мс, поддержка FreeSync Premium.",
                [("Экран", "Диагональ", "27", "дюймов"),
                 ("Экран", "Разрешение", "2560x1440", null),
                 ("Экран", "Частота обновления", "165", "Гц"),
                 ("Экран", "Матрица", "VA, изогнутая 1000R", null)], 52, 4.5m),

            Make("MN-ASU-VG2", "ASUS TUF Gaming VG249Q1A", monitors, asus, 19990m, 22990m, 11, 36, 4300,
                "Игровой монитор 23.8 дюйма, Full HD, IPS, 165 Гц, технология ELMB.",
                [("Экран", "Диагональ", "23.8", "дюймов"),
                 ("Экран", "Разрешение", "1920x1080", null),
                 ("Экран", "Частота обновления", "165", "Гц"),
                 ("Экран", "Матрица", "IPS", null)], 87, 4.6m),

            Make("MN-SAM-S8", "Samsung ViewFinity S8 32", monitors, samsung, 45990m, null, 2, 36, 7200,
                "Профессиональный монитор 32 дюйма, 4K UHD, 99% sRGB, USB-C с питанием 90 Вт.",
                [("Экран", "Диагональ", "32", "дюйма"),
                 ("Экран", "Разрешение", "3840x2160", null),
                 ("Цвет", "Охват sRGB", "99", "%")], 24, 4.7m),

            Make("SS-SAM-990", "Samsung 990 PRO 1 ТБ", storage, samsung, 12990m, 14990m, 16, 60, 9,
                "NVMe-накопитель PCIe 4.0 со скоростью чтения до 7450 МБ/с.",
                [("Накопитель", "Объём", "1", "ТБ"),
                 ("Накопитель", "Интерфейс", "PCIe 4.0 x4, M.2 2280", null),
                 ("Скорость", "Чтение", "7450", "МБ/с"),
                 ("Скорость", "Запись", "6900", "МБ/с")], 132, 4.9m),

            Make("SS-SAM-870", "Samsung 870 EVO 500 ГБ", storage, samsung, 5490m, 6490m, 34, 60, 50,
                "SATA SSD формата 2.5 дюйма с ресурсом 300 TBW.",
                [("Накопитель", "Объём", "500", "ГБ"),
                 ("Накопитель", "Интерфейс", "SATA III", null),
                 ("Скорость", "Чтение", "560", "МБ/с")], 165, 4.8m),

            Make("GP-ASU-4070", "ASUS Dual GeForce RTX 4070 12 ГБ", gpus, asus, 74990m, 82990m, 3, 36, 1050,
                "Видеокарта с 12 ГБ GDDR6X, двумя вентиляторами Axial-tech и осевым обдувом.",
                [("Графика", "Память", "12", "ГБ GDDR6X"),
                 ("Графика", "Шина", "192", "бит"),
                 ("Питание", "Рекомендуемый БП", "650", "Вт")], 29, 4.7m),

            Make("GP-ASU-4060", "ASUS TUF Gaming GeForce RTX 4060 8 ГБ", gpus, asus, 42990m, null, 5, 36, 900,
                "Видеокарта среднего сегмента с усиленной системой охлаждения и защитой от пыли.",
                [("Графика", "Память", "8", "ГБ GDDR6"),
                 ("Графика", "Шина", "128", "бит"),
                 ("Питание", "Рекомендуемый БП", "550", "Вт")], 38, 4.4m)
        };

        db.Products.AddRange(products);

        // Приход товара фиксируется движением склада, как и любое изменение остатка (FR-45).
        foreach (var product in products)
        {
            db.StockMovements.Add(new StockMovement
            {
                Product = product,
                Delta = product.StockQuantity,
                Reason = StockMovementReason.Purchase,
                Comment = "Начальный остаток (демонстрационные данные)",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Сид: загружено {Count} товаров в {Categories} категориях", products.Count, 9);
    }

    private static Product Make(
        string sku, string name, Category category, Brand brand,
        decimal price, decimal? oldPrice, int stock, int warranty, int weight,
        string description,
        (string Group, string Name, string Value, string? Unit)[] attributes,
        int soldCount, decimal rating)
    {
        var slug = Application.Common.Slug.From(name);
        var product = new Product
        {
            Sku = sku,
            Name = name,
            Slug = slug,
            Category = category,
            Brand = brand,
            Description = description,
            Price = price,
            OldPrice = oldPrice,
            StockQuantity = stock,
            WarrantyMonths = warranty,
            WeightGrams = weight,
            SoldCount = soldCount,
            RatingAvg = rating,
            RatingCount = Math.Max(1, soldCount / 8),
            CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 120))
        };

        product.Images.Add(new ProductImage
        {
            Url = $"/assets/products/{slug}.svg",
            Alt = name,
            IsPrimary = true,
            SortOrder = 0
        });

        var order = 0;
        foreach (var (group, attributeName, value, unit) in attributes)
        {
            product.Attributes.Add(new ProductAttribute
            {
                GroupName = group,
                Name = attributeName,
                Value = value,
                Unit = unit,
                SortOrder = order++
            });
        }

        return product;
    }
}
