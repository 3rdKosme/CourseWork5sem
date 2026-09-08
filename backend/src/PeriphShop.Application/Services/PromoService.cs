using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Промокоды: проверка применимости и управление (FR-55 ... FR-57).</summary>
public class PromoService(IAppDbContext db, IAuditService audit)
{
    public async Task<PromoValidationDto> ValidateAsync(ValidatePromoRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Code == code, ct);

        if (promo is null)
            return new PromoValidationDto(false, 0m, "Промокод не найден");

        return promo.IsApplicable(request.ItemsTotal, DateTime.UtcNow, out var message)
            ? new PromoValidationDto(true, promo.CalculateDiscount(request.ItemsTotal), message)
            : new PromoValidationDto(false, 0m, message);
    }

    public async Task<List<PromoCodeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var promos = await db.PromoCodes.OrderByDescending(p => p.Id).ToListAsync(ct);
        return promos.Select(Map).ToList();
    }

    public async Task<PromoCodeDto> CreateAsync(PromoCodeInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.PromoCodes.AnyAsync(p => p.Code == code, ct))
            throw new ConflictException("Промокод с таким кодом уже существует");
        if (input.DiscountValue <= 0)
            throw new BusinessRuleException("Размер скидки должен быть больше нуля");

        var promo = new PromoCode
        {
            Code = code,
            DiscountType = input.DiscountType,
            DiscountValue = input.DiscountValue,
            MinOrderTotal = input.MinOrderTotal,
            ValidFrom = input.ValidFrom,
            ValidTo = input.ValidTo,
            UsageLimit = input.UsageLimit,
            IsActive = input.IsActive
        };

        db.PromoCodes.Add(promo);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PromoCreated", nameof(PromoCode), promo.Id, new { promo.Code }, ct);

        return Map(promo);
    }

    public async Task<PromoCodeDto> UpdateAsync(int id, PromoCodeInput input, CancellationToken ct = default)
    {
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == id, ct)
                    ?? throw NotFoundException.For("Промокод", id);

        var code = input.Code.Trim().ToUpperInvariant();
        if (code != promo.Code && await db.PromoCodes.AnyAsync(p => p.Code == code, ct))
            throw new ConflictException("Промокод с таким кодом уже существует");

        promo.Code = code;
        promo.DiscountType = input.DiscountType;
        promo.DiscountValue = input.DiscountValue;
        promo.MinOrderTotal = input.MinOrderTotal;
        promo.ValidFrom = input.ValidFrom;
        promo.ValidTo = input.ValidTo;
        promo.UsageLimit = input.UsageLimit;
        promo.IsActive = input.IsActive;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PromoUpdated", nameof(PromoCode), id, new { promo.Code }, ct);

        return Map(promo);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == id, ct)
                    ?? throw NotFoundException.For("Промокод", id);

        if (await db.Orders.AnyAsync(o => o.PromoCodeId == id, ct))
        {
            // Код уже фигурирует в заказах — отключаем, но не удаляем историю.
            promo.IsActive = false;
            await db.SaveChangesAsync(ct);
            return;
        }

        db.PromoCodes.Remove(promo);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PromoDeleted", nameof(PromoCode), id, null, ct);
    }

    private static PromoCodeDto Map(PromoCode p) => new(
        p.Id, p.Code, p.DiscountType.ToString(), p.DiscountValue, p.MinOrderTotal,
        p.ValidFrom, p.ValidTo, p.UsageLimit, p.UsedCount, p.IsActive);
}
