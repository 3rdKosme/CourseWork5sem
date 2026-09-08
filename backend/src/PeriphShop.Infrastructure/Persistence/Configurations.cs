using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeriphShop.Domain.Entities;

namespace PeriphShop.Infrastructure.Persistence;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.DefaultAddress).HasMaxLength(500);
        b.Property(x => x.Role).HasConversion<byte>();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.Token).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne(x => x.Parent).WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("brands");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.Website).HasMaxLength(300);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_price", "price >= 0");
            t.HasCheckConstraint("ck_products_stock", "stock_quantity >= 0");
        });

        b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Sku).IsUnique();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(320).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Description).HasColumnType("text");
        b.Property(x => x.Price).HasPrecision(12, 2);
        b.Property(x => x.OldPrice).HasPrecision(12, 2);
        b.Property(x => x.RatingAvg).HasPrecision(3, 2);

        b.HasIndex(x => x.Price);
        b.HasIndex(x => new { x.IsActive, x.CreatedAt });

        b.HasOne(x => x.Category).WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Brand).WithMany(br => br.Products)
            .HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.InStock);
        b.Ignore(x => x.HasDiscount);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.ToTable("product_images");
        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.Property(x => x.Alt).HasMaxLength(300);
        b.HasOne(x => x.Product).WithMany(p => p.Images)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.ToTable("product_attributes");
        b.Property(x => x.GroupName).HasMaxLength(100);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Value).HasMaxLength(300).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(30);
        b.HasIndex(x => x.ProductId);
        b.HasOne(x => x.Product).WithMany(p => p.Attributes)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("reviews", t => t.HasCheckConstraint("ck_reviews_rating", "rating between 1 and 5"));
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique();
        b.HasIndex(x => x.IsApproved);
        b.HasOne(x => x.Product).WithMany(p => p.Reviews)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(u => u.Reviews)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.ToTable("favorites");
        b.HasKey(x => new { x.UserId, x.ProductId });
        b.HasOne(x => x.User).WithMany(u => u.Favorites)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("carts");
        b.Property(x => x.AnonymousId).HasMaxLength(36);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.AnonymousId).IsUnique();
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("cart_items", t => t.HasCheckConstraint("ck_cart_items_quantity", "quantity between 1 and 99"));
        b.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
        b.HasOne(x => x.Cart).WithMany(c => c.Items)
            .HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.Property(x => x.Number).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.CreatedAt);

        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.DeliveryMethod).HasConversion<byte>();
        b.Property(x => x.PaymentMethod).HasConversion<byte>();
        b.Property(x => x.PaymentStatus).HasConversion<byte>();

        b.Property(x => x.ItemsTotal).HasPrecision(12, 2);
        b.Property(x => x.DiscountTotal).HasPrecision(12, 2);
        b.Property(x => x.DeliveryCost).HasPrecision(12, 2);
        b.Property(x => x.Total).HasPrecision(12, 2);

        b.Property(x => x.DeliveryAddress).HasMaxLength(500);
        b.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
        b.Property(x => x.RecipientPhone).HasMaxLength(32).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.CancelReason).HasMaxLength(500);

        b.HasOne(x => x.User).WithMany(u => u.Orders)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PromoCode).WithMany(p => p.Orders)
            .HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_quantity", "quantity >= 1"));
        b.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Property(x => x.LineTotal).HasPrecision(12, 2);

        b.HasOne(x => x.Order).WithMany(o => o.Items)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("order_status_history");
        b.Property(x => x.FromStatus).HasConversion<byte?>();
        b.Property(x => x.ToStatus).HasConversion<byte>();
        b.Property(x => x.Comment).HasMaxLength(500);

        b.HasOne(x => x.Order).WithMany(o => o.StatusHistory)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ChangedByUser).WithMany()
            .HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> b)
    {
        b.ToTable("promo_codes");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.DiscountType).HasConversion<byte>();
        b.Property(x => x.DiscountValue).HasPrecision(12, 2);
        b.Property(x => x.MinOrderTotal).HasPrecision(12, 2);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements");
        b.Property(x => x.Reason).HasConversion<byte>();
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => x.ProductId);
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Order).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.Entity).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Payload).HasColumnType("json");
        b.Property(x => x.Ip).HasMaxLength(64);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.Entity, x.EntityId });

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
