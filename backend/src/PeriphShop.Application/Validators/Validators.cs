using FluentValidation;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Enums;

namespace PeriphShop.Application.Validators;

/// <summary>FR-11: требования к паролю — не короче 8 символов, буква и цифра.</summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256)
            .WithMessage("Укажите корректный e-mail");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Za-zА-Яа-я]").WithMessage("Пароль должен содержать букву")
            .Matches("[0-9]").WithMessage("Пароль должен содержать цифру");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(32);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Za-zА-Яа-я]").WithMessage("Пароль должен содержать букву")
            .Matches("[0-9]").WithMessage("Пароль должен содержать цифру");
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.DefaultAddress).MaximumLength(500);
    }
}

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 99);
    }
}

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator() => RuleFor(x => x.Quantity).InclusiveBetween(1, 99);
}

/// <summary>FR-31: состав данных оформления заказа, адрес обязателен для доставки не самовывозом.</summary>
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RecipientPhone).NotEmpty().MaximumLength(32)
            .Matches(@"^[\d\+\-\(\)\s]{6,}$").WithMessage("Укажите корректный телефон");
        RuleFor(x => x.DeliveryAddress).NotEmpty().MaximumLength(500)
            .When(x => x.DeliveryMethod != DeliveryMethod.Pickup)
            .WithMessage("Для выбранного способа доставки требуется адрес");
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.PromoCode).MaximumLength(40);
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Rating).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}

public class ProductInputValidator : AbstractValidator<ProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OldPrice).GreaterThan(x => x.Price)
            .When(x => x.OldPrice.HasValue)
            .WithMessage("Старая цена должна быть выше текущей");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WarrantyMonths).InclusiveBetween(0, 120);
    }
}

public class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class BrandInputValidator : AbstractValidator<BrandInput>
{
    public BrandInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Website).MaximumLength(300);
    }
}

public class PromoCodeInputValidator : AbstractValidator<PromoCodeInput>
{
    public PromoCodeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Код может содержать латиницу, цифры, дефис и подчёркивание");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
            .When(x => x.DiscountType == DiscountType.Percent)
            .WithMessage("Процент скидки не может превышать 100");
        RuleFor(x => x.MinOrderTotal).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue);
        RuleFor(x => x.ValidTo).GreaterThan(x => x.ValidFrom!.Value)
            .When(x => x.ValidFrom.HasValue && x.ValidTo.HasValue)
            .WithMessage("Дата окончания должна быть позже даты начала");
    }
}

public class StockMovementRequestValidator : AbstractValidator<StockMovementRequest>
{
    public StockMovementRequestValidator()
    {
        RuleFor(x => x.Delta).NotEqual(0);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(500)
            .WithMessage("Комментарий к движению остатка обязателен");
    }
}

public class ValidatePromoRequestValidator : AbstractValidator<ValidatePromoRequest>
{
    public ValidatePromoRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ItemsTotal).GreaterThanOrEqualTo(0);
    }
}
